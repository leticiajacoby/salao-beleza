using System.Security.Claims;
using Microsoft.EntityFrameworkCore;
using SalaoBeleza.Common;
using SalaoBeleza.Data;
using SalaoBeleza.DTOs;
using SalaoBeleza.Models;

namespace SalaoBeleza.Services;

public class AgendamentoService
{
    private readonly AppDbContext _db;
    private readonly DisponibilidadeService _disponibilidade;
    private readonly CupomService _cupons;
    private readonly FidelidadeService _fidelidade;
    private readonly NotificacaoService _notificacoes;

    public AgendamentoService(
        AppDbContext db,
        DisponibilidadeService disponibilidade,
        CupomService cupons,
        FidelidadeService fidelidade,
        NotificacaoService notificacoes)
    {
        _db = db;
        _disponibilidade = disponibilidade;
        _cupons = cupons;
        _fidelidade = fidelidade;
        _notificacoes = notificacoes;
    }

    // ---------- CONSULTAS ----------

    // "de" e inclusivo e "ate" e exclusivo. Cada perfil so enxerga o que e seu.
    public async Task<List<AgendamentoRespostaDTO>> Listar(
        ClaimsPrincipal user, DateTime? de, DateTime? ate, int? barbeiroId, string? status)
    {
        var q = ComDetalhes(_db.Agendamentos.AsNoTracking());

        if (user.EhCliente())
        {
            var clienteId = user.IdUsuario();
            q = q.Where(a => a.ClienteId == clienteId);
        }
        else if (user.EhBarbeiro())
        {
            var meuBarbeiroId = user.IdBarbeiro() ?? -1;
            q = q.Where(a => a.BarbeiroId == meuBarbeiroId);
        }
        else if (barbeiroId.HasValue)
        {
            var filtro = barbeiroId.Value;
            q = q.Where(a => a.BarbeiroId == filtro);
        }

        if (de.HasValue)
        {
            var inicio = Tempo.SemKind(de.Value);
            q = q.Where(a => a.DataHora >= inicio);
        }

        if (ate.HasValue)
        {
            var fim = Tempo.SemKind(ate.Value);
            q = q.Where(a => a.DataHora < fim);
        }

        if (!string.IsNullOrWhiteSpace(status))
        {
            var st = status.Trim();
            q = q.Where(a => a.Status == st);
        }

        var lista = await q.OrderBy(a => a.DataHora).ToListAsync();

        return lista.Select(Mapear).ToList();
    }

    public async Task<AgendamentoRespostaDTO> Obter(ClaimsPrincipal user, int id)
    {
        var ag = await ComDetalhes(_db.Agendamentos.AsNoTracking()).FirstOrDefaultAsync(a => a.Id == id);

        if (ag == null || !PodeVer(user, ag))
            throw new NegocioException("Agendamento nao encontrado.", 404);

        return Mapear(ag);
    }

    // Mostra quanto o cliente pagaria (com cupom / servico gratis) antes de confirmar.
    public async Task<SimulacaoRespostaDTO> Simular(ClaimsPrincipal user, SimularDTO dto)
    {
        var servicos = await CarregarServicos(dto.ServicoIds);

        var total = servicos.Sum(s => s.Preco);
        var duracao = servicos.Sum(s => s.DuracaoMinutos);

        var resposta = new SimulacaoRespostaDTO
        {
            ValorServico = total,
            ValorDesconto = 0,
            ValorFinal = total,
            DuracaoMinutos = duracao
        };

        if (!user.EhCliente())
            return resposta;

        try
        {
            var desconto = await CalcularDesconto(user.IdUsuario(), total, dto.CupomCodigo, dto.UsarFidelidade);

            resposta.ValorDesconto = desconto.Valor;
            resposta.ValorFinal = total - desconto.Valor;
            resposta.CupomCodigo = desconto.Cupom?.Codigo;
            resposta.UsouFidelidade = desconto.Fidelidade;
        }
        catch (NegocioException ex)
        {
            resposta.Aviso = ex.Message;
        }

        return resposta;
    }

