using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SalaoBeleza.Common;
using SalaoBeleza.Services;

namespace SalaoBeleza.Controllers;

[ApiController]
[Route("api/favoritos")]
[Authorize(Roles = "Cliente")]
public class FavoritosController : ControllerBase
{
    private readonly FavoritoService _service;

    public FavoritosController(FavoritoService service)
    {
        _service = service;
    }

    // GET api/favoritos  -> [1, 3] (ids dos barbeiros favoritos)
    [HttpGet]
    public async Task<IActionResult> Listar()
    {
        return Ok(await _service.Listar(User.IdUsuario()));
    }

    // POST api/favoritos/3
    [HttpPost("{barbeiroId:int}")]
    public async Task<IActionResult> Adicionar(int barbeiroId)
    {
        await _service.Adicionar(User.IdUsuario(), barbeiroId);
        return Ok(await _service.Listar(User.IdUsuario()));
    }

    // DELETE api/favoritos/3
    [HttpDelete("{barbeiroId:int}")]
    public async Task<IActionResult> Remover(int barbeiroId)
    {
        await _service.Remover(User.IdUsuario(), barbeiroId);
        return Ok(await _service.Listar(User.IdUsuario()));
    }
}
