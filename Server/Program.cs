using FileMonitoringWeb.Server.Data;
using FileMonitoringWeb.Server.Endpoints;
using Microsoft.AspNetCore.Diagnostics;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddSingleton<MonitoringDefinitionRepository>();
builder.Services.AddSingleton<DeliveryRepository>();

var app = builder.Build();

// Unhandled errors return a fixed message. The log records the exception type only, because SQL messages can contain server names.
app.UseExceptionHandler(errorApp => errorApp.Run(async context =>
{
    var error = context.Features.Get<IExceptionHandlerFeature>()?.Error;
    var logger = context.RequestServices.GetRequiredService<ILogger<Program>>();
    logger.LogError("Request failed: {ExceptionType}", error?.GetType().Name ?? "Unknown");

    context.Response.StatusCode = StatusCodes.Status500InternalServerError;
    await context.Response.WriteAsJsonAsync(new { message = "Something went wrong. Try again, and tell the administrator if it keeps happening." });
}));

app.MapGet("/api/health", () => Results.Ok(new { status = "ok" }));

app.MapDefinitionEndpoints();
app.MapDeliveryEndpoints();
app.MapExportEndpoints();

// Unknown API routes return 404 instead of the React index page.
app.MapGet("/api/{**path}", () => Results.NotFound());

// Serve the compiled React app from wwwroot and fall back to index.html for client-side routes.
app.UseDefaultFiles();
app.UseStaticFiles();
app.MapFallbackToFile("index.html");

app.Run();
