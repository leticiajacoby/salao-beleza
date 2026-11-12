using Microsoft.EntityFrameworkCore;
using SalaoBeleza.Data;
using SalaoBeleza.DTOs;

namespace SalaoBeleza.Services;

// A cada 10 servicos PAGOS concluidos o cliente ganha 1 servico gratis.
// Servicos gratis nao contam para a proxima meta. Cancelar um agendamento gratis devolve o credito.
public class FidelidadeService
{
    public const int Meta = 10;

    private readonly AppDbContext _db;

    public FidelidadeService(AppDbContext db)
    {
        _db = db;
    }

    public async Task<FidelidadeDTO> Resumo(int clienteId)
    {
        var pagos = await _db.Agendamentos.CountAsync(a =>
            a.ClienteId == clienteId && a.Status == "Concluido" && !a.UsouFidelidade);

        var usados = await _db.Agendamentos.CountAsync(a =>
            a.ClienteId == clienteId && a.UsouFidelidade && a.Status != "Cancelado");

        var ganhos = pagos / Meta;
        var disponiveis = ganhos - usados;
        if (disponiveis < 0)
            disponiveis = 0;

        return new FidelidadeDTO
        {
            Meta = Meta,
            Progresso = pagos % Meta,
            PagosConcluidos = pagos,
            GratisGanhos = ganhos,
            GratisUsados = usados,
            GratisDisponiveis = disponiveis
        };
    }
}
