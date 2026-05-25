using Microsoft.EntityFrameworkCore;
using Weft.Api.Data;
using Weft.Api.Domain;
using Weft.Api.Features;

namespace Weft.Api.Features;

public static class ProjectsEndpoints
{
    public static void MapProjects(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/projects").WithTags("Projects").RequireAuthorization();

        group.MapGet("/", async (WeftDb db) =>
        {
            var projects = await db.Projects
                .OrderBy(p => p.CreatedAt)
                .Select(p => new ProjectSummary(
                    p.Id, p.Name, p.Description, p.Assets.Count, p.CreatedAt))
                .ToListAsync();
            return Results.Ok(projects);
        });

        group.MapGet("/{id:guid}", async (Guid id, WeftDb db) =>
        {
            var project = await db.Projects
                .Where(p => p.Id == id)
                .Select(p => new ProjectSummary(
                    p.Id, p.Name, p.Description, p.Assets.Count, p.CreatedAt))
                .FirstOrDefaultAsync();
            return project is null ? Results.NotFound(new { error = "Project not found" }) : Results.Ok(project);
        });

        group.MapPost("/", async (CreateProjectRequest req, WeftDb db) =>
        {
            var name = (req.Name ?? "").Trim();
            if (name.Length is 0 or > 80)
                return Results.BadRequest(new { error = "Project name is required (max 80 chars)" });

            var project = new Project { Name = name, Description = (req.Description ?? "").Trim() };
            db.Projects.Add(project);
            await db.SaveChangesAsync();
            return Results.Created($"/api/projects/{project.Id}",
                new ProjectSummary(project.Id, project.Name, project.Description, 0, project.CreatedAt));
        });
    }
}
