using System.Globalization;
using Microsoft.EntityFrameworkCore;
using SalaoBeleza.Common;
using SalaoBeleza.Data;
using SalaoBeleza.DTOs;

namespace SalaoBeleza.Services;

// Numeros do painel do administrador e ganhos do barbeiro.
// Faturamento = somente atendimentos "Concluido" (valor final pago pelo cliente).
public class RelatorioService
{
    private readonly AppDbContext _db;
    private readonly BarbeiroService _barbeiros;

    public RelatorioService(AppDbContext db, BarbeiroService barbeiros)
    {
        _db = db;
        _barbeiros = barbeiros;
    }

    public async Task<GanhosDTO> Ganhos(int barbeiroId, string? mes)
    {
        var existe = await _db.Barbeiros.AnyAsync(b => b.Id == barbeiroId);
        if (!existe)
            throw new NegocioException("Profissional nao encontrada.", 404);

        var agora = Tempo.Agora();
        var primeiroDia = new DateTime(agora.Year, agora.Month, 1, 0, 0, 0, DateTimeKind.Unspecified);

        if (!string.IsNullOrWhiteSpace(mes))
        {
            if (!DateTime.TryParseExact(mes.Trim() + "-01", "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out var lido))
                throw new NegocioException("Mes invalido. Use o formato AAAA-MM.");

            primeiroDia = new DateTime(lido.Year, lido.Month, 1, 0, 0, 0, DateTimeKind.Unspecified);
        }

        var proximoMes = primeiroDia.AddMonths(1);

        var lista = await _db.Agendamentos
            .AsNoTracking()
            .Where(a => a.BarbeiroId == barbeiroId && a.DataHora >= primeiroDia && a.DataHora < proximoMes)
            .Select(a => new { a.Status, a.ValorBarbeiro })
            .ToListAsync();

        return new GanhosDTO
        {
            Mes = primeiroDia.ToString("yyyy-MM", CultureInfo.InvariantCulture),
            Concluidos = lista.Count(a => a.Status == "Concluido"),
            Faltas = lista.Count(a => a.Status == "Faltou"),
            Cancelados = lista.Count(a => a.Status == "Cancelado"),
            Agendados = lista.Count(a => a.Status == "Agendado"),
            TotalRecebido = lista.Where(a => a.Status == "Concluido").Sum(a => a.ValorBarbeiro),
            TotalPrevisto = lista.Where(a => a.Status == "Agendado").Sum(a => a.ValorBarbeiro)
        };
    }

    public async Task<DashboardDTO> Dashboard(int meses)
    {
        if (meses < 1) meses = 1;
        if (meses > 24) meses = 24;

        var agora = Tempo.Agora();
        var hoje = agora.Date;
        var inicioMes = new DateTime(hoje.Year, hoje.Month, 1, 0, 0, 0, DateTimeKind.Unspecified);
        var mesAnterior = inicioMes.AddMonths(-1);
        var proximoMes = inicioMes.AddMonths(1);
        var desde = inicioMes.AddMonths(-(meses - 1));

        var periodo = await _db.Agendamentos
            .AsNoTracking()
            .Include(a => a.Itens)
            .Where(a => a.DataHora >= desde && a.Status != "Cancelado")
            .ToListAsync();

        var concluidos = periodo.Where(a => a.Status == "Concluido").ToList();

        var doMes = concluidos.Where(a => a.DataHora >= inicioMes && a.DataHora < proximoMes).ToList();
        var doMesAnterior = concluidos.Where(a => a.DataHora >= mesAnterior && a.DataHora < inicioMes).ToList();

        var faturamentoMes = doMes.Sum(a => a.ValorFinal);

        var resposta = new DashboardDTO
        {
            FaturamentoMes = faturamentoMes,
            FaturamentoMesAnterior = doMesAnterior.Sum(a => a.ValorFinal),
            AtendimentosMes = doMes.Count,
            TicketMedio = doMes.Count > 0 ? Math.Round(faturamentoMes / doMes.Count, 2) : 0,
            AgendamentosHoje = periodo.Count(a => a.DataHora.Date == hoje),
            AgendamentosFuturos = periodo.Count(a => a.Status == "Agendado" && a.DataHora > agora),
            Faltas = await _db.Agendamentos.CountAsync(a => a.Status == "Faltou" && a.DataHora >= inicioMes && a.DataHora < proximoMes),
            TotalClientes = await _db.Usuarios.CountAsync(u => u.Tipo == "Cliente")
        };

        // Faturamento mes a mes (inclui meses sem movimento).
        for (var i = 0; i < meses; i++)
        {
            var ini = desde.AddMonths(i);
            var fim = ini.AddMonths(1);
            var doPeriodo = concluidos.Where(a => a.DataHora >= ini && a.DataHora < fim).ToList();

            resposta.FaturamentoPorMes.Add(new MesValorDTO
            {
                Mes = ini.ToString("yyyy-MM", CultureInfo.InvariantCulture),
                Faturamento = doPeriodo.Sum(a => a.ValorFinal),
                Atendimentos = doPeriodo.Count
            });
        }

        // Servicos mais pedidos (concluidos + agendados do periodo).
        resposta.ServicosMaisPedidos = periodo
            .SelectMany(a => a.Itens)
            .GroupBy(i => i.Nome)
            .Select(g => new ServicoRankingDTO
            {
                Nome = g.Key,
                Quantidade = g.Count(),
                Faturamento = g.Sum(i => i.Preco)
            })
            .OrderByDescending(s => s.Quantidade)
            .ThenBy(s => s.Nome)
            .Take(8)
            .ToList();

        // Ranking de barbeiros (so atendimentos concluidos).
        var todosBarbeiros = await _db.Barbeiros.AsNoTracking().Include(b => b.Usuario).ToListAsync();
        var medias = await _barbeiros.Medias();

        resposta.RankingBarbeiros = todosBarbeiros
            .Select(b =>
            {
                var dele = concluidos.Where(a => a.BarbeiroId == b.Id).ToList();
                var tem = medias.TryGetValue(b.Id, out var m);

                return new BarbeiroRankingDTO
                {
                    BarbeiroId = b.Id,
                    Nome = b.Usuario?.Nome ?? "",
                    FotoUrl = b.Usuario?.FotoUrl,
                    Atendimentos = dele.Count,
                    Faturamento = dele.Sum(a => a.ValorFinal),
                    Comissao = dele.Sum(a => a.ValorBarbeiro),
                    Media = tem ? m.Media : 0
                };
            })
            .OrderByDescending(r => r.Faturamento)
            .ThenByDescending(r => r.Atendimentos)
            .ToList();

        // Horarios de pico (hora de inicio dos atendimentos do periodo).
        var porHora = periodo
            .GroupBy(a => a.DataHora.Hour)
            .ToDictionary(g => g.Key, g => g.Count());

        for (var hora = 7; hora <= 21; hora++)
        {
            resposta.HorariosPico.Add(new HoraPicoDTO
            {
                Hora = hora,
                Quantidade = porHora.ContainsKey(hora) ? porHora[hora] : 0
            });
        }

        return resposta;
    }
}
