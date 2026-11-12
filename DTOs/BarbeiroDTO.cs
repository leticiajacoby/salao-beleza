namespace SalaoBeleza.DTOs;

public record BarbeiroCriarDTO(string Nome, string Email, string Senha, decimal PercentualBarbeiro);

public record BarbeiroEditarDTO(string Nome, string Email, string? NovaSenha, decimal PercentualBarbeiro);

public record BarbeiroPerfilEditarDTO(string? Especialidades, string? Bio);

public record FolgaCriarDTO(DateTime DataInicio, DateTime DataFim, string? Motivo);

public class BarbeiroListaDTO
{
    public int Id { get; set; }
    public int UsuarioId { get; set; }
    public string Nome { get; set; } = "";
    public string? Email { get; set; }
    public string? FotoUrl { get; set; }
    public string Especialidades { get; set; } = "";
    public bool Ativo { get; set; }
    public double Media { get; set; }
    public int TotalAvaliacoes { get; set; }

    // So aparece para administrador e para o proprio barbeiro.
    public decimal? PercentualBarbeiro { get; set; }
}

public class FotoPortfolioDTO
{
    public int Id { get; set; }
    public string Url { get; set; } = "";
    public string Legenda { get; set; } = "";
}

public class AvaliacaoPublicaDTO
{
    public int Id { get; set; }
    public string NomeCliente { get; set; } = "";
    public int Nota { get; set; }
    public string Comentario { get; set; } = "";
    public DateTime Data { get; set; }
}

public class BarbeiroPerfilDTO
{
    public int Id { get; set; }
    public string Nome { get; set; } = "";
    public string? FotoUrl { get; set; }
    public string Especialidades { get; set; } = "";
    public string Bio { get; set; } = "";
    public bool Ativo { get; set; }
    public double Media { get; set; }
    public int TotalAvaliacoes { get; set; }
    public List<FotoPortfolioDTO> Fotos { get; set; } = new();
    public List<AvaliacaoPublicaDTO> Avaliacoes { get; set; } = new();
}

// Horas sempre no formato "HH:mm".
public class HorarioDTO
{
    public int DiaSemana { get; set; }
    public bool Trabalha { get; set; }
    public string Inicio { get; set; } = "09:00";
    public string Fim { get; set; } = "18:00";
    public string? AlmocoInicio { get; set; }
    public string? AlmocoFim { get; set; }
}

public class FolgaRespostaDTO
{
    public int Id { get; set; }

    // Primeiro e ultimo dia bloqueados (inclusivos).
    public DateTime DataInicio { get; set; }
    public DateTime DataFim { get; set; }
    public string Motivo { get; set; } = "";
}

public class FolgaCriadaDTO
{
    public FolgaRespostaDTO Folga { get; set; } = new();
    public int AgendamentosAfetados { get; set; }
}

public class GanhosDTO
{
    public string Mes { get; set; } = "";
    public int Concluidos { get; set; }
    public int Faltas { get; set; }
    public int Cancelados { get; set; }
    public int Agendados { get; set; }
    public decimal TotalRecebido { get; set; }
    public decimal TotalPrevisto { get; set; }
}
