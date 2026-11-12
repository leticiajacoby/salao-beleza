using Microsoft.EntityFrameworkCore;
using SalaoBeleza.Common;
using SalaoBeleza.Data;
using SalaoBeleza.DTOs;
using SalaoBeleza.Models;

namespace SalaoBeleza.Services;

public class AvaliacaoService
{
    private readonly AppDbContext _db;
    private readonly NotificacaoService _notificacoes;

    public AvaliacaoService(AppDbContext db, NotificacaoService notificacoes)
    {
        _db = db;
        _notificacoes = notificacoes;
    }

    public async Task<AvaliacaoResumoDTO> Criar(int clienteId, AvaliarDTO dto)
    {
        if (dto.Nota < 1 || dto.Nota > 5)
            throw new NegocioException("A nota deve ser de 1 a 5 estrelas.");

        var comentario = (dto.Comentario ?? "").Trim();
        if (comentario.Length > 500)
            throw new NegocioException("O comentario pode ter no maximo 500 caracteres.");

        var ag = await _db.Agendamentos
            .Include(a => a.Cliente)
            .FirstOrDefaultAsync(a => a.Id == dto.AgendamentoId && a.ClienteId == clienteId);

        if (ag == null)
            throw new NegocioException("Agendamento nao encontrado.", 404);

        if (ag.Status != "Concluido")
            throw new NegocioException("Voce so pode avaliar atendimentos concluidos.");

        if (await _db.Avaliacoes.AnyAsync(v => v.AgendamentoId == ag.Id))
            throw new NegocioException("Este atendimento ja foi avaliado.", 409);

        _db.Avaliacoes.Add(new Avaliacao
        {
            AgendamentoId = ag.Id,
            ClienteId = clienteId,
            BarbeiroId = ag.BarbeiroId,
            Nota = dto.Nota,
            Comentario = comentario,
            CriadoEm = Tempo.Agora()
        });

        var usuarioBarbeiro = await _db.Barbeiros
            .AsNoTracking()
            .Where(b => b.Id == ag.BarbeiroId)
            .Select(b => b.UsuarioId)
            .FirstAsync();

        _notificacoes.Adicionar(
            usuarioBarbeiro,
            "Nova avaliacao",
            (ag.Cliente?.Nome ?? "Um cliente") + " avaliou seu atendimento com " + dto.Nota + " estrela(s).");

        await _db.SaveChangesAsync();

        return new AvaliacaoResumoDTO { Nota = dto.Nota, Comentario = comentario };
    }
}
