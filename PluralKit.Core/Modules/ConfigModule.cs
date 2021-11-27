using Autofac;

using Microsoft.Extensions.Configuration;

namespace PluralKit.Core;

public class ConfigModule<T>: Module where T : new()
{
    private readonly string _submodule;

    public ConfigModule(string submodule = null)
    {
        _submodule = submodule;
    }

    protected override void Load(ContainerBuilder builder)
    {
        // We're assuming IConfiguration is already available somehow - it comes from various places (auto-injected in ASP, etc)

        // Register the CoreConfig and where to find it
        builder.Register(c =>
                c.Resolve<IConfiguration>().GetSection("PluralKit").Get<CoreConfig>() ?? new CoreConfig())
            .SingleInstance();

        // Register the submodule config (BotConfig, etc) if specified
        if (_submodule != null)
            builder.Register(c =>
                    c.Resolve<IConfiguration>().GetSection("PluralKit").GetSection(_submodule).Get<T>() ?? new T())
                .SingleInstance();
    }
}