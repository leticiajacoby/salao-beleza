using System.Text.Json;

namespace SalaoBeleza.Common;

// Garante que TODO erro da API volte como JSON { "mensagem": "..." }.
// (Antes, erros voltavam como texto puro e o site mostrava "nao foi possivel conectar".)
public class TratadorDeErros
{
    private readonly RequestDelegate _proximo;
    private readonly ILogger<TratadorDeErros> _log;

    public TratadorDeErros(RequestDelegate proximo, ILogger<TratadorDeErros> log)
    {
        _proximo = proximo;
        _log = log;
    }

    public async Task InvokeAsync(HttpContext contexto)
    {
        try
        {
            await _proximo(contexto);
        }
        catch (NegocioException ex)
        {
            await Responder(contexto, ex.Status, ex.Message);
        }
        catch (Exception ex)
        {
            _log.LogError(ex, "Erro nao tratado em {Metodo} {Caminho}", contexto.Request.Method, contexto.Request.Path);
            await Responder(contexto, 500, "Erro interno do servidor. Tente novamente em instantes.");
        }
    }

    private static async Task Responder(HttpContext contexto, int status, string mensagem)
    {
        if (contexto.Response.HasStarted)
            return;

        contexto.Response.Clear();
        contexto.Response.StatusCode = status;
        contexto.Response.ContentType = "application/json; charset=utf-8";

        var json = JsonSerializer.Serialize(new { mensagem });
        await contexto.Response.WriteAsync(json);
    }
}
