#nullable enable
using System;
using System.Collections.Generic;

using NodaTime;

using PluralKit.Core;

namespace PluralKit.API.Models
{
    public class ApiSwitchPatch
    {
        public Partial<Instant> Timestamp { get; set; }
        public Partial<string?> Note { get; set; }
        public Partial<IEnumerable<Guid>> Members { get; set; } = null!;
    }
}