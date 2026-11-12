using System.Globalization;
using Microsoft.EntityFrameworkCore;
using SalaoBeleza.Common;
using SalaoBeleza.Data;
using SalaoBeleza.DTOs;
using SalaoBeleza.Models;

namespace SalaoBeleza.Services;

// Horarios de trabalho, folgas e calculo dos horarios livres de cada barbeiro.
public class DisponibilidadeService
{
    // Os horarios sao oferecidos de 30 em 30 minutos.
    public const int PassoMinutos = 30;

    private readonly AppDbContext _db;

    public DisponibilidadeService(AppDbContext db)
    {
        _db = db;
    }

    // Expediente usado enquanto o barbeiro nao configurar o dele: seg-sab 09:00-18:00, almoco 12:00-13:00.
    public static List<HorarioTrabalho> HorariosPadrao(int barbeiroId)
    {
        var lista = new List<HorarioTrabalho>();

        for (var dia = 0; dia <= 6; dia++)
        {
            lista.Add(new HorarioTrabalho
            {
                BarbeiroId = barbeiroId,
                DiaSemana = dia,
                Trabalha = dia != 0,
                Inicio = new TimeSpan(9, 0, 0),
                Fim = new TimeSpan(18, 0, 0),
                AlmocoInicio = new TimeSpan(12, 0, 0),
                AlmocoFim = new TimeSpan(13, 0, 0)
            });
        }

        return lista;
    }

    public async Task<List<HorarioTrabalho>> ObterHorarios(int barbeiroId)
    {
        var salvos = await _db.HorariosTrabalho
            .AsNoTracking()
            .Where(h => h.BarbeiroId == barbeiroId)
            .OrderBy(h => h.DiaSemana)
            .ToListAsync();

        if (salvos.Count == 0)
            return HorariosPadrao(barbeiroId);

        return salvos;
    }

    public async Task<List<HorarioDTO>> ListarHorarios(int barbeiroId)
    {
        await GarantirBarbeiro(barbeiroId);

        var horarios = await ObterHorarios(barbeiroId);

        return horarios.Select(h => new HorarioDTO
        {
            DiaSemana = h.DiaSemana,
            Trabalha = h.Trabalha,
            Inicio = FormatarHora(h.Inicio),
            Fim = FormatarHora(h.Fim),
            AlmocoInicio = h.AlmocoInicio.HasValue ? FormatarHora(h.AlmocoInicio.Value) : null,
            AlmocoFim = h.AlmocoFim.HasValue ? FormatarHora(h.AlmocoFim.Value) : null
        }).ToList();
    }

    public async Task<List<HorarioDTO>> SalvarHorarios(int barbeiroId, List<HorarioDTO>? dias)
    {
        await GarantirBarbeiro(barbeiroId);

        if (dias == null || dias.Count == 0)
            throw new NegocioException("Envie os horarios dos dias da semana.");

        var novos = new List<HorarioTrabalho>();

        for (var d = 0; d <= 6; d++)
        {
            var dto = dias.FirstOrDefault(x => x.DiaSemana == d);

            var item = new HorarioTrabalho
            {
                BarbeiroId = barbeiroId,
                DiaSemana = d,
                Trabalha = false,
                Inicio = new TimeSpan(9, 0, 0),
                Fim = new TimeSpan(18, 0, 0)
            };

            if (dto != null)
            {
                item.Trabalha = dto.Trabalha;

                if (dto.Trabalha)
                {
                    var inicio = LerHora(dto.Inicio, "inicio do expediente");
                    var fim = LerHora(dto.Fim, "fim do expediente");

                    if (inicio >= fim)
                        throw new NegocioException("O inicio do expediente deve ser antes do fim (" + NomeDia(d) + ").");

                    item.Inicio = inicio;
                    item.Fim = fim;

                    var temAlmocoInicio = !string.IsNullOrWhiteSpace(dto.AlmocoInicio);
                    var temAlmocoFim = !string.IsNullOrWhiteSpace(dto.AlmocoFim);

                    if (temAlmocoInicio != temAlmocoFim)
                        throw new NegocioException("Informe o inicio e o fim do almoco (" + NomeDia(d) + ").");

                    if (temAlmocoInicio)
                    {
                        var ai = LerHora(dto.AlmocoInicio, "inicio do almoco");
                        var af = LerHora(dto.AlmocoFim, "fim do almoco");

                        if (ai >= af || ai < inicio || af > fim)
                            throw new NegocioException("O almoco deve ficar dentro do expediente (" + NomeDia(d) + ").");

                        item.AlmocoInicio = ai;
                        item.AlmocoFim = af;
                    }
                }
            }

            novos.Add(item);
        }

        var antigos = await _db.HorariosTrabalho.Where(h => h.BarbeiroId == barbeiroId).ToListAsync();
        _db.HorariosTrabalho.RemoveRange(antigos);
        _db.HorariosTrabalho.AddRange(novos);

        await _db.SaveChangesAsync();

        return await ListarHorarios(barbeiroId);
    }

