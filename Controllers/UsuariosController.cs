using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SalaoBeleza.Common;
using SalaoBeleza.DTOs;
using SalaoBeleza.Services;

namespace SalaoBeleza.Controllers;

[ApiController]
[Route("api/usuarios")]
[Authorize]
public class UsuariosController : ControllerBase
{
    private readonly UsuarioService _service;
    private readonly IEmailService _email;
    private readonly IWebHostEnvironment _env;
    private readonly IConfiguration _config;

    public UsuariosController(UsuarioService service, IEmailService email, IWebHostEnvironment env, IConfiguration config)
    {
        _service = service;
        _email = email;
        _env = env;
        _config = config;
    }

    // POST api/usuarios/registrar
    [AllowAnonymous]
    [HttpPost("registrar")]
    public async Task<IActionResult> Registrar(UsuarioRegistrarDTO dto)
    {
        return Ok(await _service.Registrar(dto));
    }

    // POST api/usuarios/login
    [AllowAnonymous]
    [HttpPost("login")]
    public async Task<IActionResult> Login(UsuarioLoginDTO dto)
    {
        return Ok(await _service.Login(dto));
    }

    // POST api/usuarios/esqueci-senha
    // A resposta e sempre a mesma, exista o e-mail ou nao (nao revela quem tem conta).
    [AllowAnonymous]
    [HttpPost("esqueci-senha")]
    public async Task<IActionResult> EsqueciSenha(EsqueciSenhaDTO dto)
    {
        var urlBase = _config["App:UrlBase"];
        if (string.IsNullOrWhiteSpace(urlBase))
            urlBase = Request.Scheme + "://" + Request.Host;

        var link = await _service.EsqueciSenha(dto.Email, urlBase);

        // Em desenvolvimento, sem SMTP configurado, devolve o link para facilitar o teste.
        string? linkDev = null;
        if (_env.IsDevelopment() && !_email.Configurado)
            linkDev = link;

        return Ok(new
        {
            mensagem = "Se o e-mail estiver cadastrado, enviamos um link para redefinir a senha. Ele vale por 30 minutos.",
            linkDev
        });
    }

    // POST api/usuarios/redefinir-senha
    [AllowAnonymous]
    [HttpPost("redefinir-senha")]
    public async Task<IActionResult> RedefinirSenha(RedefinirSenhaDTO dto)
    {
        await _service.RedefinirSenha(dto);
        return Ok(new { mensagem = "Senha alterada com sucesso. Entre com a nova senha." });
    }

    // GET api/usuarios/me
    [HttpGet("me")]
    public async Task<IActionResult> Me()
    {
        return Ok(await _service.Obter(User.IdUsuario()));
    }

    // PUT api/usuarios/me
    [HttpPut("me")]
    public async Task<IActionResult> AtualizarPerfil(AtualizarPerfilDTO dto)
    {
        return Ok(await _service.AtualizarPerfil(User.IdUsuario(), dto));
    }

    // PUT api/usuarios/me/senha
    [HttpPut("me/senha")]
    public async Task<IActionResult> AlterarSenha(AlterarSenhaDTO dto)
    {
        await _service.AlterarSenha(User.IdUsuario(), dto);
        return Ok(new { mensagem = "Senha alterada com sucesso." });
    }

    // POST api/usuarios/me/foto (multipart/form-data, campo "arquivo")
    [HttpPost("me/foto")]
    public async Task<IActionResult> SalvarFoto([FromForm] UploadArquivoDTO dto)
    {
        return Ok(await _service.SalvarFoto(User.IdUsuario(), dto.Arquivo));
    }

    // DELETE api/usuarios/me/foto
    [HttpDelete("me/foto")]
    public async Task<IActionResult> RemoverFoto()
    {
        return Ok(await _service.RemoverFoto(User.IdUsuario()));
    }
}
