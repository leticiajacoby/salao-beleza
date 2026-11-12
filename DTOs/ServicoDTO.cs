namespace SalaoBeleza.DTOs;

public record ServicoSalvarDTO(string Nome, decimal Preco, int DuracaoMinutos);

public record AtivoDTO(bool Ativo);

public class ServicoRespostaDTO
{
    public int Id { get; set; }
    public string Nome { get; set; } = "";
    public decimal Preco { get; set; }
    public int DuracaoMinutos { get; set; }
    public bool Ativo { get; set; }
}
