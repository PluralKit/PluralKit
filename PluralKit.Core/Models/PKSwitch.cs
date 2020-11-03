#nullable enable
using System;

using NodaTime;

namespace PluralKit.Core {
    public class PKSwitch
    {
        public SwitchId Id { get; }
        public Guid Uuid { get; }
        public string? Note { get; }
        public SystemId System { get; set; }
        public Instant Timestamp { get; }
    }
}