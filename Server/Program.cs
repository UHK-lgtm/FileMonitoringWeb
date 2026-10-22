using FileMonitoringWeb.Server.Data;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddSingleton<MonitoringDefinitionRepository>();

var app = builder.Build();

app.MapGet("/api/health", () => Results.Ok(new { status = "ok" }));

app.MapGet("/api/monitoring-definitions", async (
    MonitoringDefinitionRepository repository,
    ILogger<Program> logger,
    CancellationToken cancellationToken) =>
{
    try
    {
        return Results.Ok(await repository.GetAllAsync(cancellationToken));
    }
    catch (Exception exception) when (!cancellationToken.IsCancellationRequested)
    {
        // Log the exception type only: SQL error messages can contain server names.
        logger.LogError("Reading monitoring definitions failed: {ExceptionType}", exception.GetType().Name);
        return Results.Problem(statusCode: StatusCodes.Status503ServiceUnavailable, title: "Monitoring definitions are unavailable.");
    }
});

// Unknown API routes return 404 instead of the React index page.
app.MapGet("/api/{**path}", () => Results.NotFound());

// Serve the compiled React app from wwwroot and fall back to index.html for client-side routes.
app.UseDefaultFiles();
app.UseStaticFiles();
app.MapFallbackToFile("index.html");

app.Run();
