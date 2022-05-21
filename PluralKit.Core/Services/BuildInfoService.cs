namespace PluralKit.Core;

public static class BuildInfoService
{
    public static string Version { get; private set; }
    public static string FullVersion { get; private set; }

    public static async Task LoadVersion()
    {
        using (var stream = typeof(BuildInfoService).Assembly.GetManifestResourceStream("version"))
        {
            // if this happens, something broke
            if (stream == null) FullVersion = "(unknown version) ";
            else
                using (var reader = new StreamReader(stream))
                    FullVersion = await reader.ReadToEndAsync();
        }

        // cheap hack to remove newline
        FullVersion = FullVersion.Remove(FullVersion.Length - 1);

        // show only short commit hash to users
        Version = FullVersion.Remove(7);
    }
}