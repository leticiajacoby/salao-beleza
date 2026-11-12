using System.Security.Claims;
using Microsoft.EntityFrameworkCore;
using SalaoBeleza.Common;
using SalaoBeleza.Data;
using SalaoBeleza.DTOs;
using SalaoBeleza.Models;

namespace SalaoBeleza.Services;

public class BarbeiroService
{
    private readonly AppDbContext _db;
    private readonly ArquivoService _arquivos;

    public BarbeiroService(AppDbContext db, ArquivoService arquivos)
    {
        _db = db;
        _arquivos = arquivos;
    }

    // Media e total de avaliacoes de cada barbeiro (calculado em memoria: poucos dados).
    public async Task<Dictionary<int, (double Media, int Total)>> Medias()
    {
        var notas = await _db.Avaliacoes
            .AsNoTracking()
            .Select(v => new { v.BarbeiroId, v.Nota })
            .ToListAsync();

        return notas
            .GroupBy(n => n.BarbeiroId)
            .ToDictionary(
                g => g.Key,
                g => (Media: Math.Round(g.Average(x => (double)x.Nota), 1), Total: g.Count()));
    }

    public async Task<List<BarbeiroListaDTO>> Listar(ClaimsPrincipal user, bool incluirInativos)
    {
        var q = _db.Barbeiros.AsNoTracking().Include(b => b.Usuario).AsQueryable();

        if (!(user.EhAdmin() && incluirInativos))
            q = q.Where(b => b.Ativo);

        var lista = await q.ToListAsync();
        var medias = await Medias();

        var meuId = user.IdBarbeiro();
        var admin = user.EhAdmin();

        return lista
            .OrderBy(b => b.Usuario?.Nome ?? "")
            .Select(b =>
            {
                var tem = medias.TryGetValue(b.Id, out var m);

                return new BarbeiroListaDTO
                {
                    Id = b.Id,
                    UsuarioId = b.UsuarioId,
                    Nome = b.Usuario?.Nome ?? "",
                    Email = admin ? b.Usuario?.Email : null,
                    FotoUrl = b.Usuario?.FotoUrl,
                    Especialidades = b.Especialidades,
                    Ativo = b.Ativo,
                    Media = tem ? m.Media : 0,
                    TotalAvaliacoes = tem ? m.Total : 0,
                    PercentualBarbeiro = (admin || meuId == b.Id) ? b.PercentualBarbeiro : null
                };
            })
            .ToList();
    }

    // Perfil publico (qualquer pessoa pode ver).
    public async Task<BarbeiroPerfilDTO> Perfil(ClaimsPrincipal user, int id)
    {
        var barbeiro = await _db.Barbeiros
            .AsNoTracking()
            .Include(b => b.Usuario)
            .Include(b => b.Fotos)
            .FirstOrDefaultAsync(b => b.Id == id);

        var pode = barbeiro != null && (barbeiro.Ativo || user.EhAdmin() || user.IdBarbeiro() == id);

        if (barbeiro == null || !pode)
            throw new NegocioException("Profissional nao encontrada.", 404);

        var notas = await _db.Avaliacoes
            .AsNoTracking()
            .Where(v => v.BarbeiroId == id)
            .Select(v => v.Nota)
            .ToListAsync();

        var recentes = await _db.Avaliacoes
            .AsNoTracking()
            .Include(v => v.Cliente)
            .Where(v => v.BarbeiroId == id)
            .OrderByDescending(v => v.CriadoEm)
            .Take(30)
            .ToListAsync();

        return new BarbeiroPerfilDTO
        {
            Id = barbeiro.Id,
            Nome = barbeiro.Usuario?.Nome ?? "",
            FotoUrl = barbeiro.Usuario?.FotoUrl,
            Especialidades = barbeiro.Especialidades,
            Bio = barbeiro.Bio,
            Ativo = barbeiro.Ativo,
            Media = notas.Count > 0 ? Math.Round(notas.Average(n => (double)n), 1) : 0,
            TotalAvaliacoes = notas.Count,
            Fotos = barbeiro.Fotos
                .OrderByDescending(f => f.Id)
                .Select(f => new FotoPortfolioDTO { Id = f.Id, Url = f.Url, Legenda = f.Legenda })
                .ToList(),
            Avaliacoes = recentes.Select(v => new AvaliacaoPublicaDTO
            {
                Id = v.Id,
                NomeCliente = PrimeiroNome(v.Cliente?.Nome),
                Nota = v.Nota,
                Comentario = v.Comentario,
                Data = v.CriadoEm
            }).ToList()
        };
    }

