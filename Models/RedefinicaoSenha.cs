namespace SalaoBeleza.Models;

public class RedefinicaoSenha
{
    public int Id { get; set; }
    public int UsuarioId { get; set; }

    // SHA256 do token enviado por e-mail (o token em si nao e guardado).
    public string TokenHash { get; set; } = string.Empty;
    public DateTime Expira { get; set; }
    public bool Usado { get; set; }
}
