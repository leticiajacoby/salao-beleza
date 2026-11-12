using System.Collections.Concurrent;
using System.Net.Mail;
using System.Security.Cryptography;
using System.Text;
using Microsoft.EntityFrameworkCore;
using SalaoBeleza.Common;
using SalaoBeleza.Data;
using SalaoBeleza.DTOs;
using SalaoBeleza.Models;

namespace SalaoBeleza.Services;

public class UsuarioService
{
    private const int MaxFalhas = 5;
    private static readonly TimeSpan TempoBloqueio = TimeSpan.FromMinutes(5);

    // Falhas de login por e-mail (protecao simples contra tentativa em massa de senhas).
    private static readonly ConcurrentDictionary<string, (int Falhas, DateTime Ate)> _falhas = new();

    private readonly AppDbContext _db;
    private readonly JwtService _jwt;
    private readonly IEmailService _email;
    private readonly ArquivoService _arquivos;

    public UsuarioService(AppDbContext db, JwtService jwt, IEmailService email, ArquivoService arquivos)
    {
        _db = db;
        _jwt = jwt;
        _email = email;
        _arquivos = arquivos;
    }

    public async Task<AuthRespostaDTO> Registrar(UsuarioRegistrarDTO dto)
    {
        var nome = (dto.Nome ?? "").Trim();
        var email = NormalizarEmail(dto.Email);

        if (nome.Length < 2)
            throw new NegocioException("O nome e obrigatorio.");

        ValidarEmail(email);
        ValidarSenha(dto.Senha);

        if (await _db.Usuarios.AnyAsync(u => u.Email.ToLower() == email))
            throw new NegocioException("Ja existe um usuario com esse email.", 409);

        var usuario = new Usuario
        {
            Nome = nome,
            Email = email,
            SenhaHash = SenhaHasher.Gerar(dto.Senha),
            Tipo = "Cliente"
        };

        _db.Usuarios.Add(usuario);
        await _db.SaveChangesAsync();

        return Montar(usuario, null);
    }

    public async Task<AuthRespostaDTO> Login(UsuarioLoginDTO dto)
    {
        var email = NormalizarEmail(dto.Email);
        var senha = dto.Senha ?? "";

        if (email.Length == 0 || senha.Length == 0)
            throw new NegocioException("Informe e-mail e senha.");

        VerificarBloqueio(email);

        var usuario = await _db.Usuarios
            .Include(u => u.Barbeiro)
            .FirstOrDefaultAsync(u => u.Email.ToLower() == email);

        if (usuario == null || !SenhaHasher.Verificar(senha, usuario.SenhaHash))
        {
            RegistrarFalha(email);
            throw new NegocioException("Email ou senha invalidos.", 401);
        }

        _falhas.TryRemove(email, out _);

        if (usuario.Barbeiro != null && !usuario.Barbeiro.Ativo)
            throw new NegocioException("Sua conta de profissional esta desativada. Fale com o administrador.", 403);

        if (SenhaHasher.PrecisaAtualizar(usuario.SenhaHash))
        {
            usuario.SenhaHash = SenhaHasher.Gerar(senha);
            await _db.SaveChangesAsync();
        }

        return Montar(usuario, usuario.Barbeiro?.Id);
    }

    // Retorna o link gerado (ou null se o e-mail nao existe). O controller nunca revela isso em producao.
    public async Task<string?> EsqueciSenha(string? emailBruto, string urlBase)
    {
        var email = NormalizarEmail(emailBruto);
        if (email.Length == 0)
            throw new NegocioException("Informe o e-mail.");

        var usuario = await _db.Usuarios.FirstOrDefaultAsync(u => u.Email.ToLower() == email);
        if (usuario == null)
            return null;

        var anteriores = await _db.RedefinicoesSenha
            .Where(r => r.UsuarioId == usuario.Id && !r.Usado)
            .ToListAsync();

        foreach (var antigo in anteriores)
            antigo.Usado = true;

        var token = Convert.ToBase64String(RandomNumberGenerator.GetBytes(32))
            .Replace('+', '-')
            .Replace('/', '_')
            .TrimEnd('=');

        _db.RedefinicoesSenha.Add(new RedefinicaoSenha
        {
            UsuarioId = usuario.Id,
            TokenHash = HashToken(token),
            Expira = Tempo.Agora().AddMinutes(30),
            Usado = false
        });

        await _db.SaveChangesAsync();

        var link = urlBase.TrimEnd('/') + "/redefinir.html?token=" + Uri.EscapeDataString(token);

        var corpo =
            "<p>Ola, " + System.Net.WebUtility.HtmlEncode(usuario.Nome) + "!</p>" +
            "<p>Recebemos um pedido para redefinir a senha da sua conta no Spa Delas.</p>" +
            "<p><a href=\"" + link + "\">Clique aqui para criar uma nova senha</a> (o link vale por 30 minutos).</p>" +
            "<p>Se voce nao pediu isso, ignore este e-mail.</p>";

        await _email.EnviarAsync(usuario.Email, "Redefinicao de senha - Spa Delas", corpo);

        return link;
    }

    public async Task RedefinirSenha(RedefinirSenhaDTO dto)
    {
        ValidarSenha(dto.NovaSenha);

        if (string.IsNullOrWhiteSpace(dto.Token))
            throw new NegocioException("Link invalido ou expirado.");

        var hash = HashToken(dto.Token.Trim());
        var agora = Tempo.Agora();

        var pedido = await _db.RedefinicoesSenha
            .FirstOrDefaultAsync(r => r.TokenHash == hash && !r.Usado && r.Expira > agora);

        if (pedido == null)
            throw new NegocioException("Link invalido ou expirado. Peca um novo link.");

        var usuario = await _db.Usuarios.FirstOrDefaultAsync(u => u.Id == pedido.UsuarioId);
        if (usuario == null)
            throw new NegocioException("Link invalido ou expirado. Peca um novo link.");

        usuario.SenhaHash = SenhaHasher.Gerar(dto.NovaSenha);
        pedido.Usado = true;

        await _db.SaveChangesAsync();

        _falhas.TryRemove(usuario.Email.ToLowerInvariant(), out _);
    }