    public async Task<BarbeiroListaDTO> Criar(BarbeiroCriarDTO dto)
    {
        var nome = (dto.Nome ?? "").Trim();
        var email = UsuarioService.NormalizarEmail(dto.Email);

        if (nome.Length < 2)
            throw new NegocioException("O nome e obrigatorio.");

        UsuarioService.ValidarEmail(email);
        UsuarioService.ValidarSenha(dto.Senha);
        ValidarPercentual(dto.PercentualBarbeiro);

        if (await _db.Usuarios.AnyAsync(u => u.Email.ToLower() == email))
            throw new NegocioException("Ja existe um usuario com esse email.", 409);

        var usuario = new Usuario
        {
            Nome = nome,
            Email = email,
            SenhaHash = SenhaHasher.Gerar(dto.Senha),
            Tipo = "Barbeiro"
        };

        usuario.Barbeiro = new Barbeiro
        {
            PercentualBarbeiro = dto.PercentualBarbeiro,
            Ativo = true,
            Especialidades = "",
            Bio = ""
        };

        _db.Usuarios.Add(usuario);
        await _db.SaveChangesAsync();

        return new BarbeiroListaDTO
        {
            Id = usuario.Barbeiro.Id,
            UsuarioId = usuario.Id,
            Nome = usuario.Nome,
            Email = usuario.Email,
            FotoUrl = null,
            Especialidades = "",
            Ativo = true,
            PercentualBarbeiro = usuario.Barbeiro.PercentualBarbeiro
        };
    }

    public async Task<BarbeiroListaDTO> Editar(int id, BarbeiroEditarDTO dto)
    {
        var barbeiro = await _db.Barbeiros.Include(b => b.Usuario).FirstOrDefaultAsync(b => b.Id == id);
        if (barbeiro == null || barbeiro.Usuario == null)
            throw new NegocioException("Profissional nao encontrada.", 404);

        var nome = (dto.Nome ?? "").Trim();
        var email = UsuarioService.NormalizarEmail(dto.Email);

        if (nome.Length < 2)
            throw new NegocioException("O nome e obrigatorio.");

        UsuarioService.ValidarEmail(email);
        ValidarPercentual(dto.PercentualBarbeiro);

        var usuarioId = barbeiro.UsuarioId;
        if (await _db.Usuarios.AnyAsync(u => u.Id != usuarioId && u.Email.ToLower() == email))
            throw new NegocioException("Ja existe um usuario com esse email.", 409);

        barbeiro.Usuario.Nome = nome;
        barbeiro.Usuario.Email = email;
        barbeiro.PercentualBarbeiro = dto.PercentualBarbeiro;

        if (!string.IsNullOrWhiteSpace(dto.NovaSenha))
        {
            UsuarioService.ValidarSenha(dto.NovaSenha);
            barbeiro.Usuario.SenhaHash = SenhaHasher.Gerar(dto.NovaSenha);
        }

        await _db.SaveChangesAsync();

        var medias = await Medias();
        var tem = medias.TryGetValue(barbeiro.Id, out var m);

        return new BarbeiroListaDTO
        {
            Id = barbeiro.Id,
            UsuarioId = barbeiro.UsuarioId,
            Nome = barbeiro.Usuario.Nome,
            Email = barbeiro.Usuario.Email,
            FotoUrl = barbeiro.Usuario.FotoUrl,
            Especialidades = barbeiro.Especialidades,
            Ativo = barbeiro.Ativo,
            Media = tem ? m.Media : 0,
            TotalAvaliacoes = tem ? m.Total : 0,
            PercentualBarbeiro = barbeiro.PercentualBarbeiro
        };
    }

