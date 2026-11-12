using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SalaoBeleza.Common;
using SalaoBeleza.Services;

namespace SalaoBeleza.Controllers;

[ApiController]
[Route("api/notificacoes")]
[Authorize]
public class NotificacoesController : ControllerBase
{
    private readonly NotificacaoService _service;

    public NotificacoesController(NotificacaoService service)
    {
        _service = service;
    }

    // GET api/notificacoes -> { naoLidas, itens: [...] }
    [HttpGet]
    public async Task<IActionResult> Listar()
    {
        return Ok(await _service.Listar(User.IdUsuario()));
    }

    // PUT api/notificacoes/lidas  (marca todas)
    [HttpPut("lidas")]
    public async Task<IActionResult> MarcarTodasLidas()
    {
        await _service.MarcarTodasLidas(User.IdUsuario());
        return Ok(await _service.Listar(User.IdUsuario()));
    }

    // PUT api/notificacoes/8/lida
    [HttpPut("{id:int}/lida")]
    public async Task<IActionResult> MarcarLida(int id)
    {
        await _service.MarcarLida(User.IdUsuario(), id);
        return Ok(await _service.Listar(User.IdUsuario()));
    }
}