    // ---------- ACOES ----------

    public async Task<AgendamentoRespostaDTO> Criar(ClaimsPrincipal user, AgendamentoCriarDTO dto)
    {
        int clienteId;

        if (user.EhAdmin())
        {
            if (!dto.ClienteId.HasValue)
                throw new NegocioException("Informe o cliente.");

            clienteId = dto.ClienteId.Value;
        }
        else if (user.EhCliente())
        {
            clienteId = user.IdUsuario();
        }
        else
        {
            throw new NegocioException("Apenas clientes podem fazer agendamentos.", 403);
        }

        var cliente = await _db.Usuarios.FirstOrDefaultAsync(u => u.Id == clienteId && u.Tipo == "Cliente");
        if (cliente == null)
            throw new NegocioException("Cliente nao encontrado.", 404);

        var inicio = Tempo.Normalizar(dto.DataHora);
        if (inicio <= Tempo.Agora())
            throw new NegocioException("A data do agendamento nao pode estar no passado.");

        var servicos = await CarregarServicos(dto.ServicoIds);

        var barbeiro = await _db.Barbeiros.FirstOrDefaultAsync(b => b.Id == dto.BarbeiroId);
        if (barbeiro == null)
            throw new NegocioException("Profissional nao encontrada.", 404);

        if (!barbeiro.Ativo)
            throw new NegocioException("Esta profissional nao esta disponivel no momento.");

        var duracao = servicos.Sum(s => s.DuracaoMinutos);
        var total = servicos.Sum(s => s.Preco);

        await _disponibilidade.GarantirLivre(barbeiro.Id, inicio, duracao, null);

        var desconto = await CalcularDesconto(clienteId, total, dto.CupomCodigo, dto.UsarFidelidade);

        var comissao = Math.Round(total * barbeiro.PercentualBarbeiro / 100m, 2);
        var valorFinal = total - desconto.Valor;

        var agendamento = new Agendamento
        {
            ClienteId = clienteId,
            BarbeiroId = barbeiro.Id,
            ServicoId = servicos[0].Id,
            DataHora = inicio,
            DuracaoMinutos = duracao,
            Status = "Agendado",
            ValorServico = total,
            ValorDesconto = desconto.Valor,
            ValorFinal = valorFinal,
            PercentualBarbeiro = barbeiro.PercentualBarbeiro,
            ValorBarbeiro = comissao,
            ValorAdministrador = valorFinal - comissao,
            CupomId = desconto.Cupom?.Id,
            CupomCodigo = desconto.Cupom?.Codigo,
            UsouFidelidade = desconto.Fidelidade,
            CriadoEm = Tempo.Agora()
        };

        foreach (var s in servicos)
        {
            agendamento.Itens.Add(new AgendamentoServico
            {
                ServicoId = s.Id,
                Nome = s.Nome,
                Preco = s.Preco,
                DuracaoMinutos = s.DuracaoMinutos
            });
        }

        if (desconto.Cupom != null)
            desconto.Cupom.UsosAtuais++;

        _db.Agendamentos.Add(agendamento);

        _notificacoes.Adicionar(
            barbeiro.UsuarioId,
            "Novo agendamento",
            cliente.Nome + " agendou " + string.Join(" + ", servicos.Select(s => s.Nome)) + " para " + Quando(inicio) + ".");

        await SalvarComConflito();

        return await ObterDto(agendamento.Id);
    }

