using Autofac;
using Autofac.Extensions.DependencyInjection;

using Microsoft.Extensions.DependencyInjection;

namespace PluralKit.Core
{
    public class DataStoreModule: Module
    {
        protected override void Load(ContainerBuilder builder)
        {
            builder.RegisterType<DbConnectionCountHolder>().SingleInstance();
            builder.RegisterType<DatabaseMigrator>().SingleInstance();
            builder.RegisterType<Database>().As<IDatabase>().SingleInstance();
            builder.RegisterType<ModelRepository>().AsSelf().SingleInstance();

            builder.RegisterType<DispatchService>().AsSelf().SingleInstance();

            builder.Populate(new ServiceCollection().AddMemoryCache());
        }
    }
}