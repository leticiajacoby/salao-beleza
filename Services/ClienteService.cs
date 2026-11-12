using Microsoft.EntityFrameworkCore;
using SalaoBeleza.Common;
using SalaoBeleza.Data;
using SalaoBeleza.DTOs;

namespace SalaoBeleza.Services;

// Clientes sao usuarios do tipo "Cliente" (eles mesmos se cadastram na tela de login).
public class ClienteService
{
    private readonly AppDbContext _db;
    private readonly ArquivoService _arquivos;

    public ClienteService(AppDbContext db, ArquivoService arquivos)
    {
        _db = db;
        _arquivos = arquivos;
    }

    public async Task<List<ClienteListaDTO>> Listar()
    {
        var clientes = await _db.Usuarios
            .AsNoTracking()
            .Where(u => u.Tipo == "Cliente")
            .OrderBy(u => u.Nome)
            .ToListAsync();

        var donos = await _db.Agendamentos.AsNoTracking().Select(a => a.ClienteId).ToListAsync();

        var totais = donos
            .GroupBy(id => id)
            .ToDictionary(g => g.Key, g => g.Count());

        return clientes.Select(c => new ClienteListaDTO
        {
            Id = c.Id,
            Nome = c.Nome,
            Email = c.Email,
            FotoUrl = c.FotoUrl,
            TotalAgendamentos = totais.ContainsKey(c.Id) ? totais[c.Id] : 0
        }).ToList();
    }

    public async Task Remover(int id)
    {
        var cliente = await _db.Usuarios.FirstOrDefaultAsync(u => u.Id == id && u.Tipo == "Cliente");
        if (cliente == null)
            throw new NegocioException("Cliente nao encontrado.", 404);

        var temHistorico = await _db.Agendamentos.AnyAsync(a => a.ClienteId == id);
        if (temHistorico)
            throw new NegocioException("Este cliente tem agendamentos no historico e nao pode ser excluido.", 409);

        var foto = cliente.FotoUrl;

        _db.Usuarios.Remove(cliente);
        await _db.SaveChangesAsync();

        _arquivos.Remover(foto);
    }
}
