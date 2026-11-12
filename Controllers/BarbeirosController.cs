using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SalaoBeleza.Common;
using SalaoBeleza.DTOs;
using SalaoBeleza.Services;

namespace SalaoBeleza.Controllers;

[ApiController]
[Route("api/barbeiros")]
public class BarbeirosController : ControllerBase
{
    private readonly BarbeiroService _service;
    private readonly DisponibilidadeService _disponibilidade;
    private readonly RelatorioService _relatorios;

    public BarbeirosController(
        BarbeiroService service,
        DisponibilidadeService disponibilidade,
        RelatorioService relatorios)
    {
        _service = service;
        _disponibilidade = disponibilidade;
        _relatorios = relatorios;
    }

    // O administrador pode tudo; o barbeiro so mexe nos proprios dados.
    private bool PodeGerenciar(int barbeiroId)
    {
        return User.EhAdmin() || (User.EhBarbeiro() && User.IdBarbeiro() == barbeiroId);
    }

    private void ExigirGerenciar(int barbeiroId)
    {
        if (!PodeGerenciar(barbeiroId))
            throw new NegocioException("Voce nao tem permissao para isso.", 403);
    }

    // GET api/barbeiros  (publico; admin pode usar ?todos=true)
    [AllowAnonymous]
    [HttpGet]
    public async Task<IActionResult> Listar([FromQuery] bool todos = false)
    {
        return Ok(await _service.Listar(User, todos));
    }

    // GET api/barbeiros/3/perfil  (perfil publico)
    [AllowAnonymous]
    [HttpGet("{id:int}/perfil")]
    public async Task<IActionResult> Perfil(int id)
    {
        return Ok(await _service.Perfil(User, id));
    }

    // POST api/barbeiros
    [Authorize(Roles = "Administrador")]
    [HttpPost]
    public async Task<IActionResult> Criar(BarbeiroCriarDTO dto)
    {
        return Ok(await _service.Criar(dto));
    }

    // PUT api/barbeiros/3
    [Authorize(Roles = "Administrador")]
    [HttpPut("{id:int}")]
    public async Task<IActionResult> Editar(int id, BarbeiroEditarDTO dto)
    {
        return Ok(await _service.Editar(id, dto));
    }

    // PATCH api/barbeiros/3/ativo   { "ativo": false }
    [Authorize(Roles = "Administrador")]
    [HttpPatch("{id:int}/ativo")]
    public async Task<IActionResult> DefinirAtivo(int id, AtivoDTO dto)
    {
        await _service.DefinirAtivo(id, dto.Ativo);
        return Ok(new { ativo = dto.Ativo });
    }

    // DELETE api/barbeiros/3
    [Authorize(Roles = "Administrador")]
    [HttpDelete("{id:int}")]
    public async Task<IActionResult> Remover(int id)
    {
        await _service.Remover(id);
        return NoContent();
    }

    // PUT api/barbeiros/3/perfil   { "especialidades": "...", "bio": "..." }
    [Authorize]
    [HttpPut("{id:int}/perfil")]
    public async Task<IActionResult> EditarPerfil(int id, BarbeiroPerfilEditarDTO dto)
    {
        ExigirGerenciar(id);
        return Ok(await _service.EditarPerfil(User, id, dto));
    }

    // POST api/barbeiros/3/portfolio (multipart: arquivo + legenda)
    [Authorize]
    [HttpPost("{id:int}/portfolio")]
    public async Task<IActionResult> AdicionarFoto(int id, [FromForm] UploadArquivoDTO dto)
    {
        ExigirGerenciar(id);
        return Ok(await _service.AdicionarFoto(id, dto.Arquivo, dto.Legenda));
    }

    // DELETE api/barbeiros/3/portfolio/10
    [Authorize]
    [HttpDelete("{id:int}/portfolio/{fotoId:int}")]
    public async Task<IActionResult> RemoverFoto(int id, int fotoId)
    {
        ExigirGerenciar(id);
        await _service.RemoverFoto(id, fotoId);
        return NoContent();
    }

    // GET api/barbeiros/3/horarios
    [Authorize]
    [HttpGet("{id:int}/horarios")]
    public async Task<IActionResult> ListarHorarios(int id)
    {
        ExigirGerenciar(id);
        return Ok(await _disponibilidade.ListarHorarios(id));
    }

    // PUT api/barbeiros/3/horarios   [ { diaSemana, trabalha, inicio, fim, almocoInicio, almocoFim } x 7 ]
    [Authorize]
    [HttpPut("{id:int}/horarios")]
    public async Task<IActionResult> SalvarHorarios(int id, List<HorarioDTO> dias)
    {
        ExigirGerenciar(id);
        return Ok(await _disponibilidade.SalvarHorarios(id, dias));
    }

    // GET api/barbeiros/3/folgas
    [Authorize]
    [HttpGet("{id:int}/folgas")]
    public async Task<IActionResult> ListarFolgas(int id)
    {
        ExigirGerenciar(id);
        return Ok(await _disponibilidade.ListarFolgas(id));
    }

    // POST api/barbeiros/3/folgas   { dataInicio, dataFim, motivo }
    [Authorize]
    [HttpPost("{id:int}/folgas")]
    public async Task<IActionResult> CriarFolga(int id, FolgaCriarDTO dto)
    {
        ExigirGerenciar(id);
        return Ok(await _disponibilidade.CriarFolga(id, dto));
    }

    // DELETE api/barbeiros/3/folgas/7
    [Authorize]
    [HttpDelete("{id:int}/folgas/{folgaId:int}")]
    public async Task<IActionResult> RemoverFolga(int id, int folgaId)
    {
        ExigirGerenciar(id);
        await _disponibilidade.RemoverFolga(id, folgaId);
        return NoContent();
    }

    // GET api/barbeiros/3/ganhos?mes=2026-10
    [Authorize]
    [HttpGet("{id:int}/ganhos")]
    public async Task<IActionResult> Ganhos(int id, [FromQuery] string? mes)
    {
        ExigirGerenciar(id);
        return Ok(await _relatorios.Ganhos(id, mes));
    }
}