    public async Task<UsuarioRespostaDTO> Obter(int id)
    {
        var usuario = await BuscarComBarbeiro(id);
        return ParaDTO(usuario, usuario.Barbeiro?.Id);
    }

    public async Task<UsuarioRespostaDTO> AtualizarPerfil(int id, AtualizarPerfilDTO dto)
    {
        var nome = (dto.Nome ?? "").Trim();
        var email = NormalizarEmail(dto.Email);

        if (nome.Length < 2)
            throw new NegocioException("O nome e obrigatorio.");

        ValidarEmail(email);

        var usuario = await BuscarComBarbeiro(id);

        var emUso = await _db.Usuarios.AnyAsync(u => u.Id != id && u.Email.ToLower() == email);
        if (emUso)
            throw new NegocioException("Ja existe um usuario com esse email.", 409);

        usuario.Nome = nome;
        usuario.Email = email;

        await _db.SaveChangesAsync();

        return ParaDTO(usuario, usuario.Barbeiro?.Id);
    }

    public async Task AlterarSenha(int id, AlterarSenhaDTO dto)
    {
        var usuario = await BuscarComBarbeiro(id);

        if (!SenhaHasher.Verificar(dto.SenhaAtual ?? "", usuario.SenhaHash))
            throw new NegocioException("A senha atual esta incorreta.");

        ValidarSenha(dto.NovaSenha);

        usuario.SenhaHash = SenhaHasher.Gerar(dto.NovaSenha);
        await _db.SaveChangesAsync();
    }

    public async Task<UsuarioRespostaDTO> SalvarFoto(int id, IFormFile? arquivo)
    {
        var usuario = await BuscarComBarbeiro(id);

        var novaUrl = await _arquivos.SalvarImagem(arquivo, "avatares");
        var antiga = usuario.FotoUrl;

        usuario.FotoUrl = novaUrl;
        await _db.SaveChangesAsync();

        _arquivos.Remover(antiga);

        return ParaDTO(usuario, usuario.Barbeiro?.Id);
    }

    public async Task<UsuarioRespostaDTO> RemoverFoto(int id)
    {
        var usuario = await BuscarComBarbeiro(id);

        var antiga = usuario.FotoUrl;
        usuario.FotoUrl = null;
        await _db.SaveChangesAsync();

        _arquivos.Remover(antiga);

        return ParaDTO(usuario, usuario.Barbeiro?.Id);
    }

    public static UsuarioRespostaDTO ParaDTO(Usuario usuario, int? barbeiroId)
    {
        return new UsuarioRespostaDTO
        {
            Id = usuario.Id,
            Nome = usuario.Nome,
            Email = usuario.Email,
            Tipo = usuario.Tipo,
            BarbeiroId = barbeiroId,
            FotoUrl = usuario.FotoUrl
        };
    }

    public static string NormalizarEmail(string? email)
    {
        return (email ?? "").Trim().ToLowerInvariant();
    }

    public static void ValidarEmail(string email)
    {
        var valido = false;

        try
        {
            var endereco = new MailAddress(email);
            valido = endereco.Address == email && email.Contains('.') && email.Length <= 150;
        }
        catch (FormatException)
        {
            valido = false;
        }

        if (!valido)
            throw new NegocioException("Informe um e-mail valido.");
    }

    public static void ValidarSenha(string? senha)
    {
        if (string.IsNullOrWhiteSpace(senha) || senha.Length < 6)
            throw new NegocioException("A senha precisa ter pelo menos 6 caracteres.");

        if (senha.Length > 100)
            throw new NegocioException("A senha e muito longa.");
    }

    private async Task<Usuario> BuscarComBarbeiro(int id)
    {
        var usuario = await _db.Usuarios
            .Include(u => u.Barbeiro)
            .FirstOrDefaultAsync(u => u.Id == id);

        if (usuario == null)
            throw new NegocioException("Usuario nao encontrado.", 404);

        return usuario;
    }

    private AuthRespostaDTO Montar(Usuario usuario, int? barbeiroId)
    {
        return new AuthRespostaDTO
        {
            Token = _jwt.Gerar(usuario, barbeiroId),
            Usuario = ParaDTO(usuario, barbeiroId)
        };
    }

    private static string HashToken(string token)
    {
        return Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(token)));
    }

    private static void VerificarBloqueio(string email)
    {
        if (_falhas.TryGetValue(email, out var info) && info.Falhas >= MaxFalhas && info.Ate > DateTime.UtcNow)
        {
            var minutos = (int)Math.Ceiling((info.Ate - DateTime.UtcNow).TotalMinutes);
            throw new NegocioException("Muitas tentativas de login. Tente novamente em " + minutos + " minuto(s) ou use \"Esqueci minha senha\".", 429);
        }
    }

    private static void RegistrarFalha(string email)
    {
        _falhas.AddOrUpdate(
            email,
            _ => (1, DateTime.UtcNow.Add(TempoBloqueio)),
            (_, atual) =>
            {
                // Se o bloqueio anterior ja passou, recomeca a contagem.
                if (atual.Ate <= DateTime.UtcNow)
                    return (1, DateTime.UtcNow.Add(TempoBloqueio));

                return (atual.Falhas + 1, DateTime.UtcNow.Add(TempoBloqueio));
            });
    }
}
