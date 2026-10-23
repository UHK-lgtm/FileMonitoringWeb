namespace FileMonitoringWeb.Server.Endpoints;

/// The signed-in user, from the header App Service adds after Entra ID sign-in. Used only for audit and comment authorship.
/// App Service enforces sign-in before a request reaches the API, so this header is trusted only behind that gate (docs/ISSUES.md).
public static class CurrentUser
{
    public const string HeaderName = "X-MS-CLIENT-PRINCIPAL-NAME";

    private const int MaxLength = 256;

    /// Null when the header is missing or empty. Writes are refused in that case.
    public static string? Get(HttpContext context)
    {
        var name = context.Request.Headers[HeaderName].ToString().Trim();
        if (name.Length == 0)
            return null;

        return name.Length > MaxLength ? name[..MaxLength] : name;
    }
}
