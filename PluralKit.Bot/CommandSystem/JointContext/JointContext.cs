using App.Metrics;

using Autofac;

using Myriad.Cache;
using Myriad.Rest;
using Myriad.Rest.Types;
using Myriad.Types;

using PluralKit.Core;

namespace PluralKit.Bot;

public abstract class JointContext
{
    protected readonly ILifetimeScope _provider;
    protected readonly IMetrics _metrics;

    // This is a joint parent class for Context and InteractionContext
    // There are more fields from the two of them that can be combined into this
    // But are just a little annoying to do so and I don't have a use for yet
    // So I haven't implemented them here yet
    // IE: Context uses Channel/Message/Guild Types but InteractionContext
    // only stores the IDs
    public JointContext(ILifetimeScope provider, PKSystem system, SystemConfig config, string[] prefixes)
    {
        _provider = provider;
        _metrics = provider.Resolve<IMetrics>();
        Cache = provider.Resolve<IDiscordCache>();
        Rest = provider.Resolve<DiscordApiClient>();
        System = system;
        Config = config;
        DefaultPrefix = prefixes[0];
        Repository = provider.Resolve<ModelRepository>();
    }

    public readonly IDiscordCache Cache;
    public readonly DiscordApiClient Rest;

    public User Author;
    public GuildMemberPartial Member;


    public readonly PKSystem System;
    public readonly SystemConfig Config;

    public readonly string DefaultPrefix;

    internal readonly ModelRepository Repository;

    public IComponentContext Services => _provider;

    public abstract Task Reply(string text = null, Embed embed = null, AllowedMentions? mentions = null, MultipartFile[]? files = null);
    public abstract Task Reply(MessageComponent[] components = null, AllowedMentions? mentions = null, MultipartFile[]? files = null);

    public LookupContext DirectLookupContextFor(SystemId systemId)
    => System?.Id == systemId ? LookupContext.ByOwner : LookupContext.ByNonOwner;
}