    public async Task DefinirAtivo(int id, bool ativo)
    {
        var barbeiro = await _db.Barbeiros.FirstOrDefaultAsync(b => b.Id == id);
        if (barbeiro == null)
            throw new NegocioException("Profissional nao encontrada.", 404);

        barbeiro.Ativo = ativo;
        await _db.SaveChangesAsync();
    }

    public async Task Remover(int id)
    {
        var barbeiro = await _db.Barbeiros
            .Include(b => b.Usuario)
            .Include(b => b.Fotos)
            .FirstOrDefaultAsync(b => b.Id == id);

        if (barbeiro == null)
            throw new NegocioException("Profissional nao encontrada.", 404);

        var temHistorico = await _db.Agendamentos.AnyAsync(a => a.BarbeiroId == id);
        if (temHistorico)
            throw new NegocioException("Esta profissional tem agendamentos no historico. Desative-a em vez de excluir.", 409);

        var urls = barbeiro.Fotos.Select(f => f.Url).ToList();
        urls.Add(barbeiro.Usuario?.FotoUrl ?? "");

        if (barbeiro.Usuario != null)
            _db.Usuarios.Remove(barbeiro.Usuario);
        else
            _db.Barbeiros.Remove(barbeiro);

        await _db.SaveChangesAsync();

        foreach (var url in urls)
            _arquivos.Remover(url);
    }

    public async Task<BarbeiroPerfilDTO> EditarPerfil(ClaimsPrincipal user, int id, BarbeiroPerfilEditarDTO dto)
    {
        var barbeiro = await _db.Barbeiros.FirstOrDefaultAsync(b => b.Id == id);
        if (barbeiro == null)
            throw new NegocioException("Profissional nao encontrada.", 404);

        var especialidades = (dto.Especialidades ?? "").Trim();
        var bio = (dto.Bio ?? "").Trim();

        if (especialidades.Length > 200)
            throw new NegocioException("As especialidades podem ter no maximo 200 caracteres.");

        if (bio.Length > 1000)
            throw new NegocioException("A apresentacao pode ter no maximo 1000 caracteres.");

        barbeiro.Especialidades = especialidades;
        barbeiro.Bio = bio;

        await _db.SaveChangesAsync();

        return await Perfil(user, id);
    }

    public async Task<FotoPortfolioDTO> AdicionarFoto(int id, IFormFile? arquivo, string? legenda)
    {
        var barbeiro = await _db.Barbeiros.FirstOrDefaultAsync(b => b.Id == id);
        if (barbeiro == null)
            throw new NegocioException("Profissional nao encontrada.", 404);

        var total = await _db.BarbeiroFotos.CountAsync(f => f.BarbeiroId == id);
        if (total >= 12)
            throw new NegocioException("O portfolio pode ter no maximo 12 fotos. Remova alguma para adicionar outra.");

        var texto = (legenda ?? "").Trim();
        if (texto.Length > 150)
            texto = texto.Substring(0, 150);

        var url = await _arquivos.SalvarImagem(arquivo, "portfolio");

        var foto = new BarbeiroFoto
        {
            BarbeiroId = id,
            Url = url,
            Legenda = texto
        };

        _db.BarbeiroFotos.Add(foto);
        await _db.SaveChangesAsync();

        return new FotoPortfolioDTO { Id = foto.Id, Url = foto.Url, Legenda = foto.Legenda };
    }

    public async Task RemoverFoto(int id, int fotoId)
    {
        var foto = await _db.BarbeiroFotos.FirstOrDefaultAsync(f => f.Id == fotoId && f.BarbeiroId == id);
        if (foto == null)
            throw new NegocioException("Foto nao encontrada.", 404);

        var url = foto.Url;

        _db.BarbeiroFotos.Remove(foto);
        await _db.SaveChangesAsync();

        _arquivos.Remover(url);
    }

    private static void ValidarPercentual(decimal percentual)
    {
        if (percentual < 0 || percentual > 100)
            throw new NegocioException("A porcentagem deve estar entre 0 e 100.");
    }

    private static string PrimeiroNome(string? nome)
    {
        var texto = (nome ?? "").Trim();
        if (texto.Length == 0)
            return "Cliente";

        var partes = texto.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        return partes[0];
    }
}
