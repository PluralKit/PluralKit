using Autofac;

using Microsoft.AspNetCore.Diagnostics;

using Newtonsoft.Json;

using PluralKit.Core;

using Serilog;

namespace PluralKit.API;

public class Startup
{
    // This method gets called by the runtime. Use this method to add services to the container.
    public void ConfigureServices(IServiceCollection services)
    {
        services.AddControllers()
            // sorry MS, this just does *more*
            .AddNewtonsoftJson(opts =>
            {
                // ... though by default it messes up timestamps in JSON
                opts.SerializerSettings.DateParseHandling = DateParseHandling.None;
            })
            .ConfigureApiBehaviorOptions(options =>
                options.InvalidModelStateResponseFactory = context =>
                    throw Errors.GenericBadRequest
            );

        services.AddHostedService<MetricsRunner>();
    }

    public void ConfigureContainer(ContainerBuilder builder)
    {
        builder.RegisterInstance(InitUtils.BuildConfiguration(Environment.GetCommandLineArgs()).Build())
            .As<IConfiguration>();
        builder.RegisterModule(new ConfigModule<ApiConfig>("API"));
        builder.RegisterModule(new LoggingModule("dotnet-api",
            cfg: new LoggerConfiguration().Filter.ByExcluding(
                exc => exc.Exception is PKError || exc.Exception.IsUserError()
        )));
        builder.RegisterModule<DataStoreModule>();
        builder.RegisterModule(new MetricsModule());
    }

    // This method gets called by the runtime. Use this method to configure the HTTP request pipeline.
    public void Configure(IApplicationBuilder app, IWebHostEnvironment env)
    {
        // add X-PluralKit-Version header
        app.Use((ctx, next) =>
        {
            ctx.Response.Headers.Append("X-PluralKit-Version", BuildInfoService.FullVersion);
            return next();
        });

        app.UseExceptionHandler(handler => handler.Run(async ctx =>
        {
            var exc = ctx.Features.Get<IExceptionHandlerPathFeature>();

            // handle common ISEs that are generated by invalid user input
            if (exc.Error.IsUserError())
                await ctx.Response.WriteJSON(400, "{\"message\":\"400: Bad Request\",\"code\":0}");

            else if (exc.Error is not PKError)
            {
                await ctx.Response.WriteJSON(500, "{\"message\":\"500: Internal Server Error\",\"code\":0}");

                var sentryEvent = new SentryEvent(exc.Error);
                SentrySdk.CaptureEvent(sentryEvent);
            }

            // for some reason, if we don't specifically cast to ModelParseError, it uses the base's ToJson method
            else if (exc.Error is ModelParseError fe)
                await ctx.Response.WriteJSON(fe.ResponseCode, JsonConvert.SerializeObject(fe.ToJson()));

            else
            {
                var err = (PKError)exc.Error;
                await ctx.Response.WriteJSON(err.ResponseCode, JsonConvert.SerializeObject(err.ToJson()));
            }

            await ctx.Response.CompleteAsync();
        }));

        app.UseMiddleware<AuthorizationTokenHandlerMiddleware>();

        app.UseRouting();
        app.UseEndpoints(endpoints =>
        {
            // register base / legacy routes
            endpoints.MapMethods("", new string[] { }, (context) => { context.Response.Redirect("https://pluralkit.me/api"); return Task.CompletedTask; });
            endpoints.MapMethods("v1/{*_}", new string[] { }, (context) => context.Response.WriteJSON(410, "{\"message\":\"Unsupported API version\",\"code\":0}"));

            // register controllers
            endpoints.MapControllers();
        });
    }
}