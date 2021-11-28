using System.Reflection;

using Autofac;

using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Mvc.Versioning;
using Microsoft.OpenApi.Models;

using Newtonsoft.Json;

using PluralKit.Core;

using Serilog;

namespace PluralKit.API;

public class Startup
{
    public Startup(IConfiguration configuration)
    {
        Configuration = configuration;
    }

    public IConfiguration Configuration { get; }

    // This method gets called by the runtime. Use this method to add services to the container.
    public void ConfigureServices(IServiceCollection services)
    {
        services.AddCors();
        services.AddAuthentication("SystemToken")
            .AddScheme<SystemTokenAuthenticationHandler.Opts,
                SystemTokenAuthenticationHandler>("SystemToken", null);

        services.AddAuthorization(options =>
        {
            options.AddPolicy("EditSystem",
                p => p.RequireAuthenticatedUser().AddRequirements(new OwnSystemRequirement()));
            options.AddPolicy("EditMember",
                p => p.RequireAuthenticatedUser().AddRequirements(new OwnSystemRequirement()));

            options.AddPolicy("ViewMembers",
                p => p.AddRequirements(new PrivacyRequirement<PKSystem>(s => s.MemberListPrivacy)));
            options.AddPolicy("ViewFront",
                p => p.AddRequirements(new PrivacyRequirement<PKSystem>(s => s.FrontPrivacy)));
            options.AddPolicy("ViewFrontHistory",
                p => p.AddRequirements(new PrivacyRequirement<PKSystem>(s => s.FrontHistoryPrivacy)));
        });
        services.AddSingleton<IAuthenticationHandler, SystemTokenAuthenticationHandler>();
        services.AddSingleton<IAuthorizationHandler, MemberOwnerHandler>();
        services.AddSingleton<IAuthorizationHandler, SystemOwnerHandler>();
        services.AddSingleton<IAuthorizationHandler, SystemPrivacyHandler>();

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

        services.AddApiVersioning();

        services.AddVersionedApiExplorer(c =>
        {
            c.GroupNameFormat = "'v'VV";
            c.ApiVersionParameterSource = new UrlSegmentApiVersionReader();
            c.SubstituteApiVersionInUrl = true;
        });

        services.AddSwaggerGen(c =>
        {
            c.SwaggerDoc("v1.0", new OpenApiInfo { Title = "PluralKit", Version = "1.0" });

            c.EnableAnnotations();
            c.AddSecurityDefinition("TokenAuth",
                new OpenApiSecurityScheme { Name = "Authorization", Type = SecuritySchemeType.ApiKey });

            // Exclude routes without a version, then fall back to group name matching (default behavior)
            c.DocInclusionPredicate((docName, apiDesc) =>
            {
                if (!apiDesc.RelativePath.StartsWith("v1/")) return false;
                return apiDesc.GroupName == docName;
            });

            // Set the comments path for the Swagger JSON and UI.
            // https://docs.microsoft.com/en-us/aspnet/core/tutorials/getting-started-with-swashbuckle?view=aspnetcore-3.1&tabs=visual-studio#customize-and-extend
            var xmlFile = $"{Assembly.GetExecutingAssembly().GetName().Name}.xml";
            var xmlPath = Path.Combine(AppContext.BaseDirectory, xmlFile);
            c.IncludeXmlComments(xmlPath);
        });
        services.AddSwaggerGenNewtonsoftSupport();
    }

    public void ConfigureContainer(ContainerBuilder builder)
    {
        builder.RegisterInstance(InitUtils.BuildConfiguration(Environment.GetCommandLineArgs()).Build())
            .As<IConfiguration>();
        builder.RegisterModule(new ConfigModule<ApiConfig>("API"));
        builder.RegisterModule(new LoggingModule("api",
            cfg: new LoggerConfiguration().Filter.ByExcluding(exc => exc.Exception is PKError || exc.Exception.IsUserError())));
        builder.RegisterModule(new MetricsModule("API"));
        builder.RegisterModule<DataStoreModule>();
        builder.RegisterModule<APIModule>();
    }

    // This method gets called by the runtime. Use this method to configure the HTTP request pipeline.
    public void Configure(IApplicationBuilder app, IWebHostEnvironment env)
    {
        if (env.IsDevelopment())
        {
            app.UseDeveloperExceptionPage();

            // Only enable Swagger stuff when ASPNETCORE_ENVIRONMENT=Development (for now)
            app.UseSwagger();
            app.UseSwaggerUI(c => { c.SwaggerEndpoint("/swagger/v1.0/swagger.json", "PluralKit (v1)"); });
        }

        // add X-PluralKit-Version header
        app.Use((ctx, next) =>
        {
            ctx.Response.Headers.Add("X-PluralKit-Version", BuildInfoService.Version);
            return next();
        });

        app.UseExceptionHandler(handler => handler.Run(async ctx =>
        {
            var exc = ctx.Features.Get<IExceptionHandlerPathFeature>();

            // handle common ISEs that are generated by invalid user input
            if (exc.Error.IsUserError())
            {
                ctx.Response.StatusCode = 400;
                await ctx.Response.WriteAsync("{\"message\":\"400: Bad Request\",\"code\":0}");
            }

            else if (exc.Error is not PKError)
            {
                ctx.Response.StatusCode = 500;
                await ctx.Response.WriteAsync("{\"message\":\"500: Internal Server Error\",\"code\":0}");
            }

            // for some reason, if we don't specifically cast to ModelParseError, it uses the base's ToJson method
            else if (exc.Error is ModelParseError fe)
            {
                ctx.Response.StatusCode = fe.ResponseCode;
                await ctx.Response.WriteAsync(JsonConvert.SerializeObject(fe.ToJson()));
            }

            else
            {
                var err = (PKError)exc.Error;
                ctx.Response.StatusCode = err.ResponseCode;

                var json = JsonConvert.SerializeObject(err.ToJson());
                await ctx.Response.WriteAsync(json);
            }

            await ctx.Response.CompleteAsync();
        }));

        app.UseMiddleware<AuthorizationTokenHandlerMiddleware>();

        //app.UseHttpsRedirection();
        app.UseCors(opts => opts.AllowAnyMethod().AllowAnyOrigin().WithHeaders("Content-Type", "Authorization"));

        app.UseRouting();
        app.UseAuthentication();
        app.UseAuthorization();
        app.UseEndpoints(endpoints => endpoints.MapControllers());
    }
}