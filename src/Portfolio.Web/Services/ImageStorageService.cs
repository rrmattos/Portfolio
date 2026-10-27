using Portfolio.Web.Data;

namespace Portfolio.Web.Services;

/// <summary>
/// Stores project images/videos under wwwroot/uploads/projects and validates type/size/signature.
/// </summary>
public class ImageStorageService(IWebHostEnvironment env, ILogger<ImageStorageService> logger)
{
    public const long MaxFileSize = 5 * 1024 * 1024; // 5 MB
    public const int MaxGalleryItems = 12;
    public const string AcceptAttribute = ".jpg,.jpeg,.png,.webp,.gif";
    public const string MediaAcceptAttribute = ".jpg,.jpeg,.png,.webp,.gif,.mp4,.webm";

    private const string UploadFolder = "uploads/projects";

    private static readonly Dictionary<string, (string Extension, MediaType Type)> AllowedTypes = new(StringComparer.OrdinalIgnoreCase)
    {
        ["image/jpeg"] = (".jpg", MediaType.Image),
        ["image/png"] = (".png", MediaType.Image),
        ["image/webp"] = (".webp", MediaType.Image),
        ["image/gif"] = (".gif", MediaType.Image),
        ["video/mp4"] = (".mp4", MediaType.Video),
        ["video/webm"] = (".webm", MediaType.Video),
    };

    /// <summary>Saves an image (cover) and returns its public relative URL.</summary>
    public async Task<string> SaveAsync(Stream content, string contentType, CancellationToken ct = default)
    {
        var (url, type) = await SaveMediaAsync(content, contentType, ct);
        if (type != MediaType.Image)
        {
            Delete(url);
            throw new InvalidOperationException("A capa deve ser uma imagem (JPG, PNG, WEBP ou GIF).");
        }
        return url;
    }

    /// <summary>Saves an image or video and returns its public relative URL and media type.</summary>
    public async Task<(string Url, MediaType Type)> SaveMediaAsync(Stream content, string contentType, CancellationToken ct = default)
    {
        if (!AllowedTypes.TryGetValue(contentType, out var info))
            throw new InvalidOperationException("Formato não suportado. Use JPG, PNG, WEBP, GIF, MP4 ou WEBM.");

        using var buffer = new MemoryStream();
        await content.CopyToAsync(buffer, ct);

        if (buffer.Length == 0)
            throw new InvalidOperationException("O arquivo está vazio.");
        if (buffer.Length > MaxFileSize)
            throw new InvalidOperationException("O arquivo deve ter no máximo 5 MB.");
        if (!HasValidSignature(buffer.GetBuffer(), (int)buffer.Length, info.Extension))
            throw new InvalidOperationException("O conteúdo do arquivo não corresponde ao formato informado.");

        var folder = Path.Combine(WebRoot, UploadFolder);
        Directory.CreateDirectory(folder);

        var fileName = $"{Guid.NewGuid():N}{info.Extension}";
        buffer.Position = 0;
        await using (var file = File.Create(Path.Combine(folder, fileName)))
            await buffer.CopyToAsync(file, ct);

        return ($"/{UploadFolder}/{fileName}", info.Type);
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
            logger.LogWarning(ex, "Não foi possível remover o arquivo {Path}", path);
        }
    }

    private string WebRoot => env.WebRootPath ?? Path.Combine(env.ContentRootPath, "wwwroot");

    // Magic-number check so a renamed file of another type is rejected.
    private static bool HasValidSignature(byte[] d, int length, string extension) => extension switch
    {
        ".jpg" => length > 3 && d[0] == 0xFF && d[1] == 0xD8 && d[2] == 0xFF,
        ".png" => length > 8 && d[0] == 0x89 && d[1] == 0x50 && d[2] == 0x4E && d[3] == 0x47,
        ".gif" => length > 6 && d[0] == 0x47 && d[1] == 0x49 && d[2] == 0x46,
        ".webp" => length > 12 && d[0] == 0x52 && d[1] == 0x49 && d[2] == 0x46 && d[3] == 0x46
                   && d[8] == 0x57 && d[9] == 0x45 && d[10] == 0x42 && d[11] == 0x50,
        // MP4/ISO-BMFF: "ftyp" box at offset 4.
        ".mp4" => length > 12 && d[4] == 0x66 && d[5] == 0x74 && d[6] == 0x79 && d[7] == 0x70,
        // WebM/Matroska: EBML header 1A 45 DF A3.
        ".webm" => length > 4 && d[0] == 0x1A && d[1] == 0x45 && d[2] == 0xDF && d[3] == 0xA3,
        _ => false,
    };
}
