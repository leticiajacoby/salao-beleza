using System.Security.Claims;

namespace SalaoBeleza.Common;

// Atalhos para ler os dados do token JWT do usuario logado.
public static class UsuarioLogado
{
    public static int IdUsuario(this ClaimsPrincipal user)
    {
        var valor = user.FindFirst("sub")?.Value;
        return int.TryParse(valor, out var id) ? id : 0;
    }

    public static int? IdBarbeiro(this ClaimsPrincipal user)
    {
        var valor = user.FindFirst("bid")?.Value;
        return int.TryParse(valor, out var id) ? id : null;
    }

    public static string Tipo(this ClaimsPrincipal user)
    {
        return user.FindFirst("role")?.Value ?? "";
    }

    public static bool EhAdmin(this ClaimsPrincipal user)
    {
        return user.Tipo() == "Administrador";
    }

    public static bool EhBarbeiro(this ClaimsPrincipal user)
    {
        return user.Tipo() == "Barbeiro";
    }

    public static bool EhCliente(this ClaimsPrincipal user)
    {
        return user.Tipo() == "Cliente";
    }
}
