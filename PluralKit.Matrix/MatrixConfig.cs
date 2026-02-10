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
}