    public async Task<List<FolgaRespostaDTO>> ListarFolgas(int barbeiroId)
    {
        await GarantirBarbeiro(barbeiroId);

        var hoje = Tempo.Agora().Date;

        var folgas = await _db.Folgas
            .AsNoTracking()
            .Where(f => f.BarbeiroId == barbeiroId && f.Fim > hoje)
            .OrderBy(f => f.Inicio)
            .ToListAsync();

        return folgas.Select(ParaDTO).ToList();
    }

    public async Task<FolgaCriadaDTO> CriarFolga(int barbeiroId, FolgaCriarDTO dto)
    {
        await GarantirBarbeiro(barbeiroId);

        var inicio = Tempo.SemKind(dto.DataInicio.Date);
        var ultimoDia = Tempo.SemKind(dto.DataFim.Date);

        if (ultimoDia < inicio)
            throw new NegocioException("A data final nao pode ser antes da data inicial.");

        if (ultimoDia < Tempo.Agora().Date)
            throw new NegocioException("Escolha datas a partir de hoje.");

        if ((ultimoDia - inicio).TotalDays > 366)
            throw new NegocioException("O bloqueio pode ter no maximo 1 ano.");

        var motivo = (dto.Motivo ?? "").Trim();
        if (motivo.Length > 150)
            motivo = motivo.Substring(0, 150);

        var fim = ultimoDia.AddDays(1);

        var folga = new Folga
        {
            BarbeiroId = barbeiroId,
            Inicio = inicio,
            Fim = fim,
            Motivo = motivo
        };

        _db.Folgas.Add(folga);
        await _db.SaveChangesAsync();

        var afetados = await _db.Agendamentos.CountAsync(a =>
            a.BarbeiroId == barbeiroId && a.Status == "Agendado" && a.DataHora >= inicio && a.DataHora < fim);

        return new FolgaCriadaDTO
        {
            Folga = ParaDTO(folga),
            AgendamentosAfetados = afetados
        };
    }

    public async Task RemoverFolga(int barbeiroId, int folgaId)
    {
        var folga = await _db.Folgas.FirstOrDefaultAsync(f => f.Id == folgaId && f.BarbeiroId == barbeiroId);
        if (folga == null)
            throw new NegocioException("Bloqueio nao encontrado.", 404);

        _db.Folgas.Remove(folga);
        await _db.SaveChangesAsync();
    }

