using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SalaoBeleza.DTOs;
using SalaoBeleza.Services;

namespace SalaoBeleza.Controllers;

[ApiController]
[Route("api/cupons")]
[Authorize]
public class CuponsController : ControllerBase
{
    private readonly CupomService _service;

    public CuponsController(CupomService service)
    {
        _service = service;
    }

    // GET api/cupons/validar?codigo=BEMVINDO10&valor=80   (qualquer usuario logado)
    [HttpGet("validar")]
    public async Task<IActionResult> Validar([FromQuery] string? codigo, [FromQuery] decimal valor)
    {
        return Ok(await _service.Validar(codigo, valor));
    }

    [Authorize(Roles = "Administrador")]
    [HttpGet]
    public async Task<IActionResult> Listar()
    {
        return Ok(await _service.Listar());
    }

    [Authorize(Roles = "Administrador")]
    [HttpPost]
    public async Task<IActionResult> Criar(CupomSalvarDTO dto)
    {
        return Ok(await _service.Criar(dto));
    }

    [Authorize(Roles = "Administrador")]
    [HttpPut("{id:int}")]
    public async Task<IActionResult> Editar(int id, CupomSalvarDTO dto)
    {
        return Ok(await _service.Editar(id, dto));
    }

    [Authorize(Roles = "Administrador")]
    [HttpPatch("{id:int}/ativo")]
    public async Task<IActionResult> DefinirAtivo(int id, AtivoDTO dto)
    {
        return Ok(await _service.DefinirAtivo(id, dto.Ativo));
    }

    [Authorize(Roles = "Administrador")]
    [HttpDelete("{id:int}")]
    public async Task<IActionResult> Remover(int id)
    {
        await _service.Remover(id);
        return NoContent();
    }
}
