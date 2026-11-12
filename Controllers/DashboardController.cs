using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SalaoBeleza.Services;

namespace SalaoBeleza.Controllers;

[ApiController]
[Route("api/dashboard")]
[Authorize(Roles = "Administrador")]
public class DashboardController : ControllerBase
{
    private readonly RelatorioService _service;

    public DashboardController(RelatorioService service)
    {
        _service = service;
    }

    // GET api/dashboard?meses=6
    [HttpGet]
    public async Task<IActionResult> Obter([FromQuery] int meses = 6)
    {
        return Ok(await _service.Dashboard(meses));
    }
}
