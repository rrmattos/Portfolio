using System.ComponentModel.DataAnnotations;

namespace Portfolio.Web.Data;

public class Project
{
    public int Id { get; set; }

    [Required(ErrorMessage = "Informe o título.")]
    [StringLength(120)]
    public string Title { get; set; } = string.Empty;

    [Required(ErrorMessage = "Informe um resumo curto.")]
    [StringLength(280)]
    public string Summary { get; set; } = string.Empty;

    [StringLength(8000)]
    public string? Description { get; set; }

    /// <summary>Relative URL of the uploaded cover image (e.g. /uploads/projects/abc.webp).</summary>
    [StringLength(300)]
    public string? ImageUrl { get; set; }

    /// <summary>Comma separated list of technologies.</summary>
    [StringLength(500)]
    public string? Technologies { get; set; }

    [Url(ErrorMessage = "URL inválida.")]
    [StringLength(300)]
    public string? RepositoryUrl { get; set; }

    /// <summary>Toggle para exibir ou ocultar o botão do código fonte (GitHub) no site público.</summary>
    public bool ShowRepository { get; set; } = true;

    [Url(ErrorMessage = "URL inválida.")]
    [StringLength(300)]
    public string? LiveUrl { get; set; }

    public bool IsFeatured { get; set; }

    public bool IsPublished { get; set; } = true;

    public int DisplayOrder { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public DateTime? UpdatedAt { get; set; }

    /// <summary>Galeria de imagens/vídeos do projeto (ordenada por DisplayOrder).</summary>
    public List<ProjectMedia> Medias { get; set; } = [];

    public IEnumerable<string> TechList => SplitTags(Technologies);

    /// <summary>Capa a exibir: a imagem de capa ou, se não houver, a primeira imagem da galeria.</summary>
    public string? DisplayCoverUrl => !string.IsNullOrWhiteSpace(ImageUrl)
        ? ImageUrl
        : Medias.OrderBy(m => m.DisplayOrder).FirstOrDefault(m => m.MediaType == MediaType.Image)?.MediaUrl;

    internal static IEnumerable<string> SplitTags(string? value) =>
        string.IsNullOrWhiteSpace(value)
            ? []
            : value.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
}

public enum MediaType
{
    Image = 0,
    Video = 1,
}

public class ProjectMedia
{
    public int Id { get; set; }

    public int ProjectId { get; set; }

    [Required, StringLength(300)]
    public string MediaUrl { get; set; } = string.Empty;

    public MediaType MediaType { get; set; }

    public int DisplayOrder { get; set; }
}

public class Experience
{
    public int Id { get; set; }

    [Required(ErrorMessage = "Informe a empresa.")]
    [StringLength(120)]
    public string Company { get; set; } = string.Empty;

    [Required(ErrorMessage = "Informe o cargo.")]
    [StringLength(120)]
    public string Role { get; set; } = string.Empty;

    [StringLength(120)]
    public string? Location { get; set; }

    [Required(ErrorMessage = "Informe a data de início.")]
    public DateTime StartDate { get; set; } = DateTime.Today;

    public DateTime? EndDate { get; set; }

    public bool IsCurrent { get; set; }

    [StringLength(4000)]
    public string? Description { get; set; }

    [StringLength(500)]
    public string? Technologies { get; set; }

    public int DisplayOrder { get; set; }

    public IEnumerable<string> TechList => Project.SplitTags(Technologies);
}

public class ContactMessage
{
    public int Id { get; set; }

    [Required(ErrorMessage = "Informe seu nome.")]
    [StringLength(100)]
    public string Name { get; set; } = string.Empty;

    [Required(ErrorMessage = "Informe seu e-mail.")]
    [EmailAddress(ErrorMessage = "E-mail inválido.")]
    [StringLength(150)]
    public string Email { get; set; } = string.Empty;

    [StringLength(150)]
    public string? Subject { get; set; }

    [Required(ErrorMessage = "Escreva uma mensagem.")]
    [StringLength(4000, MinimumLength = 10, ErrorMessage = "A mensagem deve ter entre 10 e 4000 caracteres.")]
    public string Message { get; set; } = string.Empty;

    public bool IsRead { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}

public class AdminUser
{
    public int Id { get; set; }

    [Required, StringLength(150)]
    public string Email { get; set; } = string.Empty;

    [Required, StringLength(300)]
    public string PasswordHash { get; set; } = string.Empty;

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}