    public async Task<AgendamentoRespostaDTO> Reagendar(ClaimsPrincipal user, int id, ReagendarDTO dto)
    {
        var ag = await _db.Agendamentos.Include(a => a.Cliente).FirstOrDefaultAsync(a => a.Id == id);

        if (ag == null || !(user.EhAdmin() || (user.EhCliente() && ag.ClienteId == user.IdUsuario())))
            throw new NegocioException("Agendamento nao encontrado.", 404);

        if (ag.Status != "Agendado")
            throw new NegocioException("Apenas agendamentos ativos podem ser reagendados.");

        var agora = Tempo.Agora();

        if (ag.DataHora <= agora)
            throw new NegocioException("Este agendamento ja passou e nao pode ser reagendado.");

        var novaData = Tempo.Normalizar(dto.DataHora);
        if (novaData <= agora)
            throw new NegocioException("A nova data nao pode estar no passado.");

        var novoBarbeiroId = dto.BarbeiroId ?? ag.BarbeiroId;

        var novoBarbeiro = await _db.Barbeiros.FirstOrDefaultAsync(b => b.Id == novoBarbeiroId);
        if (novoBarbeiro == null || !novoBarbeiro.Ativo)
            throw new NegocioException("Esta profissional nao esta disponivel.");

        await _disponibilidade.GarantirLivre(novoBarbeiroId, novaData, ag.DuracaoMinutos, ag.Id);

        var barbeiroAntigoId = ag.BarbeiroId;
        var dataAntiga = ag.DataHora;
        var barbeiroAntigoUsuarioId = await UsuarioDoBarbeiro(barbeiroAntigoId);
        var nomeCliente = ag.Cliente?.Nome ?? "O cliente";

        if (novoBarbeiroId != ag.BarbeiroId)
        {
            ag.BarbeiroId = novoBarbeiroId;
            ag.PercentualBarbeiro = novoBarbeiro.PercentualBarbeiro;
            ag.ValorBarbeiro = Math.Round(ag.ValorServico * novoBarbeiro.PercentualBarbeiro / 100m, 2);
            ag.ValorAdministrador = ag.ValorFinal - ag.ValorBarbeiro;

            _notificacoes.Adicionar(
                barbeiroAntigoUsuarioId,
                "Agendamento transferido",
                nomeCliente + " trocou o agendamento de " + Quando(dataAntiga) + " para outra profissional.");
        }

        ag.DataHora = novaData;

        _notificacoes.Adicionar(
            novoBarbeiro.UsuarioId,
            "Agendamento reagendado",
            nomeCliente + " agendou para " + Quando(novaData) + " (antes: " + Quando(dataAntiga) + ").");

        await _notificacoes.AvisarListaEspera(barbeiroAntigoId, dataAntiga);

        await SalvarComConflito();

        return await ObterDto(ag.Id);
    }

    public async Task<AgendamentoRespostaDTO> Cancelar(ClaimsPrincipal user, int id)
    {
        var ag = await _db.Agendamentos.Include(a => a.Cliente).FirstOrDefaultAsync(a => a.Id == id);

        var ehDono = user.EhCliente() && ag != null && ag.ClienteId == user.IdUsuario();
        var ehBarbeiro = user.EhBarbeiro() && ag != null && user.IdBarbeiro() == ag.BarbeiroId;

        if (ag == null || !(user.EhAdmin() || ehDono || ehBarbeiro))
            throw new NegocioException("Agendamento nao encontrado.", 404);

        if (ag.Status != "Agendado")
            throw new NegocioException("Este agendamento nao pode mais ser cancelado.");

        var agora = Tempo.Agora();

        if (ehDono && ag.DataHora <= agora)
            throw new NegocioException("Nao e possivel cancelar um agendamento que ja passou.");

        ag.Status = "Cancelado";

        // Devolve o uso do cupom.
        if (ag.CupomId.HasValue)
        {
            var cupomId = ag.CupomId.Value;
            var cupom = await _db.Cupons.FirstOrDefaultAsync(c => c.Id == cupomId);

            if (cupom != null && cupom.UsosAtuais > 0)
                cupom.UsosAtuais--;
        }

        var nomeCliente = ag.Cliente?.Nome ?? "O cliente";
        var quando = Quando(ag.DataHora);

        if (ehDono)
        {
            var usuarioBarbeiro = await UsuarioDoBarbeiro(ag.BarbeiroId);
            _notificacoes.Adicionar(usuarioBarbeiro, "Agendamento cancelado", nomeCliente + " cancelou o horario de " + quando + ".");
        }
        else
        {
            _notificacoes.Adicionar(ag.ClienteId, "Agendamento cancelado", "Seu horario de " + quando + " foi cancelado pelo salao.");
        }

        if (ag.DataHora > agora)
            await _notificacoes.AvisarListaEspera(ag.BarbeiroId, ag.DataHora);

        await _db.SaveChangesAsync();

        return await ObterDto(ag.Id);
    }

