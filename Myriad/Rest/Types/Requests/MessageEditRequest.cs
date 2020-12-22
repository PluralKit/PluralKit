using Myriad.Types;

namespace Myriad.Rest.Types.Requests
{
    public record MessageEditRequest
    {
        public string? Content { get; set; }
        public Embed? Embed { get; set; }
    }
}