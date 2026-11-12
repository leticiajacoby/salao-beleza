namespace SalaoBeleza.Models;

public class Avaliacao
{
    public int Id { get; set; }

    public int AgendamentoId { get; set; }
    public Agendamento? Agendamento { get; set; }

    public int ClienteId { get; set; }
    public Usuario? Cliente { get; set; }

    public int BarbeiroId { get; set; }
    public Barbeiro? Barbeiro { get; set; }

    // 1 a 5
    public int Nota { get; set; }
    public string Comentario { get; set; } = string.Empty;
    public DateTime CriadoEm { get; set; }
}
