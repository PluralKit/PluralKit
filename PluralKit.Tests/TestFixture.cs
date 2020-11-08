using System;
using System.Collections.Generic;
using System.Net.Http;

using Autofac;

using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;

using PluralKit.API;
using PluralKit.Core;

namespace PluralKit.Tests
{
    public class TestFixture: IDisposable
    {
        private readonly IHost _apiHost;
        
        public ILifetimeScope Services { get; }
        public HttpClient Client { get; }

        public TestFixture()
        {
            var config = InitUtils.BuildConfiguration(Environment.GetCommandLineArgs()).Build();
            InitUtils.InitStatic();

            var builder = new ContainerBuilder();
            builder.RegisterInstance<IConfiguration>(config);
            builder.RegisterModule(new LoggingModule("test"));
            builder.RegisterModule(new ConfigModule<CoreConfig>());
            builder.RegisterModule<TestModule>();
            Services = builder.Build();

            var database = (TestDatabase) Services.Resolve<IDatabase>();
            database.ApplyMigrations().Wait();
            
            _apiHost = Program.CreateHostBuilder(Environment.GetCommandLineArgs())
                .ConfigureAppConfiguration(x=> x.AddInMemoryCollection(new[]
                {
                    new KeyValuePair<string, string>("PluralKit:Database", database.ConnectionString)
                }))
                .ConfigureWebHost(x => x.UseTestServer())
                .Build();
            _apiHost.Start();
            Client = _apiHost.GetTestClient();
        }

        public void Dispose()
        {
            _apiHost.Dispose();
            Services.Dispose();
        }
    }
}