    // Barbeiro (do proprio agendamento) ou administrador marca como Concluido ou Faltou.
    public async Task<AgendamentoRespostaDTO> AtualizarStatus(ClaimsPrincipal user, int id, StatusDTO dto)
    {
        var novo = (dto.Status ?? "").Trim();

        if (novo != "Concluido" && novo != "Faltou")
            throw new NegocioException("Status invalido. Use Concluido ou Faltou.");

        var ag = await _db.Agendamentos.FirstOrDefaultAsync(a => a.Id == id);

        var ehBarbeiro = user.EhBarbeiro() && ag != null && user.IdBarbeiro() == ag.BarbeiroId;

        if (ag == null || !(user.EhAdmin() || ehBarbeiro))
            throw new NegocioException("Agendamento nao encontrado.", 404);

        if (ag.Status != "Agendado")
            throw new NegocioException("Este agendamento ja esta " + ag.Status + ".");

        if (novo == "Faltou" && ag.DataHora > Tempo.Agora())
            throw new NegocioException("So e possivel registrar falta depois do horario do agendamento.");

        ag.Status = novo;

        if (novo == "Concluido")
        {
            _notificacoes.Adicionar(ag.ClienteId, "Atendimento concluido", "Conte como foi! Avalie seu atendimento em \"Meus agendamentos\".");
        }
        else
        {
            _notificacoes.Adicionar(ag.ClienteId, "Falta registrada", "Registramos que voce nao compareceu ao horario de " + Quando(ag.DataHora) + ".");
        }

        await _db.SaveChangesAsync();

        return await ObterDto(ag.Id);
    }

    public async Task Remover(int id)
    {
        var ag = await _db.Agendamentos.FirstOrDefaultAsync(a => a.Id == id);
        if (ag == null)
            throw new NegocioException("Agendamento nao encontrado.", 404);

        _db.Agendamentos.Remove(ag);
        await _db.SaveChangesAsync();
    }

    // ---------- AUXILIARES ----------

    private static IQueryable<Agendamento> ComDetalhes(IQueryable<Agendamento> q)
    {
        return q
            .Include(a => a.Cliente)
            .Include(a => a.Barbeiro!)
                .ThenInclude(b => b.Usuario)
            .Include(a => a.Itens)
            .Include(a => a.Avaliacao);
    }

    private async Task<AgendamentoRespostaDTO> ObterDto(int id)
    {
        var ag = await ComDetalhes(_db.Agendamentos.AsNoTracking()).FirstAsync(a => a.Id == id);
        return Mapear(ag);
    }

    private static bool PodeVer(ClaimsPrincipal user, Agendamento ag)
    {
        if (user.EhAdmin())
            return true;

        if (user.EhCliente())
            return ag.ClienteId == user.IdUsuario();

        if (user.EhBarbeiro())
            return user.IdBarbeiro() == ag.BarbeiroId;

        return false;
    }

