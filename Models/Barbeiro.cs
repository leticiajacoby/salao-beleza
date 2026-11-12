namespace SalaoBeleza.Models;

public class Barbeiro
{
    public int Id { get; set; }

    public int UsuarioId { get; set; }
    public Usuario? Usuario { get; set; }

    public decimal PercentualBarbeiro { get; set; }

    // Barbeiro desativado nao aparece para os clientes e nao recebe agendamentos.
    public bool Ativo { get; set; } = true;

    // Perfil publico
    public string Especialidades { get; set; } = string.Empty;
    public string Bio { get; set; } = string.Empty;

    public List<Agendamento> Agendamentos { get; set; } = new();
    public List<BarbeiroFoto> Fotos { get; set; } = new();
    public List<HorarioTrabalho> Horarios { get; set; } = new();
    public List<Folga> Folgas { get; set; } = new();
}
