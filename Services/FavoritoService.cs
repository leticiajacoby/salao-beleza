using Microsoft.EntityFrameworkCore;
using SalaoBeleza.Common;
using SalaoBeleza.Data;
using SalaoBeleza.Models;

namespace SalaoBeleza.Services;

public class FavoritoService
{
    private readonly AppDbContext _db;

    public FavoritoService(AppDbContext db)
    {
        _db = db;
    }

    public async Task<List<int>> Listar(int clienteId)
    {
        return await _db.Favoritos
            .AsNoTracking()
            .Where(f => f.ClienteId == clienteId)
            .Select(f => f.BarbeiroId)
            .ToListAsync();
    }

    public async Task Adicionar(int clienteId, int barbeiroId)
    {
        var existe = await _db.Barbeiros.AnyAsync(b => b.Id == barbeiroId);
        if (!existe)
            throw new NegocioException("Profissional nao encontrada.", 404);

        var jaFavorito = await _db.Favoritos.AnyAsync(f => f.ClienteId == clienteId && f.BarbeiroId == barbeiroId);
        if (jaFavorito)
            return;

        _db.Favoritos.Add(new Favorito { ClienteId = clienteId, BarbeiroId = barbeiroId });
        await _db.SaveChangesAsync();
    }

    public async Task Remover(int clienteId, int barbeiroId)
    {
        var favorito = await _db.Favoritos.FirstOrDefaultAsync(f => f.ClienteId == clienteId && f.BarbeiroId == barbeiroId);
        if (favorito == null)
            return;

        _db.Favoritos.Remove(favorito);
        await _db.SaveChangesAsync();
    }
}
