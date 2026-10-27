namespace Portfolio.Web.Services;

/// <summary>
/// Stores project images under wwwroot/uploads/projects and validates type/size/signature.
/// </summary>
public class ImageStorageService(IWebHostEnvironment env, ILogger<ImageStorageService> logger)
{
    public const long MaxFileSize = 5 * 1024 * 1024; // 5 MB
    public const string AcceptAttribute = ".jpg,.jpeg,.png,.webp,.gif";

    private const string UploadFolder = "uploads/projects";

    private static readonly Dictionary<string, string> ContentTypeToExtension = new(StringComparer.OrdinalIgnoreCase)
    {
        ["image/jpeg"] = ".jpg",
        ["image/png"] = ".png",
        ["image/webp"] = ".webp",
        ["image/gif"] = ".gif",
    };

    /// <summary>Saves an image stream and returns its public relative URL.</summary>
    public async Task<string> SaveAsync(Stream content, string contentType, CancellationToken ct = default)
    {
        if (!ContentTypeToExtension.TryGetValue(contentType, out var extension))
            throw new InvalidOperationException("Formato não suportado. Use JPG, PNG, WEBP ou GIF.");

        using var buffer = new MemoryStream();
        await content.CopyToAsync(buffer, ct);

        if (buffer.Length == 0)
            throw new InvalidOperationException("O arquivo está vazio.");
        if (buffer.Length > MaxFileSize)
            throw new InvalidOperationException("A imagem deve ter no máximo 5 MB.");
        if (!HasValidSignature(buffer.GetBuffer(), extension))
            throw new InvalidOperationException("O conteúdo do arquivo não corresponde a uma imagem válida.");

        var folder = Path.Combine(WebRoot, UploadFolder);
        Directory.CreateDirectory(folder);

        var fileName = $"{Guid.NewGuid():N}{extension}";
        buffer.Position = 0;
        await using (var file = File.Create(Path.Combine(folder, fileName)))
            await buffer.CopyToAsync(file, ct);

        return $"/{UploadFolder}/{fileName}";
    }

    public void Delete(string? relativeUrl)
    {
        if (string.IsNullOrWhiteSpace(relativeUrl) || !relativeUrl.StartsWith($"/{UploadFolder}/"))
            return;

        var fileName = Path.GetFileName(relativeUrl);
        var path = Path.Combine(WebRoot, UploadFolder, fileName);
        try
        {
            if (File.Exists(path)) File.Delete(path);
        }
        catch (IOException ex)
        {
            logger.LogWarning(ex, "Não foi possível remover a imagem {Path}", path);
        }
    }

    private string WebRoot => env.WebRootPath ?? Path.Combine(env.ContentRootPath, "wwwroot");

    // Magic-number check so a renamed non-image file is rejected.
    private static bool HasValidSignature(byte[] data, string extension) => extension switch
    {
        ".jpg" => data.Length > 3 && data[0] == 0xFF && data[1] == 0xD8 && data[2] == 0xFF,
        ".png" => data.Length > 8 && data[0] == 0x89 && data[1] == 0x50 && data[2] == 0x4E && data[3] == 0x47,
        ".gif" => data.Length > 6 && data[0] == 0x47 && data[1] == 0x49 && data[2] == 0x46,
        ".webp" => data.Length > 12 && data[0] == 0x52 && data[1] == 0x49 && data[2] == 0x46 && data[3] == 0x46
                   && data[8] == 0x57 && data[9] == 0x45 && data[10] == 0x42 && data[11] == 0x50,
        _ => false,
    };
}
