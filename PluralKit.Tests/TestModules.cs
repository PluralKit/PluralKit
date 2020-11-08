using Autofac;

using PluralKit.Core;

namespace PluralKit.Tests
{
    public class TestModule: Module
    {
        protected override void Load(ContainerBuilder builder)
        {
            builder.RegisterType<TestDatabase>().As<IDatabase>().SingleInstance();
            builder.RegisterType<ModelRepository>().AsSelf();
        }
    }
}