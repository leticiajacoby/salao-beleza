using System.Net;
using System.Net.Mail;

namespace SalaoBeleza.Services;

public interface IEmailService
{
    bool Configurado { get; }
    Task EnviarAsync(string para, string assunto, string corpoHtml);
}

// Envia por SMTP quando a secao "Smtp" do appsettings estiver preenchida.
// Sem SMTP, o e-mail e apenas escrito no console da API (bom para testar).
public class EmailService : IEmailService
{
    private readonly IConfiguration _config;
    private readonly ILogger<EmailService> _log;

    public EmailService(IConfiguration config, ILogger<EmailService> log)
    {
        _config = config;
        _log = log;
    }

    public bool Configurado => !string.IsNullOrWhiteSpace(_config["Smtp:Host"]);

    public async Task EnviarAsync(string para, string assunto, string corpoHtml)
    {
        if (!Configurado)
        {
            _log.LogWarning("SMTP nao configurado. E-mail NAO enviado. Para: {Para} | Assunto: {Assunto} | Conteudo: {Corpo}", para, assunto, corpoHtml);
            return;
        }

        try
        {
            var porta = int.TryParse(_config["Smtp:Porta"], out var p) ? p : 587;
            var usuario = _config["Smtp:Usuario"];
            var senha = _config["Smtp:Senha"];
            var de = _config["Smtp:De"] ?? usuario ?? "nao-responda@salaobeleza.com";

            using var cliente = new SmtpClient(_config["Smtp:Host"], porta);
            cliente.EnableSsl = !string.Equals(_config["Smtp:Ssl"], "false", StringComparison.OrdinalIgnoreCase);

            if (!string.IsNullOrWhiteSpace(usuario))
                cliente.Credentials = new NetworkCredential(usuario, senha);

            using var mensagem = new MailMessage(de, para, assunto, corpoHtml);
            mensagem.IsBodyHtml = true;

            await cliente.SendMailAsync(mensagem);
        }
        catch (Exception ex)
        {
            _log.LogError(ex, "Falha ao enviar e-mail para {Para}", para);
        }
    }
}
