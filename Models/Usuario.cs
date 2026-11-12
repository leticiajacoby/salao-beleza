namespace SalaoBeleza.Models;

public class Usuario
{
    public int Id { get; set; }
    public string Nome { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public string SenhaHash { get; set; } = string.Empty;

    // "Administrador", "Barbeiro" ou "Cliente"
    public string Tipo { get; set; } = "Cliente";

    // Caminho publico da foto de perfil (ex.: /uploads/avatares/abc.jpg)
    public string? FotoUrl { get; set; }

    public Barbeiro? Barbeiro { get; set; }
    public List<Agendamento> Agendamentos { get; set; } = new();
}
