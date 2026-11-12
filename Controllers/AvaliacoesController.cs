using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SalaoBeleza.Common;
using SalaoBeleza.DTOs;
using SalaoBeleza.Services;

namespace SalaoBeleza.Controllers;

[ApiController]
[Route("api/avaliacoes")]
[Authorize(Roles = "Cliente")]
public class AvaliacoesController : ControllerBase
{
    private readonly AvaliacaoService _service;

    public AvaliacoesController(AvaliacaoService service)
    {
        _service = service;
    }

    // POST api/avaliacoes   { "agendamentoId": 5, "nota": 5, "comentario": "Otimo!" }
    [HttpPost]
    public async Task<IActionResult> Criar(AvaliarDTO dto)
    {
        return Ok(await _service.Criar(User.IdUsuario(), dto));
    }
}
