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
        var query = db.Projects.AsNoTracking().Where(p => p.IsPublished);
        if (featuredOnly) query = query.Where(p => p.IsFeatured);
        return await query.OrderBy(p => p.DisplayOrder).ThenByDescending(p => p.CreatedAt).ToListAsync();
    }

    public async Task<List<Project>> GetAllProjectsAsync()
    {
        await using var db = await dbFactory.CreateDbContextAsync();
        return await db.Projects.AsNoTracking()
            .OrderBy(p => p.DisplayOrder).ThenByDescending(p => p.CreatedAt).ToListAsync();
    }

    public async Task<Project?> GetProjectAsync(int id, bool publishedOnly = false)
    {
        await using var db = await dbFactory.CreateDbContextAsync();
        return await db.Projects.AsNoTracking()
            .FirstOrDefaultAsync(p => p.Id == id && (!publishedOnly || p.IsPublished));
    }

    public async Task<Project> SaveProjectAsync(Project project)
    {
        await using var db = await dbFactory.CreateDbContextAsync();
        if (project.Id == 0)
        {
            project.CreatedAt = DateTime.UtcNow;
            db.Projects.Add(project);
        }
        else
        {
            var existing = await db.Projects.FindAsync(project.Id)
                ?? throw new KeyNotFoundException($"Projeto {project.Id} não encontrado.");

            // Remove the previous image file when it was replaced or cleared.
            if (existing.ImageUrl != project.ImageUrl)
                images.Delete(existing.ImageUrl);

            db.Entry(existing).CurrentValues.SetValues(project);
            existing.CreatedAt = existing.CreatedAt == default ? DateTime.UtcNow : existing.CreatedAt;
            existing.UpdatedAt = DateTime.UtcNow;
            project = existing;
        }

        await db.SaveChangesAsync();
        return project;
    }

    public async Task<bool> DeleteProjectAsync(int id)
    {
        await using var db = await dbFactory.CreateDbContextAsync();
        var project = await db.Projects.FindAsync(id);
        if (project is null) return false;
        db.Projects.Remove(project);
        await db.SaveChangesAsync();
        images.Delete(project.ImageUrl);
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
