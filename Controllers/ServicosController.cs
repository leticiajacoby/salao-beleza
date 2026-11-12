using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SalaoBeleza.Common;
using SalaoBeleza.DTOs;
using SalaoBeleza.Services;

namespace SalaoBeleza.Controllers;

[ApiController]
[Route("api/servicos")]
public class ServicosController : ControllerBase
{
    private readonly ServicoService _service;

    public ServicosController(ServicoService service)
    {
        _service = service;
    }

    // GET api/servicos  (publico; admin pode usar ?todos=true para ver os desativados)
    [AllowAnonymous]
    [HttpGet]
    public async Task<IActionResult> Listar([FromQuery] bool todos = false)
    {
        var incluirInativos = todos && User.EhAdmin();
        return Ok(await _service.Listar(incluirInativos));
    }

    // GET api/servicos/5
    [AllowAnonymous]
    [HttpGet("{id:int}")]
    public async Task<IActionResult> BuscarPorId(int id)
    {
        var servico = await _service.BuscarPorId(id);
        if (servico == null)
            return NotFound(new { mensagem = "Servico nao encontrado." });

        return Ok(servico);
    }

    // POST api/servicos
    [Authorize(Roles = "Administrador")]
    [HttpPost]
    public async Task<IActionResult> Criar(ServicoSalvarDTO dto)
    {
        return Ok(await _service.Criar(dto));
    }

    // PUT api/servicos/5
    [Authorize(Roles = "Administrador")]
    [HttpPut("{id:int}")]
    public async Task<IActionResult> Editar(int id, ServicoSalvarDTO dto)
    {
        return Ok(await _service.Editar(id, dto));
    }

    // PATCH api/servicos/5/ativo   { "ativo": false }
    [Authorize(Roles = "Administrador")]
    [HttpPatch("{id:int}/ativo")]
    public async Task<IActionResult> DefinirAtivo(int id, AtivoDTO dto)
    {
        return Ok(await _service.DefinirAtivo(id, dto.Ativo));
    }

    // DELETE api/servicos/5
    [Authorize(Roles = "Administrador")]
    [HttpDelete("{id:int}")]
    public async Task<IActionResult> Remover(int id)
    {
        await _service.Remover(id);
        return NoContent();
    }
}
