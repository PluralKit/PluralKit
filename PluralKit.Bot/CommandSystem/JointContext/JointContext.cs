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
    public JointContext(ILifetimeScope provider, PKSystem system, SystemConfig config)
    {
        _provider = provider;
        _metrics = provider.Resolve<IMetrics>();
        Cache = provider.Resolve<IDiscordCache>();
        Rest = provider.Resolve<DiscordApiClient>();
        System = system;
        Config = config;
        Repository = provider.Resolve<ModelRepository>();
    }

    public readonly IDiscordCache Cache;
    public readonly DiscordApiClient Rest;

    public User Author;
    public GuildMemberPartial Member;


    public readonly PKSystem System;
    public readonly SystemConfig Config;

    internal readonly ModelRepository Repository;

    public IComponentContext Services => _provider;

    public abstract Task Reply(string text = null, Embed embed = null, AllowedMentions? mentions = null, MultipartFile[]? files = null);
    public abstract Task Reply(MessageComponent[] components = null, AllowedMentions? mentions = null, MultipartFile[]? files = null);

    public LookupContext DirectLookupContextFor(SystemId systemId)
    => System?.Id == systemId ? LookupContext.ByOwner : LookupContext.ByNonOwner;
}