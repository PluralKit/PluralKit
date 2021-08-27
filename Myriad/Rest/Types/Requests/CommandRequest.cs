using System.Collections.Generic;

using Myriad.Types;

namespace Myriad.Rest.Types
{
    public record ApplicationCommandRequest
    {
        public string Name { get; init; }
        public string Description { get; init; }
        public List<ApplicationCommandOption>? Options { get; init; }
    }
}