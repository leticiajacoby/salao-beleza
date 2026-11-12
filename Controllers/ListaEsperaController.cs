using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SalaoBeleza.Common;
using SalaoBeleza.DTOs;
using SalaoBeleza.Services;

namespace SalaoBeleza.Controllers;

[ApiController]
[Route("api/lista-espera")]
[Authorize(Roles = "Cliente,Administrador")]
public class ListaEsperaController : ControllerBase
{
    private readonly ListaEsperaService _service;

    public ListaEsperaController(ListaEsperaService service)
    {
        _service = service;
    }

    // GET api/lista-espera
    [HttpGet]
    public async Task<IActionResult> Listar()
    {
        return Ok(await _service.Listar(User));
    }

    // POST api/lista-espera   { "barbeiroId": 1, "data": "2026-10-20", "servicoIds": [1,2] }
    [Authorize(Roles = "Cliente")]
    [HttpPost]
    public async Task<IActionResult> Criar(ListaEsperaCriarDTO dto)
    {
        return Ok(await _service.Criar(User.IdUsuario(), dto));
    }

    // DELETE api/lista-espera/4
    [HttpDelete("{id:int}")]
    public async Task<IActionResult> Remover(int id)
    {
        await _service.Remover(User, id);
        return NoContent();
    }
}
