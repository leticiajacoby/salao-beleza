using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SalaoBeleza.Services;

namespace SalaoBeleza.Controllers;

[ApiController]
[Route("api/clientes")]
[Authorize(Roles = "Administrador")]
public class ClientesController : ControllerBase
{
    private readonly ClienteService _service;

    public ClientesController(ClienteService service)
    {
        _service = service;
    }

    // GET api/clientes
    [HttpGet]
    public async Task<IActionResult> Listar()
    {
        return Ok(await _service.Listar());
    }

    // DELETE api/clientes/5
    [HttpDelete("{id:int}")]
    public async Task<IActionResult> Remover(int id)
    {
        await _service.Remover(id);
        return NoContent();
    }
}
