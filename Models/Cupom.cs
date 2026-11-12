namespace SalaoBeleza.Models;

public class Cupom
{
    public int Id { get; set; }

    // Sempre em maiusculas.
    public string Codigo { get; set; } = string.Empty;

    // "Percentual" (Valor = 10 significa 10%) ou "Valor" (Valor = 15 significa R$ 15,00)
    public string Tipo { get; set; } = "Percentual";
    public decimal Valor { get; set; }

    public DateTime? Validade { get; set; }

    // null = ilimitado
    public int? UsosMaximos { get; set; }
    public int UsosAtuais { get; set; }
    public bool Ativo { get; set; } = true;
}
