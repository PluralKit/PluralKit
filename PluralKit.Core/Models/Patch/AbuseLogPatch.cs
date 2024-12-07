using Newtonsoft.Json.Linq;

using SqlKata;

namespace PluralKit.Core;

public class AbuseLogPatch: PatchObject
{
    public Partial<string> Description { get; set; }
    public Partial<bool> DenyBotUsage { get; set; }

    public override Query Apply(Query q) => q.ApplyPatch(wrapper => wrapper
        .With("description", Description)
        .With("deny_bot_usage", DenyBotUsage)
    );
}