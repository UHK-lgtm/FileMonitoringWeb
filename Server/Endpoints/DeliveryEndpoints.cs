using FileMonitoringWeb.Server.Data;
using FileMonitoringWeb.Server.Domain;
using FileMonitoringWeb.Server.Models;

namespace FileMonitoringWeb.Server.Endpoints;

/// Delivery endpoints (docs/api-contract.md, "Deliveries"). Reads, plus comments. Status is written by FileScan only.
public static class DeliveryEndpoints
{
    private const int MaxCommentLength = 2000;

    public static void MapDeliveryEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/deliveries");

        group.MapGet("", async (DateOnly? date, DeliveryRepository repository, CancellationToken cancellationToken) =>
        {
            var day = date ?? LocalDates.Today(LocalDates.Luxembourg);
            var items = await repository.ListAsync(day, cancellationToken);
            return Results.Ok(new { date = day, items });
        });

        group.MapGet("{id:long}", async (long id, DeliveryRepository repository, CancellationToken cancellationToken) =>
        {
            var detail = await repository.GetDetailAsync(id, cancellationToken);
            return detail is null ? Results.NotFound() : Results.Ok(detail);
        });

        group.MapPost("{id:long}/comments", async (long id, CommentInput input, HttpContext context, DeliveryRepository repository, CancellationToken cancellationToken) =>
        {
            if (CurrentUser.Get(context) is not { } user)
                return Results.Unauthorized();

            var text = input.Text?.Trim() ?? string.Empty;
            if (text.Length == 0)
                return Results.BadRequest(new { errors = new[] { "text is required." } });

            if (text.Length > MaxCommentLength)
                return Results.BadRequest(new { errors = new[] { $"text must be at most {MaxCommentLength} characters." } });

            var comment = await repository.AddCommentAsync(id, text, user, cancellationToken);
            return comment is null
                ? Results.NotFound()
                : Results.Created($"/api/deliveries/{id}/comments/{comment.CommentId}", comment);
        });
    }
}
