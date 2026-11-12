using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SalaoBeleza.Common;
using SalaoBeleza.Services;

namespace SalaoBeleza.Controllers;

[ApiController]
[Route("api/fidelidade")]
[Authorize(Roles = "Cliente")]
public class FidelidadeController : ControllerBase
{
    private readonly FidelidadeService _service;

    public FidelidadeController(FidelidadeService service)
    {
        _service = service;
    }

    // GET api/fidelidade
    [HttpGet]
    public async Task<IActionResult> Resumo()
    {
        return Ok(await _service.Resumo(User.IdUsuario()));
    }
}
