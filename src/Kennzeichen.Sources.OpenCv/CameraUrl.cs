namespace Kennzeichen.Sources.OpenCv;

public static class CameraUrl
{
    /// <summary>
    /// Setzt Benutzername und Passwort in die URL ein (<c>rtsp://user:pass@host/...</c>), so wie Axis es für RTSP dokumentiert.
    /// Sonderzeichen werden URL-kodiert.
    /// </summary>
    public static string WithCredentials(string url, string? username, string? password)
    {
        if (string.IsNullOrEmpty(username))
        {
            return url;
        }

        var builder = new UriBuilder(url)
        {
            UserName = Uri.EscapeDataString(username),
            Password = Uri.EscapeDataString(password ?? ""),
        };
        return builder.Uri.AbsoluteUri;
    }
}
