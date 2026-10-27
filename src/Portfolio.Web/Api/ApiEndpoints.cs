using Microsoft.AspNetCore.Http.HttpResults;
using Portfolio.Web.Auth;
using Portfolio.Web.Data;
using Portfolio.Web.Services;

namespace Portfolio.Web.Api;

/// <summary>
/// REST API. Public read endpoints + JWT-protected management endpoints
/// (send "Authorization: Bearer {token}" obtained from POST /api/auth/login).
/// </summary>
public static class ApiEndpoints
{
    public record LoginRequest(string Email, string Password);

    public static void MapPortfolioApi(this IEndpointRouteBuilder app)
    {
        var api = app.MapGroup("/api").DisableAntiforgery();

        // ---------- Auth ----------
        api.MapPost("/auth/login", async Task<Results<Ok<LoginResult>, UnauthorizedHttpResult>> (LoginRequest req, TokenService tokens) =>
        {
            var result = await tokens.LoginAsync(req.Email, req.Password);
            return result.Success ? TypedResults.Ok(result) : TypedResults.Unauthorized();
        }).RequireRateLimiting("auth");

        // ---------- Public ----------
        api.MapGet("/projects", (PortfolioService svc) => svc.GetPublishedProjectsAsync());
        api.MapGet("/projects/{id:int}", async Task<Results<Ok<Project>, NotFound>> (int id, PortfolioService svc) =>
            await svc.GetProjectAsync(id, publishedOnly: true) is { } p ? TypedResults.Ok(p) : TypedResults.NotFound());
        api.MapGet("/experiences", (PortfolioService svc) => svc.GetExperiencesAsync());

        api.MapPost("/contact", async Task<Results<Ok, ValidationProblem>> (ContactMessage msg, ContactService svc) =>
        {
            var errors = Validate(msg);
            if (errors is not null) return TypedResults.ValidationProblem(errors);
            await svc.SubmitAsync(msg);
            return TypedResults.Ok();
        }).RequireRateLimiting("contact");

        // ---------- Admin (JWT) ----------
        var admin = api.MapGroup("/admin").RequireAuthorization(AuthPolicies.ApiAdmin);

        admin.MapGet("/projects", (PortfolioService svc) => svc.GetAllProjectsAsync());

        admin.MapPost("/projects", async Task<Results<Created<Project>, ValidationProblem>> (Project p, PortfolioService svc) =>
        {
            p.Id = 0;
            if (Validate(p) is { } errors) return TypedResults.ValidationProblem(errors);
            var saved = await svc.SaveProjectAsync(p);
            return TypedResults.Created($"/api/projects/{saved.Id}", saved);
        });

        admin.MapPut("/projects/{id:int}", async Task<Results<Ok<Project>, NotFound, ValidationProblem>> (int id, Project p, PortfolioService svc) =>
        {
            if (await svc.GetProjectAsync(id) is null) return TypedResults.NotFound();
            p.Id = id;
            if (Validate(p) is { } errors) return TypedResults.ValidationProblem(errors);
            return TypedResults.Ok(await svc.SaveProjectAsync(p, syncMedias: false));
        });

        admin.MapDelete("/projects/{id:int}", async Task<Results<NoContent, NotFound>> (int id, PortfolioService svc) =>
            await svc.DeleteProjectAsync(id) ? TypedResults.NoContent() : TypedResults.NotFound());

        // multipart/form-data with a "file" field
        admin.MapPost("/projects/{id:int}/image", async Task<Results<Ok<Project>, NotFound, BadRequest<string>>> (
            int id, IFormFile file, PortfolioService svc, ImageStorageService images) =>
        {
            var project = await svc.GetProjectAsync(id);
            if (project is null) return TypedResults.NotFound();
            try
            {
                await using var stream = file.OpenReadStream();
                project.ImageUrl = await images.SaveAsync(stream, file.ContentType);
            }
            catch (InvalidOperationException ex)
            {
                return TypedResults.BadRequest(ex.Message);
            }
            return TypedResults.Ok(await svc.SaveProjectAsync(project));
        });

        // multipart/form-data with a "file" field (image or mp4/webm video)
        admin.MapPost("/projects/{id:int}/media", async Task<Results<Ok<Project>, NotFound, BadRequest<string>>> (
            int id, IFormFile file, PortfolioService svc, ImageStorageService images) =>
        {
            var project = await svc.GetProjectAsync(id);
            if (project is null) return TypedResults.NotFound();
            if (project.Medias.Count >= ImageStorageService.MaxGalleryItems)
                return TypedResults.BadRequest($"Limite de {ImageStorageService.MaxGalleryItems} itens na galeria.");
            try
            {
                await using var stream = file.OpenReadStream();
                var (url, type) = await images.SaveMediaAsync(stream, file.ContentType);
                project.Medias.Add(new ProjectMedia { MediaUrl = url, MediaType = type });
            }
            catch (InvalidOperationException ex)
            {
                return TypedResults.BadRequest(ex.Message);
            }
            return TypedResults.Ok(await svc.SaveProjectAsync(project));
        });

        admin.MapPost("/experiences", async Task<Results<Created<Experience>, ValidationProblem>> (Experience e, PortfolioService svc) =>
        {
            e.Id = 0;
            if (Validate(e) is { } errors) return TypedResults.ValidationProblem(errors);
            var saved = await svc.SaveExperienceAsync(e);
            return TypedResults.Created($"/api/experiences/{saved.Id}", saved);
        });

        admin.MapPut("/experiences/{id:int}", async Task<Results<Ok<Experience>, NotFound, ValidationProblem>> (int id, Experience e, PortfolioService svc) =>
        {
            if (await svc.GetExperienceAsync(id) is null) return TypedResults.NotFound();
            e.Id = id;
            if (Validate(e) is { } errors) return TypedResults.ValidationProblem(errors);
            return TypedResults.Ok(await svc.SaveExperienceAsync(e));
        });

        admin.MapDelete("/experiences/{id:int}", async Task<Results<NoContent, NotFound>> (int id, PortfolioService svc) =>
            await svc.DeleteExperienceAsync(id) ? TypedResults.NoContent() : TypedResults.NotFound());

        admin.MapGet("/messages", (ContactService svc) => svc.GetMessagesAsync());
    }

    private static Dictionary<string, string[]>? Validate(object model)
    {
        var results = new List<System.ComponentModel.DataAnnotations.ValidationResult>();
        var ctx = new System.ComponentModel.DataAnnotations.ValidationContext(model);
        if (System.ComponentModel.DataAnnotations.Validator.TryValidateObject(model, ctx, results, true))
            return null;

        return results
            .SelectMany(r => (r.MemberNames.Any() ? r.MemberNames : [""]).Select(m => (m, r.ErrorMessage ?? "Inválido")))
            .GroupBy(x => x.m)
            .ToDictionary(g => g.Key, g => g.Select(x => x.Item2).ToArray());
    }
}
