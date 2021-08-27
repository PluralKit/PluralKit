using System.Text.Json;

namespace Myriad.Rest
{
    public record DiscordApiError(string Message, int Code)
    {
        public JsonElement? Errors { get; init; }
    }
}