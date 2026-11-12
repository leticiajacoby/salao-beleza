namespace SalaoBeleza.Common;

// Erro "esperado" (validacao, regra de negocio). A mensagem e mostrada ao usuario.
public class NegocioException : Exception
{
    public int Status { get; }

    public NegocioException(string mensagem, int status = 400) : base(mensagem)
    {
        Status = status;
    }
}
