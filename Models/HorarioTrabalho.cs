namespace SalaoBeleza.Models;

// Expediente do barbeiro em um dia da semana (0 = domingo ... 6 = sabado).
public class HorarioTrabalho
{
    public int Id { get; set; }
    public int BarbeiroId { get; set; }
    public Barbeiro? Barbeiro { get; set; }

    public int DiaSemana { get; set; }
    public bool Trabalha { get; set; }
    public TimeSpan Inicio { get; set; }
    public TimeSpan Fim { get; set; }
    public TimeSpan? AlmocoInicio { get; set; }
    public TimeSpan? AlmocoFim { get; set; }
}
