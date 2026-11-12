namespace SalaoBeleza.DTOs;

public record AgendamentoCriarDTO(
    DateTime DataHora,
    int BarbeiroId,
    List<int>? ServicoIds,
    string? CupomCodigo,
    bool UsarFidelidade,
    int? ClienteId
);

public record SimularDTO(List<int>? ServicoIds, string? CupomCodigo, bool UsarFidelidade);

public record ReagendarDTO(DateTime DataHora, int? BarbeiroId);

public record StatusDTO(string Status);

public record AvaliarDTO(int AgendamentoId, int Nota, string? Comentario);

public class ServicoItemDTO
{
    public int ServicoId { get; set; }
    public string Nome { get; set; } = "";
    public decimal Preco { get; set; }
    public int DuracaoMinutos { get; set; }
}

public class AvaliacaoResumoDTO
{
    public int Nota { get; set; }
    public string Comentario { get; set; } = "";
}

public class AgendamentoRespostaDTO
{
    public int Id { get; set; }
    public DateTime DataHora { get; set; }
    public DateTime Fim { get; set; }
    public int DuracaoMinutos { get; set; }
    public string Status { get; set; } = "";

    public int ClienteId { get; set; }
    public string NomeCliente { get; set; } = "";
    public string? FotoCliente { get; set; }

    public int BarbeiroId { get; set; }
    public string NomeBarbeiro { get; set; } = "";
    public string? FotoBarbeiro { get; set; }

    public List<ServicoItemDTO> Servicos { get; set; } = new();

    // Nomes dos servicos juntos: "Corte + Barba"
    public string NomeServico { get; set; } = "";

    public decimal ValorServico { get; set; }
    public decimal ValorDesconto { get; set; }
    public decimal ValorFinal { get; set; }
    public decimal PercentualBarbeiro { get; set; }
    public decimal ValorBarbeiro { get; set; }
    public decimal ValorAdministrador { get; set; }

    public string? CupomCodigo { get; set; }
    public bool UsouFidelidade { get; set; }

    public AvaliacaoResumoDTO? Avaliacao { get; set; }
}

public class SimulacaoRespostaDTO
{
    public decimal ValorServico { get; set; }
    public decimal ValorDesconto { get; set; }
    public decimal ValorFinal { get; set; }
    public int DuracaoMinutos { get; set; }
    public string? CupomCodigo { get; set; }
    public bool UsouFidelidade { get; set; }

    // Preenchido quando o cupom/fidelidade nao pode ser aplicado (o agendamento em si ainda seria aceito sem eles).
    public string? Aviso { get; set; }
}

public class FidelidadeDTO
{
    public int Meta { get; set; } = 10;

    // Servicos pagos concluidos no ciclo atual (0 a Meta-1).
    public int Progresso { get; set; }
    public int PagosConcluidos { get; set; }
    public int GratisGanhos { get; set; }
    public int GratisUsados { get; set; }
    public int GratisDisponiveis { get; set; }
}
