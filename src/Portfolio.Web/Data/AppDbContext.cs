using Microsoft.EntityFrameworkCore;

namespace Portfolio.Web.Data;

public class AppDbContext(DbContextOptions<AppDbContext> options) : DbContext(options)
{
    public DbSet<Project> Projects => Set<Project>();
    public DbSet<ProjectMedia> ProjectMedias => Set<ProjectMedia>();
    public DbSet<Experience> Experiences => Set<Experience>();
    public DbSet<ContactMessage> ContactMessages => Set<ContactMessage>();
    public DbSet<AdminUser> AdminUsers => Set<AdminUser>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<Project>(e =>
        {
            e.Ignore(p => p.TechList);
            e.Ignore(p => p.DisplayCoverUrl);
            e.Property(p => p.Description).HasColumnType("text");
            e.HasIndex(p => new { p.IsPublished, p.DisplayOrder });
            e.HasMany(p => p.Medias)
                .WithOne()
                .HasForeignKey(m => m.ProjectId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<ProjectMedia>(e => e.HasIndex(m => new { m.ProjectId, m.DisplayOrder }));

        modelBuilder.Entity<Experience>(e =>
        {
            e.Ignore(x => x.TechList);
            e.Property(x => x.Description).HasColumnType("text");
            e.Property(x => x.StartDate).HasColumnType("date");
            e.Property(x => x.EndDate).HasColumnType("date");
        });

        modelBuilder.Entity<ContactMessage>(e =>
        {
            e.Property(x => x.Message).HasColumnType("text");
            e.HasIndex(x => x.CreatedAt);
        });

        modelBuilder.Entity<AdminUser>(e => e.HasIndex(x => x.Email).IsUnique());
    }
}
