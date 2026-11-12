namespace SalaoBeleza.Models;

// Bloqueio de datas (ferias, folga, atestado). Inicio inclusivo, Fim exclusivo (meia-noite do dia seguinte ao ultimo dia).
public class Folga
{
    public int Id { get; set; }
    public int BarbeiroId { get; set; }
    public Barbeiro? Barbeiro { get; set; }

    public DateTime Inicio { get; set; }
    public DateTime Fim { get; set; }
    public string Motivo { get; set; } = string.Empty;
}
