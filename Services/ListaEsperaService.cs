using System.Security.Claims;
using Microsoft.EntityFrameworkCore;
using SalaoBeleza.Common;
using SalaoBeleza.Data;
using SalaoBeleza.DTOs;
using SalaoBeleza.Models;

namespace SalaoBeleza.Services;

public class ListaEsperaService
{
    private readonly AppDbContext _db;

    public ListaEsperaService(AppDbContext db)
    {
        _db = db;
    }

    public async Task<List<ListaEsperaRespostaDTO>> Listar(ClaimsPrincipal user)
    {
        var q = _db.ListasEspera
            .AsNoTracking()
            .Include(l => l.Cliente)
            .Include(l => l.Barbeiro!)
                .ThenInclude(b => b.Usuario)
            .AsQueryable();

        if (!user.EhAdmin())
        {
            var clienteId = user.IdUsuario();
            q = q.Where(l => l.ClienteId == clienteId);
        }

        var hoje = Tempo.Agora().Date;
        q = q.Where(l => l.Data >= hoje);

        var lista = await q.OrderBy(l => l.Data).ToListAsync();

        var nomesServicos = await _db.Servicos.AsNoTracking().ToDictionaryAsync(s => s.Id, s => s.Nome);

        return lista.Select(l =>
        {
            var ids = LerIds(l.ServicoIds);

            return new ListaEsperaRespostaDTO
            {
                Id = l.Id,
                BarbeiroId = l.BarbeiroId,
                NomeBarbeiro = l.Barbeiro?.Usuario?.Nome ?? "",
                ClienteId = l.ClienteId,
                NomeCliente = l.Cliente?.Nome ?? "",
                Data = l.Data,
                ServicoIds = ids,
                Servicos = string.Join(" + ", ids.Where(i => nomesServicos.ContainsKey(i)).Select(i => nomesServicos[i])),
                Status = l.Status
            };
        }).ToList();
    }

    public async Task<ListaEsperaRespostaDTO> Criar(int clienteId, ListaEsperaCriarDTO dto)
    {
        var data = Tempo.SemKind(dto.Data.Date);

        if (data < Tempo.Agora().Date)
            throw new NegocioException("Escolha uma data a partir de hoje.");

        var barbeiro = await _db.Barbeiros
            .Include(b => b.Usuario)
            .FirstOrDefaultAsync(b => b.Id == dto.BarbeiroId);

        if (barbeiro == null || !barbeiro.Ativo)
            throw new NegocioException("Profissional nao encontrada.", 404);

        var ids = (dto.ServicoIds ?? new List<int>()).Distinct().ToList();

        if (ids.Count > 0)
        {
            var validos = await _db.Servicos.CountAsync(s => ids.Contains(s.Id));
            if (validos != ids.Count)
                throw new NegocioException("Servico invalido.");
        }

        var jaExiste = await _db.ListasEspera.AnyAsync(l =>
            l.ClienteId == clienteId && l.BarbeiroId == dto.BarbeiroId && l.Data == data && l.Status == "Aguardando");

        if (jaExiste)
            throw new NegocioException("Voce ja esta na lista de espera dessa profissional para esse dia.", 409);

        var item = new ListaEspera
        {
            ClienteId = clienteId,
            BarbeiroId = dto.BarbeiroId,
            Data = data,
            ServicoIds = string.Join(",", ids),
            Status = "Aguardando",
            CriadoEm = Tempo.Agora()
        };

        _db.ListasEspera.Add(item);
        await _db.SaveChangesAsync();

        return new ListaEsperaRespostaDTO
        {
            Id = item.Id,
            BarbeiroId = item.BarbeiroId,
            NomeBarbeiro = barbeiro.Usuario?.Nome ?? "",
            ClienteId = clienteId,
            Data = item.Data,
            ServicoIds = ids,
            Status = item.Status
        };
    }

    public async Task Remover(ClaimsPrincipal user, int id)
    {
        var item = await _db.ListasEspera.FirstOrDefaultAsync(l => l.Id == id);

        if (item == null || !(user.EhAdmin() || item.ClienteId == user.IdUsuario()))
            throw new NegocioException("Item nao encontrado.", 404);

        _db.ListasEspera.Remove(item);
        await _db.SaveChangesAsync();
    }

    private static List<int> LerIds(string texto)
    {
        var ids = new List<int>();

        foreach (var parte in (texto ?? "").Split(',', StringSplitOptions.RemoveEmptyEntries))
        {
            if (int.TryParse(parte.Trim(), out var id))
                ids.Add(id);
        }

        return ids;
    }
}
