using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

namespace Portfolio.Web.Data;

/// <summary>Applies migrations, creates the first admin user and (optionally) sample content.</summary>
public static class DbInitializer
{
    public static async Task InitializeAsync(IServiceProvider services)
    {
        using var scope = services.CreateScope();
        var sp = scope.ServiceProvider;
        var config = sp.GetRequiredService<IConfiguration>();
        var logger = sp.GetRequiredService<ILoggerFactory>().CreateLogger("DbInitializer");
        var db = await sp.GetRequiredService<IDbContextFactory<AppDbContext>>().CreateDbContextAsync();

        try
        {
            await db.Database.MigrateAsync();
        }
        catch (Exception ex)
        {
            logger.LogError(ex,
                "Não foi possível conectar/migrar o MySQL. Verifique 'ConnectionStrings:DefaultConnection'. " +
                "O site continuará iniciando, mas páginas que usam o banco falharão.");
            return;
        }

        await SeedAdminAsync(db, sp.GetRequiredService<IPasswordHasher<AdminUser>>(), config, logger);

        if (config.GetValue<bool>("Seed:SampleData"))
            await SeedSampleDataAsync(db);

        await db.DisposeAsync();
    }

    private static async Task SeedAdminAsync(AppDbContext db, IPasswordHasher<AdminUser> hasher,
        IConfiguration config, ILogger logger)
    {
        if (await db.AdminUsers.AnyAsync()) return;

        var email = config["Admin:Email"]?.Trim().ToLowerInvariant();
        var password = config["Admin:Password"];
        if (string.IsNullOrWhiteSpace(email) || string.IsNullOrWhiteSpace(password))
        {
            logger.LogWarning("Nenhum administrador cadastrado. Defina 'Admin:Email' e 'Admin:Password' para criar o primeiro.");
            return;
        }

        var admin = new AdminUser { Email = email };
        admin.PasswordHash = hasher.HashPassword(admin, password);
        db.AdminUsers.Add(admin);
        await db.SaveChangesAsync();
        logger.LogInformation("Administrador inicial criado: {Email}", email);
    }

    private static async Task SeedSampleDataAsync(AppDbContext db)
    {
        if (!await db.Projects.AnyAsync())
        {
            db.Projects.AddRange(
                new Project
                {
                    Title = "Experiência Gamificada XR / Treinamento",
                    Summary = "Aplicações interativas e gamificadas para simulação e treinamento em VR, AR e WebGL.",
                    Description = "Desenvolvimento de sistemas interativos com mecânicas de gameplay imersivas, suporte a múltiplos dispositivos (Desktop, Mobile, VR/AR/MR) e integração de dados para acompanhamento de métricas de aprendizagem.",
                    Technologies = "Unity3d, C#, VR, AR, MR, WebGL, Clean Code",
                    RepositoryUrl = "https://github.com/rrmattos",
                    IsFeatured = true,
                    DisplayOrder = 1,
                },
                new Project
                {
                    Title = "Sistema Multiplayer em Tempo Real",
                    Summary = "Arquitetura e sincronização de partidas multiplayer utilizando Photon e Mirror na Unity.",
                    Description = "Implementação de lógica cliente-servidor e peer-to-peer, interpolação de movimento, reconciliação de estado, balanceamento e sincronização de UI em tempo real.",
                    Technologies = "Unity3d, C#, Photon, Mirror, Networking",
                    RepositoryUrl = "https://github.com/rrmattos",
                    IsFeatured = true,
                    DisplayOrder = 2,
                },
                new Project
                {
                    Title = "Portal Web & API para Gestão e Monitoramento",
                    Summary = "Sistema web completo desenvolvido com C#, ASP.NET Core, EF Core e integração com banco de dados MySQL.",
                    Description = "Arquitetura com autenticação JWT, painel administrativo dinâmico, upload de mídias e APIs RESTful para consumo por aplicações web e jogos.",
                    Technologies = "C#, ASP.NET Core, Blazor, MySQL, EF Core, Docker",
                    RepositoryUrl = "https://github.com/rrmattos",
                    IsFeatured = true,
                    DisplayOrder = 3,
                });
        }

        if (!await db.Experiences.AnyAsync())
        {
            db.Experiences.AddRange(
                new Experience
                {
                    Company = "RRMattos (MEI)",
                    Role = "Desenvolvedor Unity | Sistemas",
                    Location = "Remoto",
                    StartDate = new DateTime(2022, 3, 1),
                    IsCurrent = true,
                    Description = "• Desenvolvimento de jogos, aplicações gamificadas e experiências interativas utilizando Unity e C#.\n• Implementação de sistemas de gameplay, interação, UI e ferramentas de suporte.\n• Desenvolvimento de aplicações para Desktop, Mobile, VR e AR.\n• Decisões de arquitetura de projeto e Game Design.\n• Integração com APIs e bancos de dados.\n• Modelagem 3D (não orgânica) e edição audiovisual.",
                    Technologies = "Unity3d, C#, NodeJS, MySQL, Firebase, Blender, Git, Docker",
                    DisplayOrder = 1,
                },
                new Experience
                {
                    Company = "Hololab Laboratório de Inovações e Tecnologias",
                    Role = "Desenvolvedor Unity",
                    Location = "Criciúma, SC / Remoto",
                    StartDate = new DateTime(2022, 6, 1),
                    EndDate = new DateTime(2024, 10, 1),
                    IsCurrent = false,
                    Description = "• Desenvolvimento de aplicações interativas e gamificadas utilizando Unity e C# para Desktop, Mobile, WebGL, VR, AR e MR.\n• Implementação de sistemas de gameplay, UI e ferramentas de configuração.\n• Desenvolvimento de funcionalidades multiplayer utilizando Photon e Mirror.\n• Integração com APIs e bancos de dados MySQL e PostgreSQL.\n• Atuação como Tech Lead em projetos, direcionamento técnico e Clean Code.\n• Participação em projetos para Fiocruz, ABEn, Acadepol, UNESC e Multilog.",
                    Technologies = "Unity3d, C#, Photon, Mirror, Java, MySQL, Blender, Git, Docker",
                    DisplayOrder = 2,
                },
                new Experience
                {
                    Company = "Contare Tecnologia",
                    Role = "Desenvolvedor Web",
                    Location = "Criciúma, SC",
                    StartDate = new DateTime(2022, 3, 1),
                    EndDate = new DateTime(2022, 6, 1),
                    IsCurrent = false,
                    Description = "• Manutenção e evolução de sistemas Web voltados ao gerenciamento e monitoramento de produtos.\n• Integração de sistemas com tecnologia RFID.\n• Implementação e consumo de APIs RESTful e manipulação de dados.",
                    Technologies = "Java, AngularJS, JS, HTML, CSS, MySQL, PostgreSQL, Git, Jenkins",
                    DisplayOrder = 3,
                },
                new Experience
                {
                    Company = "UNESC",
                    Role = "Técnico de Informática",
                    Location = "Criciúma, SC",
                    StartDate = new DateTime(2017, 7, 1),
                    EndDate = new DateTime(2021, 11, 1),
                    IsCurrent = false,
                    Description = "• Manutenção e diagnóstico de computadores e equipamentos em laboratórios de informática.\n• Suporte presencial e remoto a alunos e professores.\n• Automação de tarefas utilizando shell scripts e manutenção eletrônica.",
                    Technologies = "Hardware, Redes, Shell Script, Suporte",
                    DisplayOrder = 4,
                });
        }

        await db.SaveChangesAsync();
    }
}
