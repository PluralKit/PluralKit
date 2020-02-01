using Autofac;

namespace PluralKit.API
{
    public class APIModule: Module
    {
        protected override void Load(ContainerBuilder builder)
        {
            // Lifetime scope so the service, RequiresSystem, and handler itself all get the same value
            builder.RegisterType<TokenAuthService>().AsSelf().InstancePerLifetimeScope();
        }
    }
}