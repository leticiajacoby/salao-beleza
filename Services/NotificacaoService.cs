using Microsoft.EntityFrameworkCore;
using SalaoBeleza.Common;
using SalaoBeleza.Data;
using SalaoBeleza.DTOs;
using SalaoBeleza.Models;

namespace SalaoBeleza.Services;

public class NotificacaoService
{
    private readonly AppDbContext _db;

    public NotificacaoService(AppDbContext db)
    {
        _db = db;
    }

    // Apenas adiciona ao contexto; quem chamou salva junto com o resto da operacao.
    public void Adicionar(int usuarioId, string titulo, string mensagem)
    {
        _db.Notificacoes.Add(new Notificacao
        {
            UsuarioId = usuarioId,
            Titulo = titulo,
            Mensagem = mensagem.Length > 500 ? mensagem.Substring(0, 500) : mensagem,
            Lida = false,
            CriadoEm = Tempo.Agora()
        });
    }

    // Avisa quem esta na lista de espera do barbeiro naquele dia que abriu uma vaga.
    public async Task AvisarListaEspera(int barbeiroId, DateTime horarioLiberado)
    {
        var dia = horarioLiberado.Date;

        var fila = await _db.ListasEspera
            .Include(l => l.Barbeiro!)
                .ThenInclude(b => b.Usuario)
            .Where(l => l.BarbeiroId == barbeiroId && l.Data == dia && l.Status == "Aguardando")
            .ToListAsync();

        foreach (var item in fila)
        {
            var nomeBarbeiro = item.Barbeiro?.Usuario?.Nome ?? "a profissional";

            Adicionar(
                item.ClienteId,
                "Abriu um horario!",
                "Liberou uma vaga com " + nomeBarbeiro + " em " + horarioLiberado.ToString("dd/MM") + " as " + horarioLiberado.ToString("HH:mm") + ". Corra para agendar!");

            item.Status = "Notificado";
        }
    }

    public async Task<NotificacoesRespostaDTO> Listar(int usuarioId)
    {
        var itens = await _db.Notificacoes
            .AsNoTracking()
            .Where(n => n.UsuarioId == usuarioId)
            .OrderByDescending(n => n.CriadoEm)
            .Take(30)
            .ToListAsync();

        var naoLidas = await _db.Notificacoes.CountAsync(n => n.UsuarioId == usuarioId && !n.Lida);

        return new NotificacoesRespostaDTO
        {
            NaoLidas = naoLidas,
            Itens = itens.Select(n => new NotificacaoDTO
            {
                Id = n.Id,
                Titulo = n.Titulo,
                Mensagem = n.Mensagem,
                Lida = n.Lida,
                CriadoEm = n.CriadoEm
            }).ToList()
        };
    }

    public async Task MarcarLida(int usuarioId, int id)
    {
        var n = await _db.Notificacoes.FirstOrDefaultAsync(x => x.Id == id && x.UsuarioId == usuarioId);
        if (n == null)
            return;

        n.Lida = true;
        await _db.SaveChangesAsync();
    }

    public async Task MarcarTodasLidas(int usuarioId)
    {
        var pendentes = await _db.Notificacoes.Where(n => n.UsuarioId == usuarioId && !n.Lida).ToListAsync();

        foreach (var n in pendentes)
            n.Lida = true;

        await _db.SaveChangesAsync();
    }
}
