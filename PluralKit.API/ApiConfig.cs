namespace PluralKit.API;

public class ApiConfig
{
    public int Port { get; set; } = 5000;
    public string? ClientId { get; set; }
    public string? ClientSecret { get; set; }
    public bool TrustAuth { get; set; } = false;
    public string? AvatarServiceUrl { get; set; }
}