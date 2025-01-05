namespace PluralKit.Core;

public static class BuildInfoService
{
    public static string Version { get; private set; }
    public static string FullVersion { get; private set; }
    public static string Timestamp { get; private set; }
    public static bool IsDev { get; private set; }

    public static async Task LoadVersion()
    {
        using var stream = typeof(BuildInfoService).Assembly.GetManifestResourceStream("version");
        if (stream == null) throw new Exception("missing version information");

        using var reader = new StreamReader(stream);
        var data = (await reader.ReadToEndAsync()).Split("\n");

        FullVersion = data[0];
        Timestamp = data[1];

        IsDev = data.Length < 3 || data[2] == "";

        // show only short commit hash to users
        Version = FullVersion.Remove(7);
    }
}