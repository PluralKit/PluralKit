namespace PluralKit.Bot
{
    public class BotConfig
    {
        public static readonly string[] DefaultPrefixes = {"pk;", "pk!"};

        public string Token { get; set; }
        public ulong? ClientId { get; set; }
        
        // ASP.NET configuration merges arrays with defaults, so we leave this field nullable
        // and fall back to the separate default array at the use site :)
        // This does bind [] as null (therefore default) instead of an empty array, but I can live w/ that. 
        public string[] Prefixes { get; set; }
    }
}