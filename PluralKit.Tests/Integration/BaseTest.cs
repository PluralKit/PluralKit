using Autofac;

using PluralKit.Core;
using PluralKit.Tests.API;

using Xunit;

namespace PluralKit.Tests.Integration
{
    [Collection(nameof(TestCollection))]
    public class BaseTest
    {
        protected ILifetimeScope Services { get; }
        protected ApiClient ApiClient { get; }
        protected IDatabase Database => Services.Resolve<IDatabase>();
        protected ModelRepository Repo => Services.Resolve<ModelRepository>();
        
        public BaseTest(TestFixture fixture)
        {
            Services = fixture.Services;
            ApiClient = new ApiClient(fixture.Client);
        }
    }
}