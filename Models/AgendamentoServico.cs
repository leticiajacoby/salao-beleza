namespace SalaoBeleza.Models;

// Cada servico escolhido dentro de um agendamento (combo).
// Guarda copia do nome, preco e duracao no momento da compra.
public class AgendamentoServico
{
    public int Id { get; set; }
    public int AgendamentoId { get; set; }
    public Agendamento? Agendamento { get; set; }

    public int ServicoId { get; set; }
    public string Nome { get; set; } = string.Empty;
    public decimal Preco { get; set; }
    public int DuracaoMinutos { get; set; }
}
