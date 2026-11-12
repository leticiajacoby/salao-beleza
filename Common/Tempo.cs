namespace SalaoBeleza.Common;

// O banco usa "timestamp without time zone". Todas as datas do sistema sao horario local do salao,
// sempre com Kind = Unspecified para o Npgsql aceitar.
public static class Tempo
{
    public static DateTime Agora()
    {
        return DateTime.SpecifyKind(DateTime.Now, DateTimeKind.Unspecified);
    }

    public static DateTime SemKind(DateTime data)
    {
        return DateTime.SpecifyKind(data, DateTimeKind.Unspecified);
    }

    public static DateTime Normalizar(DateTime data)
    {
        // Remove segundos e troca o Kind.
        return new DateTime(data.Year, data.Month, data.Day, data.Hour, data.Minute, 0, DateTimeKind.Unspecified);
    }
}
