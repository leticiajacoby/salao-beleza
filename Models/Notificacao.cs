namespace SalaoBeleza.Models;

public class Notificacao
{
    public int Id { get; set; }
    public int UsuarioId { get; set; }
    public string Titulo { get; set; } = string.Empty;
    public string Mensagem { get; set; } = string.Empty;
    public bool Lida { get; set; }
    public DateTime CriadoEm { get; set; }
}
