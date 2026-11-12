namespace SalaoBeleza.DTOs;

public record CupomSalvarDTO(string Codigo, string Tipo, decimal Valor, DateTime? Validade, int? UsosMaximos);

public class CupomRespostaDTO
{
    public int Id { get; set; }
    public string Codigo { get; set; } = "";
    public string Tipo { get; set; } = "";
    public decimal Valor { get; set; }
    public DateTime? Validade { get; set; }
    public int? UsosMaximos { get; set; }
    public int UsosAtuais { get; set; }
    public bool Ativo { get; set; }
}

public class CupomValidacaoDTO
{
    public bool Valido { get; set; }
    public string Mensagem { get; set; } = "";
    public string? Codigo { get; set; }
    public string? Tipo { get; set; }
    public decimal Valor { get; set; }
    public decimal Desconto { get; set; }
}

public record ListaEsperaCriarDTO(int BarbeiroId, DateTime Data, List<int>? ServicoIds);

public class ListaEsperaRespostaDTO
{
    public int Id { get; set; }
    public int BarbeiroId { get; set; }
    public string NomeBarbeiro { get; set; } = "";
    public int ClienteId { get; set; }
    public string NomeCliente { get; set; } = "";
    public DateTime Data { get; set; }
    public string Servicos { get; set; } = "";
    public List<int> ServicoIds { get; set; } = new();
    public string Status { get; set; } = "";
}

public class NotificacaoDTO
{
    public int Id { get; set; }
    public string Titulo { get; set; } = "";
    public string Mensagem { get; set; } = "";
    public bool Lida { get; set; }
    public DateTime CriadoEm { get; set; }
}

public class NotificacoesRespostaDTO
{
    public int NaoLidas { get; set; }
    public List<NotificacaoDTO> Itens { get; set; } = new();
}

public class ClienteListaDTO
{
    public int Id { get; set; }
    public string Nome { get; set; } = "";
    public string Email { get; set; } = "";
    public string? FotoUrl { get; set; }
    public int TotalAgendamentos { get; set; }
}

public class MesValorDTO
{
    public string Mes { get; set; } = "";
    public decimal Faturamento { get; set; }
    public int Atendimentos { get; set; }
}

public class ServicoRankingDTO
{
    public string Nome { get; set; } = "";
    public int Quantidade { get; set; }
    public decimal Faturamento { get; set; }
}

public class BarbeiroRankingDTO
{
    public int BarbeiroId { get; set; }
    public string Nome { get; set; } = "";
    public string? FotoUrl { get; set; }
    public int Atendimentos { get; set; }
    public decimal Faturamento { get; set; }
    public decimal Comissao { get; set; }
    public double Media { get; set; }
}

public class HoraPicoDTO
{
    public int Hora { get; set; }
    public int Quantidade { get; set; }
}

public class DashboardDTO
{
    public decimal FaturamentoMes { get; set; }
    public decimal FaturamentoMesAnterior { get; set; }
    public int AtendimentosMes { get; set; }
    public decimal TicketMedio { get; set; }
    public int AgendamentosHoje { get; set; }
    public int AgendamentosFuturos { get; set; }
    public int TotalClientes { get; set; }
    public int Faltas { get; set; }
    public List<MesValorDTO> FaturamentoPorMes { get; set; } = new();
    public List<ServicoRankingDTO> ServicosMaisPedidos { get; set; } = new();
    public List<BarbeiroRankingDTO> RankingBarbeiros { get; set; } = new();
    public List<HoraPicoDTO> HorariosPico { get; set; } = new();
}
