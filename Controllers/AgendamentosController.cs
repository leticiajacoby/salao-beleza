using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SalaoBeleza.Common;
using SalaoBeleza.DTOs;
using SalaoBeleza.Services;

namespace SalaoBeleza.Controllers;

[ApiController]
[Route("api/agendamentos")]
[Authorize]
public class AgendamentosController : ControllerBase
{
    private readonly AgendamentoService _service;
    private readonly DisponibilidadeService _disponibilidade;

    public AgendamentosController(AgendamentoService service, DisponibilidadeService disponibilidade)
    {
        _service = service;
        _disponibilidade = disponibilidade;
    }

    // GET api/agendamentos?de=2026-10-01&ate=2026-11-01&barbeiroId=1&status=Agendado
    // Cada perfil ve apenas o que e seu (cliente: os dele; barbeiro: os dele; admin: todos).
    [HttpGet]
    public async Task<IActionResult> Listar(
        [FromQuery] DateTime? de,
        [FromQuery] DateTime? ate,
        [FromQuery] int? barbeiroId,
        [FromQuery] string? status)
    {
        return Ok(await _service.Listar(User, de, ate, barbeiroId, status));
    }

    // GET api/agendamentos/5
    [HttpGet("{id:int}")]
    public async Task<IActionResult> Obter(int id)
    {
        return Ok(await _service.Obter(User, id));
    }

    // GET api/agendamentos/horarios-livres?barbeiroId=1&data=2026-10-20&servicoIds=1,2
    // (ou duracao=60; ignorarAgendamentoId=5 ao reagendar)
    [HttpGet("horarios-livres")]
    public async Task<IActionResult> HorariosLivres(
        [FromQuery] int barbeiroId,
        [FromQuery] DateTime data,
        [FromQuery] string? servicoIds,
        [FromQuery] int? duracao,
        [FromQuery] int? ignorarAgendamentoId,
        [FromServices] ServicoService servicos)
    {
        var minutos = duracao ?? 0;

        if (minutos <= 0)
        {
            var ids = new List<int>();

            foreach (var parte in (servicoIds ?? "").Split(',', StringSplitOptions.RemoveEmptyEntries))
            {
                if (int.TryParse(parte.Trim(), out var id))
                    ids.Add(id);
            }

            foreach (var id in ids.Distinct())
            {
                var servico = await servicos.BuscarPorId(id);
                if (servico != null)
                    minutos += servico.DuracaoMinutos;
            }
        }

        if (minutos <= 0)
            throw new NegocioException("Informe os servicos (servicoIds) ou a duracao em minutos.");

        var horarios = await _disponibilidade.HorariosLivres(barbeiroId, data, minutos, ignorarAgendamentoId);

        return Ok(horarios);
    }

    // POST api/agendamentos/simular
    [Authorize(Roles = "Cliente,Administrador")]
    [HttpPost("simular")]
    public async Task<IActionResult> Simular(SimularDTO dto)
    {
        return Ok(await _service.Simular(User, dto));
    }

    // POST api/agendamentos
    [Authorize(Roles = "Cliente,Administrador")]
    [HttpPost]
    public async Task<IActionResult> Criar(AgendamentoCriarDTO dto)
    {
        return Ok(await _service.Criar(User, dto));
    }

    // PUT api/agendamentos/5/reagendar   { "dataHora": "2026-10-21T10:00", "barbeiroId": 2 }
    [HttpPut("{id:int}/reagendar")]
    public async Task<IActionResult> Reagendar(int id, ReagendarDTO dto)
    {
        return Ok(await _service.Reagendar(User, id, dto));
    }

    // PUT api/agendamentos/5/cancelar
    [HttpPut("{id:int}/cancelar")]
    public async Task<IActionResult> Cancelar(int id)
    {
        return Ok(await _service.Cancelar(User, id));
    }

    // PUT api/agendamentos/5/status   { "status": "Concluido" | "Faltou" }
    [Authorize(Roles = "Barbeiro,Administrador")]
    [HttpPut("{id:int}/status")]
    public async Task<IActionResult> AtualizarStatus(int id, StatusDTO dto)
    {
        return Ok(await _service.AtualizarStatus(User, id, dto));
    }

    // DELETE api/agendamentos/5  (apaga de vez - so administrador)
    [Authorize(Roles = "Administrador")]
    [HttpDelete("{id:int}")]
    public async Task<IActionResult> Remover(int id)
    {
        await _service.Remover(id);
        return NoContent();
    }
}