    public static AgendamentoRespostaDTO Mapear(Agendamento a)
    {
        var itens = a.Itens
            .OrderBy(i => i.Id)
            .Select(i => new ServicoItemDTO
            {
                ServicoId = i.ServicoId,
                Nome = i.Nome,
                Preco = i.Preco,
                DuracaoMinutos = i.DuracaoMinutos
            })
            .ToList();

        return new AgendamentoRespostaDTO
        {
            Id = a.Id,
            DataHora = a.DataHora,
            Fim = a.DataHora.AddMinutes(a.DuracaoMinutos),
            DuracaoMinutos = a.DuracaoMinutos,
            Status = a.Status,
            ClienteId = a.ClienteId,
            NomeCliente = a.Cliente?.Nome ?? "",
            FotoCliente = a.Cliente?.FotoUrl,
            BarbeiroId = a.BarbeiroId,
            NomeBarbeiro = a.Barbeiro?.Usuario?.Nome ?? "",
            FotoBarbeiro = a.Barbeiro?.Usuario?.FotoUrl,
            Servicos = itens,
            NomeServico = string.Join(" + ", itens.Select(i => i.Nome)),
            ValorServico = a.ValorServico,
            ValorDesconto = a.ValorDesconto,
            ValorFinal = a.ValorFinal,
            PercentualBarbeiro = a.PercentualBarbeiro,
            ValorBarbeiro = a.ValorBarbeiro,
            ValorAdministrador = a.ValorAdministrador,
            CupomCodigo = a.CupomCodigo,
            UsouFidelidade = a.UsouFidelidade,
            Avaliacao = a.Avaliacao == null
                ? null
                : new AvaliacaoResumoDTO { Nota = a.Avaliacao.Nota, Comentario = a.Avaliacao.Comentario }
        };
    }

    private async Task<List<Servico>> CarregarServicos(List<int>? ids)
    {
        var distintos = (ids ?? new List<int>()).Distinct().ToList();

        if (distintos.Count == 0)
            throw new NegocioException("Escolha pelo menos um servico.");

        if (distintos.Count > 6)
            throw new NegocioException("Escolha no maximo 6 servicos por agendamento.");

        var encontrados = await _db.Servicos
            .Where(s => distintos.Contains(s.Id) && s.Ativo)
            .ToListAsync();

        if (encontrados.Count != distintos.Count)
            throw new NegocioException("Um dos servicos escolhidos nao esta mais disponivel.");

        // Mantem a ordem em que o cliente escolheu.
        return distintos.Select(id => encontrados.First(s => s.Id == id)).ToList();
    }

    private class DescontoAplicado
    {
        public decimal Valor { get; set; }
        public Cupom? Cupom { get; set; }
        public bool Fidelidade { get; set; }
    }

    private async Task<DescontoAplicado> CalcularDesconto(int clienteId, decimal total, string? codigo, bool usarFidelidade)
    {
        var resultado = new DescontoAplicado();
        var temCupom = !string.IsNullOrWhiteSpace(codigo);

        if (temCupom && usarFidelidade)
            throw new NegocioException("Use um cupom OU o servico gratis da fidelidade, nao os dois ao mesmo tempo.");

        if (usarFidelidade)
        {
            var resumo = await _fidelidade.Resumo(clienteId);

            if (resumo.GratisDisponiveis <= 0)
                throw new NegocioException("Voce ainda nao tem um servico gratis disponivel.");

            resultado.Valor = total;
            resultado.Fidelidade = true;
        }
        else if (temCupom)
        {
            var aplicado = await _cupons.Aplicar(codigo, total);

            resultado.Cupom = aplicado.Cupom;
            resultado.Valor = aplicado.Desconto;
        }

        return resultado;
    }

    private async Task<int> UsuarioDoBarbeiro(int barbeiroId)
    {
        return await _db.Barbeiros
            .AsNoTracking()
            .Where(b => b.Id == barbeiroId)
            .Select(b => b.UsuarioId)
            .FirstAsync();
    }

    private static string Quando(DateTime data)
    {
        return data.ToString("dd/MM") + " as " + data.ToString("HH:mm");
    }

    // O indice unico (barbeiro + horario) garante que dois clientes nao fechem o mesmo horario ao mesmo tempo.
    private async Task SalvarComConflito()
    {
        try
        {
            await _db.SaveChangesAsync();
        }
        catch (DbUpdateException ex)
        {
            var detalhe = ex.InnerException?.Message ?? "";

            if (detalhe.Contains("ux_agendamento_barbeiro_hora"))
                throw new NegocioException("Esse horario acabou de ser ocupado. Escolha outro horario.", 409);

            throw;
        }
    }
}
