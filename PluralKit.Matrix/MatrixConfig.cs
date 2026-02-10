namespace PluralKit.Matrix;

public class MatrixConfig
{
    public string HomeserverUrl { get; set; } = "http://localhost:8008";
    public string ServerName { get; set; } = "localhost";
    public string AsToken { get; set; } = "";
    public string HsToken { get; set; } = "";
    public string BotLocalpart { get; set; } = "_pk_bot";
    public int Port { get; set; } = 5050;
    public string Prefix { get; set; } = "!pk";

    public void Validate()
    {
        if (string.IsNullOrWhiteSpace(AsToken))
            throw new InvalidOperationException("Matrix AsToken is not configured. Run with --generate-registration to create one.");
        if (string.IsNullOrWhiteSpace(HsToken))
            throw new InvalidOperationException("Matrix HsToken is not configured. Run with --generate-registration to create one.");
        if (!Uri.TryCreate(HomeserverUrl, UriKind.Absolute, out _))
            throw new InvalidOperationException($"Matrix HomeserverUrl is not a valid URL: {HomeserverUrl}");
        if (Port < 1 || Port > 65535)
            throw new InvalidOperationException($"Matrix Port is out of range: {Port}");
    }
}
