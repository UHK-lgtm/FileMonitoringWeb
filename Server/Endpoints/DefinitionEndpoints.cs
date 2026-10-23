using FileMonitoringWeb.Server.Data;
using FileMonitoringWeb.Server.Domain;
using FileMonitoringWeb.Server.Models;

namespace FileMonitoringWeb.Server.Endpoints;

/// Configuration endpoints for monitoring definitions (docs/api-contract.md, "Monitoring definitions").
public static class DefinitionEndpoints
{
    public sealed record SetEnabledRequest(bool IsEnabled);

    public static void MapDefinitionEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/monitoring-definitions");

        group.MapGet("", async (MonitoringDefinitionRepository repository, CancellationToken cancellationToken) =>
            Results.Ok(await repository.ListAsync(cancellationToken)));

        group.MapGet("{id:int}", async (int id, MonitoringDefinitionRepository repository, CancellationToken cancellationToken) =>
        {
            var definition = await repository.GetAsync(id, cancellationToken);
            return definition is null ? Results.NotFound() : Results.Ok(definition);
        });

        group.MapPost("", async (DefinitionInput input, HttpContext context, MonitoringDefinitionRepository repository, CancellationToken cancellationToken) =>
        {
            if (CurrentUser.Get(context) is not { } user)
                return Results.Unauthorized();

            var errors = DefinitionValidator.Validate(input);
            if (errors.Count > 0)
                return Results.BadRequest(new { errors });

            var id = await repository.CreateAsync(input, user, cancellationToken);
            return Results.Created($"/api/monitoring-definitions/{id}", await repository.GetAsync(id, cancellationToken));
        });

        group.MapPut("{id:int}", async (int id, DefinitionInput input, HttpContext context, MonitoringDefinitionRepository repository, CancellationToken cancellationToken) =>
        {
            if (CurrentUser.Get(context) is not { } user)
                return Results.Unauthorized();

            var errors = DefinitionValidator.Validate(input);
            if (errors.Count > 0)
                return Results.BadRequest(new { errors });

            var outcome = await repository.UpdateAsync(id, input, user, cancellationToken);
            return outcome switch
            {
                SaveOutcome.Saved => Results.Ok(await repository.GetAsync(id, cancellationToken)),
                SaveOutcome.NotFound => Results.NotFound(),
                SaveOutcome.Conflict => Results.Conflict(new { message = "This definition changed since you opened it. Reload it and try again." }),
                _ => Results.BadRequest(new { errors = new[] { "The definition cannot be saved in its current state." } }),
            };
        });

        group.MapPost("{id:int}/enabled", async (int id, SetEnabledRequest request, HttpContext context, MonitoringDefinitionRepository repository, CancellationToken cancellationToken) =>
        {
            if (CurrentUser.Get(context) is not { } user)
                return Results.Unauthorized();

            var outcome = await repository.SetEnabledAsync(id, request.IsEnabled, user, cancellationToken);
            return outcome switch
            {
                SaveOutcome.Saved => Results.Ok(await repository.GetAsync(id, cancellationToken)),
                SaveOutcome.NotFound => Results.NotFound(),
                _ => Results.BadRequest(new { errors = new[] { "A retired definition cannot be enabled." } }),
            };
        });

        group.MapPost("{id:int}/retire", async (int id, HttpContext context, MonitoringDefinitionRepository repository, CancellationToken cancellationToken) =>
        {
            if (CurrentUser.Get(context) is not { } user)
                return Results.Unauthorized();

            var outcome = await repository.RetireAsync(id, user, cancellationToken);
            return outcome == SaveOutcome.NotFound
                ? Results.NotFound()
                : Results.Ok(await repository.GetAsync(id, cancellationToken));
        });

        group.MapGet("{id:int}/audit", async (int id, MonitoringDefinitionRepository repository, CancellationToken cancellationToken) =>
        {
            if (await repository.GetAsync(id, cancellationToken) is null)
                return Results.NotFound();

            return Results.Ok(await repository.AuditForAsync(id, cancellationToken));
        });
    }
}
