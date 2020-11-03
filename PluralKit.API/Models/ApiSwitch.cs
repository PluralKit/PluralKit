#nullable enable
using System;
using System.Collections.Generic;

using NodaTime;

namespace PluralKit.API.Models
{
    public class ApiSwitch
    {
        public Guid SwitchId { get; set; }
        public Instant Timestamp { get; set; }
        public string? Note { get; set; }
        public IEnumerable<Guid> Members { get; set; } = null!;
    }
}