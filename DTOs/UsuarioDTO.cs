using Microsoft.AspNetCore.Http;

namespace SalaoBeleza.DTOs;

public record UsuarioRegistrarDTO(string Nome, string Email, string Senha);

public record UsuarioLoginDTO(string Email, string Senha);

public record EsqueciSenhaDTO(string Email);

public record RedefinirSenhaDTO(string Token, string NovaSenha);

public record AtualizarPerfilDTO(string Nome, string Email);

public record AlterarSenhaDTO(string SenhaAtual, string NovaSenha);

public class UsuarioRespostaDTO
{
    public int Id { get; set; }
    public string Nome { get; set; } = "";
    public string Email { get; set; } = "";
    public string Tipo { get; set; } = "";
    public int? BarbeiroId { get; set; }
    public string? FotoUrl { get; set; }
}

public class AuthRespostaDTO
{
    public string Token { get; set; } = "";
    public UsuarioRespostaDTO Usuario { get; set; } = new();
}

// Upload de imagem (foto de perfil ou foto do portfolio).
public class UploadArquivoDTO
{
    public IFormFile? Arquivo { get; set; }
    public string? Legenda { get; set; }
}
