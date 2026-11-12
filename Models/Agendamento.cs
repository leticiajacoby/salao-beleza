namespace SalaoBeleza.Models;

public class Agendamento
{
    public int Id { get; set; }

    public int ClienteId { get; set; }
    public Usuario? Cliente { get; set; }

    public int BarbeiroId { get; set; }
    public Barbeiro? Barbeiro { get; set; }

    // Servico principal (o primeiro do combo). Os itens completos ficam em Itens.
    public int ServicoId { get; set; }
    public Servico? Servico { get; set; }

    public DateTime DataHora { get; set; }

    // Soma da duracao de todos os servicos do agendamento.
    public int DuracaoMinutos { get; set; }

    // Agendado, Concluido, Faltou ou Cancelado
    public string Status { get; set; } = "Agendado";

    // Soma dos precos dos servicos (antes de descontos).
    public decimal ValorServico { get; set; }

    public decimal ValorDesconto { get; set; }

    // O que o cliente paga: ValorServico - ValorDesconto.
    public decimal ValorFinal { get; set; }

    public decimal PercentualBarbeiro { get; set; }

    // Comissao calculada sobre ValorServico. O salao absorve descontos e servicos gratis.
    public decimal ValorBarbeiro { get; set; }

    // ValorFinal - ValorBarbeiro (pode ser negativo quando ha desconto grande).
    public decimal ValorAdministrador { get; set; }

    public int? CupomId { get; set; }
    public string? CupomCodigo { get; set; }
    public bool UsouFidelidade { get; set; }

    public DateTime CriadoEm { get; set; }

    public List<AgendamentoServico> Itens { get; set; } = new();
    public Avaliacao? Avaliacao { get; set; }
}
