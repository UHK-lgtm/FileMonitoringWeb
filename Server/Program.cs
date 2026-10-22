var builder = WebApplication.CreateBuilder(args);
var app = builder.Build();

app.MapGet("/api/health", () => Results.Ok(new { status = "ok" }));

// Unknown API routes return 404 instead of the React index page.
app.MapGet("/api/{**path}", () => Results.NotFound());

// Serve the compiled React app from wwwroot and fall back to index.html for client-side routes.
app.UseDefaultFiles();
app.UseStaticFiles();
app.MapFallbackToFile("index.html");

app.Run();