    // Lista os horarios ("HH:mm") em que cabe um atendimento de "duracao" minutos.
    public async Task<List<string>> HorariosLivres(int barbeiroId, DateTime data, int duracao, int? ignorarAgendamentoId)
    {
        var resultado = new List<string>();

        var dia = Tempo.SemKind(data.Date);
        var agora = Tempo.Agora();

        if (dia < agora.Date || duracao <= 0)
            return resultado;

        var horarios = await ObterHorarios(barbeiroId);
        var expediente = horarios.FirstOrDefault(h => h.DiaSemana == (int)dia.DayOfWeek);

        if (expediente == null || !expediente.Trabalha)
            return resultado;

        var proximoDia = dia.AddDays(1);

        var folgas = await _db.Folgas
            .AsNoTracking()
            .Where(f => f.BarbeiroId == barbeiroId && f.Inicio < proximoDia && f.Fim > dia)
            .ToListAsync();

        var ocupados = await _db.Agendamentos
            .AsNoTracking()
            .Where(a => a.BarbeiroId == barbeiroId
                        && a.DataHora >= dia
                        && a.DataHora < proximoDia
                        && (a.Status == "Agendado" || a.Status == "Concluido")
                        && (!ignorarAgendamentoId.HasValue || a.Id != ignorarAgendamentoId.Value))
            .Select(a => new { a.DataHora, a.DuracaoMinutos })
            .ToListAsync();

        var inicioExpediente = dia + expediente.Inicio;
        var fimExpediente = dia + expediente.Fim;

        for (var s = inicioExpediente; s.AddMinutes(duracao) <= fimExpediente; s = s.AddMinutes(PassoMinutos))
        {
            var e = s.AddMinutes(duracao);

            if (s <= agora)
                continue;

            if (expediente.AlmocoInicio.HasValue && expediente.AlmocoFim.HasValue)
            {
                var almocoInicio = dia + expediente.AlmocoInicio.Value;
                var almocoFim = dia + expediente.AlmocoFim.Value;

                if (s < almocoFim && e > almocoInicio)
                    continue;
            }

            var inicioSlot = s;
            var fimSlot = e;

            if (folgas.Any(f => inicioSlot < f.Fim && fimSlot > f.Inicio))
                continue;

            if (ocupados.Any(o => inicioSlot < o.DataHora.AddMinutes(o.DuracaoMinutos) && fimSlot > o.DataHora))
                continue;

            resultado.Add(s.ToString("HH:mm", CultureInfo.InvariantCulture));
        }

        return resultado;
    }

    public async Task GarantirLivre(int barbeiroId, DateTime inicio, int duracao, int? ignorarAgendamentoId)
    {
        var livres = await HorariosLivres(barbeiroId, inicio.Date, duracao, ignorarAgendamentoId);

        var hora = inicio.ToString("HH:mm", CultureInfo.InvariantCulture);

        if (!livres.Contains(hora))
            throw new NegocioException("Esse horario nao esta disponivel com esta profissional. Escolha outro horario.", 409);
    }

    private async Task GarantirBarbeiro(int barbeiroId)
    {
        var existe = await _db.Barbeiros.AnyAsync(b => b.Id == barbeiroId);
        if (!existe)
            throw new NegocioException("Profissional nao encontrada.", 404);
    }

    private static FolgaRespostaDTO ParaDTO(Folga f)
    {
        return new FolgaRespostaDTO
        {
            Id = f.Id,
            DataInicio = f.Inicio,
            DataFim = f.Fim.AddDays(-1),
            Motivo = f.Motivo
        };
    }

    private static string FormatarHora(TimeSpan hora)
    {
        return hora.Hours.ToString("00", CultureInfo.InvariantCulture) + ":" + hora.Minutes.ToString("00", CultureInfo.InvariantCulture);
    }

    private static TimeSpan LerHora(string? texto, string campo)
    {
        if (!string.IsNullOrWhiteSpace(texto) &&
            TimeSpan.TryParse(texto, CultureInfo.InvariantCulture, out var hora) &&
            hora >= TimeSpan.Zero && hora < TimeSpan.FromHours(24))
        {
            return new TimeSpan(hora.Hours, hora.Minutes, 0);
        }

        throw new NegocioException("Horario invalido: " + campo + ". Use o formato HH:mm.");
    }

    private static string NomeDia(int dia)
    {
        var nomes = new[] { "domingo", "segunda", "terca", "quarta", "quinta", "sexta", "sabado" };
        return nomes[dia];
    }
}
