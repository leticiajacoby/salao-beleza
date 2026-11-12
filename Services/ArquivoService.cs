namespace SalaoBeleza.Services;

// Salva imagens enviadas (foto de perfil e portfolio) em wwwroot/uploads.
public class ArquivoService
{
    private const long TamanhoMaximo = 3 * 1024 * 1024;
    private readonly IWebHostEnvironment _env;

    public ArquivoService(IWebHostEnvironment env)
    {
        _env = env;
    }

    private string Raiz()
    {
        var web = _env.WebRootPath;
        if (string.IsNullOrWhiteSpace(web))
            web = Path.Combine(_env.ContentRootPath, "wwwroot");
        return web;
    }

    public async Task<string> SalvarImagem(IFormFile? arquivo, string pasta)
    {
        if (arquivo == null || arquivo.Length == 0)
            throw new Common.NegocioException("Selecione uma imagem.");

        if (arquivo.Length > TamanhoMaximo)
            throw new Common.NegocioException("A imagem deve ter no maximo 3 MB.");

        byte[] bytes;
        using (var memoria = new MemoryStream())
        {
            await arquivo.CopyToAsync(memoria);
            bytes = memoria.ToArray();
        }

        var extensao = DetectarExtensao(bytes);
        if (extensao == null)
            throw new Common.NegocioException("Formato invalido. Envie uma imagem JPG, PNG ou WEBP.");

        var nome = Guid.NewGuid().ToString("N") + extensao;
        var diretorio = Path.Combine(Raiz(), "uploads", pasta);
        Directory.CreateDirectory(diretorio);

        await File.WriteAllBytesAsync(Path.Combine(diretorio, nome), bytes);

        return "/uploads/" + pasta + "/" + nome;
    }

    public void Remover(string? url)
    {
        if (string.IsNullOrWhiteSpace(url) || !url.StartsWith("/uploads/"))
            return;

        try
        {
            var raizUploads = Path.GetFullPath(Path.Combine(Raiz(), "uploads"));
            var relativo = url.TrimStart('/').Replace('/', Path.DirectorySeparatorChar);
            var completo = Path.GetFullPath(Path.Combine(Raiz(), relativo));

            // Nunca apaga nada fora da pasta de uploads.
            if (!completo.StartsWith(raizUploads))
                return;

            if (File.Exists(completo))
                File.Delete(completo);
        }
        catch
        {
            // Falha ao apagar arquivo nao deve quebrar a operacao.
        }
    }

    // Confere o conteudo real do arquivo (nao confia na extensao enviada).
    private static string? DetectarExtensao(byte[] b)
    {
        if (b.Length > 3 && b[0] == 0xFF && b[1] == 0xD8 && b[2] == 0xFF)
            return ".jpg";

        if (b.Length > 8 && b[0] == 0x89 && b[1] == 0x50 && b[2] == 0x4E && b[3] == 0x47 &&
            b[4] == 0x0D && b[5] == 0x0A && b[6] == 0x1A && b[7] == 0x0A)
            return ".png";

        if (b.Length > 12 && b[0] == 0x52 && b[1] == 0x49 && b[2] == 0x46 && b[3] == 0x46 &&
            b[8] == 0x57 && b[9] == 0x45 && b[10] == 0x42 && b[11] == 0x50)
            return ".webp";

        return null;
    }
}
