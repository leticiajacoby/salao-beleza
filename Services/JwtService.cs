using System.Globalization;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using Microsoft.IdentityModel.Tokens;
using SalaoBeleza.Models;

namespace SalaoBeleza.Services;

public class JwtService
{
    private readonly IConfiguration _config;

    public JwtService(IConfiguration config)
    {
        _config = config;
    }

    public string Gerar(Usuario usuario, int? barbeiroId)
    {
        var chave = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(_config["Jwt:Key"]!));
        var credenciais = new SigningCredentials(chave, SecurityAlgorithms.HmacSha256);

        var claims = new List<Claim>
        {
            new Claim("sub", usuario.Id.ToString()),
            new Claim("role", usuario.Tipo),
            new Claim("name", usuario.Nome)
        };

        if (barbeiroId.HasValue)
            claims.Add(new Claim("bid", barbeiroId.Value.ToString()));

        var horas = 8d;
        var configurado = _config["Jwt:ExpiraHoras"];
        if (!string.IsNullOrWhiteSpace(configurado) &&
            double.TryParse(configurado, NumberStyles.Float, CultureInfo.InvariantCulture, out var h) && h > 0)
        {
            horas = h;
        }

        var token = new JwtSecurityToken(
            issuer: _config["Jwt:Issuer"],
            audience: _config["Jwt:Audience"],
            claims: claims,
            expires: DateTime.UtcNow.AddHours(horas),
            signingCredentials: credenciais);

        return new JwtSecurityTokenHandler().WriteToken(token);
    }
}
