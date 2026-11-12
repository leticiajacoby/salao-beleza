using Microsoft.EntityFrameworkCore;
using SalaoBeleza.Common;
using SalaoBeleza.Data;
using SalaoBeleza.DTOs;
using SalaoBeleza.Models;

namespace SalaoBeleza.Services;

public class ServicoService
{
    private readonly AppDbContext _db;

    public ServicoService(AppDbContext db)
    {
        _db = db;
    }

    // Clientes so veem servicos ativos. O admin pode pedir todos.
    public async Task<List<ServicoRespostaDTO>> Listar(bool incluirInativos)
    {
        var q = _db.Servicos.AsNoTracking().AsQueryable();

        if (!incluirInativos)
            q = q.Where(s => s.Ativo);

        var lista = await q.OrderBy(s => s.Nome).ToListAsync();

        return lista.Select(ParaDTO).ToList();
    }

    public async Task<ServicoRespostaDTO?> BuscarPorId(int id)
    {
        var servico = await _db.Servicos.AsNoTracking().FirstOrDefaultAsync(s => s.Id == id);
        return servico == null ? null : ParaDTO(servico);
    }

    public async Task<ServicoRespostaDTO> Criar(ServicoSalvarDTO dto)
    {
        Validar(dto);

        var servico = new Servico
        {
            Nome = dto.Nome.Trim(),
            Preco = dto.Preco,
            DuracaoMinutos = dto.DuracaoMinutos,
            Ativo = true
        };

        _db.Servicos.Add(servico);
        await _db.SaveChangesAsync();

        return ParaDTO(servico);
    }

    public async Task<ServicoRespostaDTO> Editar(int id, ServicoSalvarDTO dto)
    {
        Validar(dto);

        var servico = await _db.Servicos.FirstOrDefaultAsync(s => s.Id == id);
        if (servico == null)
            throw new NegocioException("Servico nao encontrado.", 404);

        servico.Nome = dto.Nome.Trim();
        servico.Preco = dto.Preco;
        servico.DuracaoMinutos = dto.DuracaoMinutos;

        await _db.SaveChangesAsync();

        return ParaDTO(servico);
    }

    public async Task<ServicoRespostaDTO> DefinirAtivo(int id, bool ativo)
    {
        var servico = await _db.Servicos.FirstOrDefaultAsync(s => s.Id == id);
        if (servico == null)
            throw new NegocioException("Servico nao encontrado.", 404);

        servico.Ativo = ativo;
        await _db.SaveChangesAsync();

        return ParaDTO(servico);
    }

    public async Task Remover(int id)
    {
        var servico = await _db.Servicos.FirstOrDefaultAsync(s => s.Id == id);
        if (servico == null)
            throw new NegocioException("Servico nao encontrado.", 404);

        var usado = await _db.Agendamentos.AnyAsync(a => a.ServicoId == id)
                    || await _db.AgendamentoServicos.AnyAsync(i => i.ServicoId == id);

        if (usado)
            throw new NegocioException("Este servico ja foi usado em agendamentos. Desative-o em vez de excluir para manter o historico.", 409);

        _db.Servicos.Remove(servico);
        await _db.SaveChangesAsync();
    }

    private static void Validar(ServicoSalvarDTO dto)
    {
        var nome = (dto.Nome ?? "").Trim();

        if (nome.Length < 2 || nome.Length > 150)
            throw new NegocioException("Informe o nome do servico (ate 150 caracteres).");

        if (dto.Preco < 0 || dto.Preco > 100000)
            throw new NegocioException("Informe um preco valido.");

        if (dto.DuracaoMinutos < 5 || dto.DuracaoMinutos > 480)
            throw new NegocioException("A duracao deve ficar entre 5 e 480 minutos.");
    }

    private static ServicoRespostaDTO ParaDTO(Servico s)
    {
        return new ServicoRespostaDTO
        {
            Id = s.Id,
            Nome = s.Nome,
            Preco = s.Preco,
            DuracaoMinutos = s.DuracaoMinutos,
            Ativo = s.Ativo
        };
    }
}
