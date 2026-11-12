namespace SalaoBeleza.Models;

// Foto do portfolio de cortes do barbeiro.
public class BarbeiroFoto
{
    public int Id { get; set; }
    public int BarbeiroId { get; set; }
    public Barbeiro? Barbeiro { get; set; }
    public string Url { get; set; } = string.Empty;
    public string Legenda { get; set; } = string.Empty;
}
