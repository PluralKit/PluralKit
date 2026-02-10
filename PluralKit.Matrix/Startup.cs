using Autofac;

using PluralKit.Core;

using Serilog;

namespace PluralKit.Matrix;

public class Startup
{
    public void ConfigureServices(IServiceCollection services)
    {
        services.AddControllers();
    }

    public void ConfigureContainer(ContainerBuilder builder)
    {
        builder.RegisterInstance(InitUtils.BuildConfiguration(Environment.GetCommandLineArgs()).Build())
            .As<IConfiguration>();
        builder.RegisterModule(new ConfigModule<MatrixConfig>("Matrix"));
        builder.RegisterModule(new LoggingModule("dotnet-matrix"));
        builder.RegisterModule<DataStoreModule>();
        builder.RegisterModule(new MetricsModule());
        builder.RegisterModule<MatrixModule>();
    }

    public void Configure(IApplicationBuilder app, IWebHostEnvironment env)
    {
        app.UseRouting();
        app.UseEndpoints(endpoints =>
        {
            endpoints.MapControllers();
        });
    }
}
