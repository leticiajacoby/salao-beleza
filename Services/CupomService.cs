using System.Text.RegularExpressions;
using Microsoft.EntityFrameworkCore;
using SalaoBeleza.Common;
using SalaoBeleza.Data;
using SalaoBeleza.DTOs;
using SalaoBeleza.Models;

namespace SalaoBeleza.Services;

public class CupomService
{
    private readonly AppDbContext _db;

    public CupomService(AppDbContext db)
    {
        _db = db;
    }

    // Confere o cupom e calcula o desconto sobre "valor". Lanca NegocioException se nao puder usar.
    public async Task<(Cupom Cupom, decimal Desconto)> Aplicar(string? codigoBruto, decimal valor)
    {
        var codigo = (codigoBruto ?? "").Trim().ToUpperInvariant();

        var cupom = await _db.Cupons.FirstOrDefaultAsync(c => c.Codigo == codigo);

        if (cupom == null || !cupom.Ativo)
            throw new NegocioException("Cupom invalido.");

        if (cupom.Validade.HasValue && cupom.Validade.Value.Date < Tempo.Agora().Date)
            throw new NegocioException("Este cupom expirou.");

        if (cupom.UsosMaximos.HasValue && cupom.UsosAtuais >= cupom.UsosMaximos.Value)
            throw new NegocioException("Este cupom atingiu o limite de usos.");

        decimal desconto;

        if (cupom.Tipo == "Percentual")
            desconto = Math.Round(valor * cupom.Valor / 100m, 2);
        else
            desconto = cupom.Valor;

        if (desconto > valor)
            desconto = valor;

        return (cupom, desconto);
    }

    public async Task<CupomValidacaoDTO> Validar(string? codigo, decimal valor)
    {
        try
        {
            var (cupom, desconto) = await Aplicar(codigo, valor);

            return new CupomValidacaoDTO
            {
                Valido = true,
                Mensagem = "Cupom aplicado!",
                Codigo = cupom.Codigo,
                Tipo = cupom.Tipo,
                Valor = cupom.Valor,
                Desconto = desconto
            };
        }
        catch (NegocioException ex)
        {
            return new CupomValidacaoDTO { Valido = false, Mensagem = ex.Message };
        }
    }

    public async Task<List<CupomRespostaDTO>> Listar()
    {
        var lista = await _db.Cupons.AsNoTracking().OrderByDescending(c => c.Id).ToListAsync();
        return lista.Select(ParaDTO).ToList();
    }

    public async Task<CupomRespostaDTO> Criar(CupomSalvarDTO dto)
    {
        var codigo = ValidarDados(dto);

        if (await _db.Cupons.AnyAsync(c => c.Codigo == codigo))
            throw new NegocioException("Ja existe um cupom com esse codigo.", 409);

        var cupom = new Cupom
        {
            Codigo = codigo,
            Tipo = dto.Tipo,
            Valor = dto.Valor,
            Validade = dto.Validade.HasValue ? Tempo.SemKind(dto.Validade.Value.Date) : null,
            UsosMaximos = dto.UsosMaximos,
            UsosAtuais = 0,
            Ativo = true
        };

        _db.Cupons.Add(cupom);
        await _db.SaveChangesAsync();

        return ParaDTO(cupom);
    }

    public async Task<CupomRespostaDTO> Editar(int id, CupomSalvarDTO dto)
    {
        var codigo = ValidarDados(dto);

        var cupom = await _db.Cupons.FirstOrDefaultAsync(c => c.Id == id);
        if (cupom == null)
            throw new NegocioException("Cupom nao encontrado.", 404);

        if (await _db.Cupons.AnyAsync(c => c.Codigo == codigo && c.Id != id))
            throw new NegocioException("Ja existe um cupom com esse codigo.", 409);

        cupom.Codigo = codigo;
        cupom.Tipo = dto.Tipo;
        cupom.Valor = dto.Valor;
        cupom.Validade = dto.Validade.HasValue ? Tempo.SemKind(dto.Validade.Value.Date) : null;
        cupom.UsosMaximos = dto.UsosMaximos;

        await _db.SaveChangesAsync();

        return ParaDTO(cupom);
    }

    public async Task<CupomRespostaDTO> DefinirAtivo(int id, bool ativo)
    {
        var cupom = await _db.Cupons.FirstOrDefaultAsync(c => c.Id == id);
        if (cupom == null)
            throw new NegocioException("Cupom nao encontrado.", 404);

        cupom.Ativo = ativo;
        await _db.SaveChangesAsync();

        return ParaDTO(cupom);
    }

    public async Task Remover(int id)
    {
        var cupom = await _db.Cupons.FirstOrDefaultAsync(c => c.Id == id);
        if (cupom == null)
            throw new NegocioException("Cupom nao encontrado.", 404);

        if (cupom.UsosAtuais > 0)
            throw new NegocioException("Este cupom ja foi usado em agendamentos. Desative-o em vez de excluir.", 409);

        _db.Cupons.Remove(cupom);
        await _db.SaveChangesAsync();
    }

    private static string ValidarDados(CupomSalvarDTO dto)
    {
        var codigo = (dto.Codigo ?? "").Trim().ToUpperInvariant();

        if (!Regex.IsMatch(codigo, "^[A-Z0-9_-]{3,40}$"))
            throw new NegocioException("O codigo deve ter de 3 a 40 caracteres (letras, numeros, - ou _).");

        if (dto.Tipo != "Percentual" && dto.Tipo != "Valor")
            throw new NegocioException("O tipo do cupom deve ser Percentual ou Valor.");

        if (dto.Valor <= 0)
            throw new NegocioException("O valor do desconto deve ser maior que zero.");

        if (dto.Tipo == "Percentual" && dto.Valor > 100)
            throw new NegocioException("O desconto percentual nao pode passar de 100%.");

        if (dto.UsosMaximos.HasValue && dto.UsosMaximos.Value < 1)
            throw new NegocioException("O limite de usos deve ser de pelo menos 1.");

        return codigo;
    }

    private static CupomRespostaDTO ParaDTO(Cupom c)
    {
        return new CupomRespostaDTO
        {
            Id = c.Id,
            Codigo = c.Codigo,
            Tipo = c.Tipo,
            Valor = c.Valor,
            Validade = c.Validade,
            UsosMaximos = c.UsosMaximos,
            UsosAtuais = c.UsosAtuais,
            Ativo = c.Ativo
        };
    }
}
