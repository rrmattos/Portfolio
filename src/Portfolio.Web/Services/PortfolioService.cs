using Microsoft.EntityFrameworkCore;
using Portfolio.Web.Data;

namespace Portfolio.Web.Services;

/// <summary>
/// CRUD for projects and experiences. Uses a DbContext factory so every operation gets a
/// short-lived context (recommended for Blazor Server circuits).
/// </summary>
public class PortfolioService(IDbContextFactory<AppDbContext> dbFactory, ImageStorageService images)
{
    // ---------- Projects ----------

    public async Task<List<Project>> GetPublishedProjectsAsync(bool featuredOnly = false)
    {
        await using var db = await dbFactory.CreateDbContextAsync();
        var query = db.Projects.AsNoTracking().Include(p => p.Medias).Where(p => p.IsPublished);
        if (featuredOnly) query = query.Where(p => p.IsFeatured);
        return await query.OrderBy(p => p.DisplayOrder).ThenByDescending(p => p.CreatedAt).ToListAsync();
    }

    public async Task<List<Project>> GetAllProjectsAsync()
    {
        await using var db = await dbFactory.CreateDbContextAsync();
        return await db.Projects.AsNoTracking().Include(p => p.Medias)
            .OrderBy(p => p.DisplayOrder).ThenByDescending(p => p.CreatedAt).ToListAsync();
    }

    public async Task<Project?> GetProjectAsync(int id, bool publishedOnly = false)
    {
        await using var db = await dbFactory.CreateDbContextAsync();
        var project = await db.Projects.AsNoTracking().Include(p => p.Medias)
            .FirstOrDefaultAsync(p => p.Id == id && (!publishedOnly || p.IsPublished));
        project?.Medias.Sort((a, b) => a.DisplayOrder.CompareTo(b.DisplayOrder));
        return project;
    }

    /// <param name="syncMedias">
    /// When true, the gallery in the database is replaced by <c>project.Medias</c>
    /// (items missing from the list are deleted, including their files).
    /// </param>
    public async Task<Project> SaveProjectAsync(Project project, bool syncMedias = true)
    {
        var filesToDelete = new List<string?>();

        // Normalise gallery order to the list order.
        for (var i = 0; i < project.Medias.Count; i++)
            project.Medias[i].DisplayOrder = i;

        await using var db = await dbFactory.CreateDbContextAsync();
        if (project.Id == 0)
        {
            project.CreatedAt = DateTime.UtcNow;
            foreach (var m in project.Medias) m.Id = 0;
            db.Projects.Add(project);
        }
        else
        {
            var existing = await db.Projects.Include(p => p.Medias).FirstOrDefaultAsync(p => p.Id == project.Id)
                ?? throw new KeyNotFoundException($"Projeto {project.Id} não encontrado.");

            // Remove the previous cover file when it was replaced or cleared.
            if (existing.ImageUrl != project.ImageUrl)
                filesToDelete.Add(existing.ImageUrl);

            var createdAt = existing.CreatedAt;
            db.Entry(existing).CurrentValues.SetValues(project);
            existing.CreatedAt = createdAt == default ? DateTime.UtcNow : createdAt;
            existing.UpdatedAt = DateTime.UtcNow;

            if (syncMedias)
            {
                var incoming = project.Medias.Where(m => m.Id != 0).ToDictionary(m => m.Id);

                foreach (var media in existing.Medias.ToList())
                {
                    if (incoming.TryGetValue(media.Id, out var updated))
                    {
                        media.DisplayOrder = updated.DisplayOrder;
                    }
                    else
                    {
                        existing.Medias.Remove(media);
                        filesToDelete.Add(media.MediaUrl);
                    }
                }

                foreach (var media in project.Medias.Where(m => m.Id == 0))
                {
                    existing.Medias.Add(new ProjectMedia
                    {
                        MediaUrl = media.MediaUrl,
                        MediaType = media.MediaType,
                        DisplayOrder = media.DisplayOrder,
                    });
                }
            }

            project = existing;
        }

        await db.SaveChangesAsync();

        // Only touch the disk after the database accepted the change.
        foreach (var file in filesToDelete) images.Delete(file);

        project.Medias.Sort((a, b) => a.DisplayOrder.CompareTo(b.DisplayOrder));
        return project;
    }

    public async Task<bool> DeleteProjectAsync(int id)
    {
        await using var db = await dbFactory.CreateDbContextAsync();
        var project = await db.Projects.Include(p => p.Medias).FirstOrDefaultAsync(p => p.Id == id);
        if (project is null) return false;
        var files = project.Medias.Select(m => m.MediaUrl).Append(project.ImageUrl).ToList();
        db.Projects.Remove(project); // gallery rows are removed by cascade
        await db.SaveChangesAsync();
        foreach (var file in files) images.Delete(file);
        return true;
    }

    // ---------- Experiences ----------

    public async Task<List<Experience>> GetExperiencesAsync()
    {
        await using var db = await dbFactory.CreateDbContextAsync();
        return await db.Experiences.AsNoTracking()
            .OrderByDescending(e => e.IsCurrent)
            .ThenByDescending(e => e.StartDate)
            .ThenBy(e => e.DisplayOrder)
            .ToListAsync();
    }

    public async Task<Experience?> GetExperienceAsync(int id)
    {
        await using var db = await dbFactory.CreateDbContextAsync();
        return await db.Experiences.AsNoTracking().FirstOrDefaultAsync(e => e.Id == id);
    }

    public async Task<Experience> SaveExperienceAsync(Experience experience)
    {
        if (experience.IsCurrent) experience.EndDate = null;

        await using var db = await dbFactory.CreateDbContextAsync();
        if (experience.Id == 0)
        {
            db.Experiences.Add(experience);
        }
        else
        {
            var existing = await db.Experiences.FindAsync(experience.Id)
                ?? throw new KeyNotFoundException($"Experiência {experience.Id} não encontrada.");
            db.Entry(existing).CurrentValues.SetValues(experience);
            experience = existing;
        }

        await db.SaveChangesAsync();
        return experience;
    }

    public async Task<bool> DeleteExperienceAsync(int id)
    {
        await using var db = await dbFactory.CreateDbContextAsync();
        var deleted = await db.Experiences.Where(e => e.Id == id).ExecuteDeleteAsync();
        return deleted > 0;
    }

    // ---------- Dashboard ----------

    public async Task<DashboardStats> GetStatsAsync()
    {
        await using var db = await dbFactory.CreateDbContextAsync();
        return new DashboardStats(
            await db.Projects.CountAsync(),
            await db.Projects.CountAsync(p => p.IsPublished),
            await db.Experiences.CountAsync(),
            await db.ContactMessages.CountAsync(),
            await db.ContactMessages.CountAsync(m => !m.IsRead));
    }
}

public record DashboardStats(int Projects, int PublishedProjects, int Experiences, int Messages, int UnreadMessages);
