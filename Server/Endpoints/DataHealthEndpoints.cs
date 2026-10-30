using FileMonitoringWeb.Server.Data;
using FileMonitoringWeb.Server.DataHealth;
using FileMonitoringWeb.Server.Domain;

namespace FileMonitoringWeb.Server.Endpoints;

/// Data Health endpoints (docs/Data_Health_Basic_Specifications.md and docs/api-contract.md). Reads run the approved checks;
/// writes change only the control configuration, and every change is audited.
public static class DataHealthEndpoints
{
    public static void MapDataHealthEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/data-health");

        group.MapGet("", async (DataHealthService service, CancellationToken cancellationToken) =>
            Results.Ok(await service.GetResultsAsync(cancellationToken)));

        group.MapGet("sources", async (DataHealthRepository repository, CancellationToken cancellationToken) =>
            Results.Ok(await repository.ListSourcesAsync(cancellationToken)));

        group.MapGet("controls",async (DataHealthRepository repository, CancellationToken cancellationToken) =>
            Results.Ok(await repository.ListControlsAsync(cancellationToken)));

        group.MapGet("controls/{id:int}", async (int id, DataHealthRepository repository, CancellationToken cancellationToken) =>
        {
            var control = await repository.GetControlAsync(id, cancellationToken);
            return control is null ? Results.NotFound() : Results.Ok(control);
        });

        group.MapPost("controls", async (DataHealthControlInput input, HttpContext context, DataHealthRepository repository, CancellationToken cancellationToken) =>
        {
            if (CurrentUser.Get(context) is not { } user)
                return Results.Unauthorized();

            var errors = await ValidateAsync(input, repository, cancellationToken);
            if (errors.Count > 0)
                return Results.BadRequest(new { errors });

            var id = await repository.CreateControlAsync(input, user, cancellationToken);
            return Results.Created($"/api/data-health/controls/{id}", await repository.GetControlAsync(id, cancellationToken));
        });

        group.MapPut("controls/{id:int}", async (int id, DataHealthControlInput input, HttpContext context, DataHealthRepository repository, CancellationToken cancellationToken) =>
        {
            if (CurrentUser.Get(context) is not { } user)
                return Results.Unauthorized();

            var errors = await ValidateAsync(input, repository, cancellationToken);
            if (errors.Count > 0)
                return Results.BadRequest(new { errors });

            var outcome = await repository.UpdateControlAsync(id, input, user, cancellationToken);
            return outcome switch
            {
                SaveOutcome.Saved => Results.Ok(await repository.GetControlAsync(id, cancellationToken)),
                SaveOutcome.NotFound => Results.NotFound(),
                SaveOutcome.Conflict => Results.Conflict(new { message = "This control changed since you opened it. Reload it and try again." }),
                _ => Results.BadRequest(new { errors = new[] { "The control cannot be saved in its current state." } }),
            };
        });

        group.MapPost("controls/{id:int}/enabled", async (int id, DefinitionEndpoints.SetEnabledRequest request, HttpContext context, DataHealthRepository repository, CancellationToken cancellationToken) =>
        {
            if (CurrentUser.Get(context) is not { } user)
                return Results.Unauthorized();

            var outcome = await repository.SetEnabledAsync(id, request.IsEnabled, user, cancellationToken);
            return outcome switch
            {
                SaveOutcome.Saved => Results.Ok(await repository.GetControlAsync(id, cancellationToken)),
                SaveOutcome.NotFound => Results.NotFound(),
                _ => Results.BadRequest(new { errors = new[] { "A retired control cannot be enabled." } }),
            };
        });

        group.MapPost("controls/{id:int}/retire", async (int id, HttpContext context, DataHealthRepository repository, CancellationToken cancellationToken) =>
        {
            if (CurrentUser.Get(context) is not { } user)
                return Results.Unauthorized();

            var outcome = await repository.RetireAsync(id, user, cancellationToken);
            return outcome == SaveOutcome.NotFound
                ? Results.NotFound()
                : Results.Ok(await repository.GetControlAsync(id, cancellationToken));
        });

        group.MapGet("controls/{id:int}/audit", async (int id, DataHealthRepository repository, CancellationToken cancellationToken) =>
        {
            if (await repository.GetControlAsync(id, cancellationToken) is null)
                return Results.NotFound();

            return Results.Ok(await repository.AuditForAsync(id, cancellationToken));
        });
    }

    /// Field rules first, then the source: it must be an approved, active source.
    private static async Task<IReadOnlyList<string>> ValidateAsync(DataHealthControlInput input, DataHealthRepository repository, CancellationToken cancellationToken)
    {
        var errors = DataHealthValidator.Validate(input).ToList();
        if (errors.Count == 0 && await repository.GetSourceAsync(input.ApprovedSourceId, cancellationToken) is null)
            errors.Add("The chosen source is not an approved, active source.");

        return errors;
    }
}
