namespace SalaoBeleza.Models;

// Cliente quer ser avisado se abrir vaga com um barbeiro em um dia.
public class ListaEspera
{
    public int Id { get; set; }

    public int ClienteId { get; set; }
    public Usuario? Cliente { get; set; }

    public int BarbeiroId { get; set; }
    public Barbeiro? Barbeiro { get; set; }

    // Dia desejado (sempre meia-noite).
    public DateTime Data { get; set; }

    // Ids dos servicos desejados separados por virgula (ex.: "1,3").
    public string ServicoIds { get; set; } = string.Empty;

    // Aguardando ou Notificado
    public string Status { get; set; } = "Aguardando";
    public DateTime CriadoEm { get; set; }
}
