using System;
using System.IO;
using System.Reflection;

using Autofac;

using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Versioning;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.OpenApi.Models;

using Newtonsoft.Json.Serialization;

using PluralKit.API.Middleware;
using PluralKit.Core;

namespace PluralKit.API
{
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
                .AddScheme<SystemTokenAuthenticationHandler.Opts, SystemTokenAuthenticationHandler>("SystemToken", null);
            
            services.AddAuthorization(options =>
            {
                options.AddPolicy(AuthPolicies.EditSystem, p => p.RequireAuthenticatedUser().AddRequirements(new OwnSystemRequirement()));
                options.AddPolicy(AuthPolicies.EditMember, p => p.RequireAuthenticatedUser().AddRequirements(new OwnSystemRequirement()));
                options.AddPolicy(AuthPolicies.DeleteMember, p => p.RequireAuthenticatedUser().AddRequirements(new OwnSystemRequirement()));
                
                options.AddPolicy(AuthPolicies.ViewMembers, p => p.AddRequirements(new PrivacyRequirement<PKSystem>(s => s.MemberListPrivacy)));
                options.AddPolicy(AuthPolicies.ViewGroups, p => p.AddRequirements(new PrivacyRequirement<PKSystem>(s => s.GroupListPrivacy)));
                options.AddPolicy(AuthPolicies.ViewFront, p => p.AddRequirements(new PrivacyRequirement<PKSystem>(s => s.FrontPrivacy)));
                options.AddPolicy(AuthPolicies.ViewFrontHistory, p => p.AddRequirements(new PrivacyRequirement<PKSystem>(s => s.FrontHistoryPrivacy)));
            });
            services.AddSingleton<IAuthenticationHandler, SystemTokenAuthenticationHandler>();
            services.AddSingleton<IAuthorizationHandler, MemberOwnerHandler>();
            services.AddSingleton<IAuthorizationHandler, SystemOwnerHandler>();
            services.AddSingleton<IAuthorizationHandler, SystemPrivacyHandler>();
            
            services.AddControllers(opts =>
                {
                    opts.Filters.Add(new ApiErrorExceptionFilter());
                })
                .SetCompatibilityVersion(CompatibilityVersion.Latest)
                // sorry MS, this just does *more*
                .AddNewtonsoftJson(opts =>
                {
                    opts.SerializerSettings.ContractResolver = new PartialContractResolver
                    {
                        NamingStrategy = new SnakeCaseNamingStrategy()
                    };
                });

            services.AddApiVersioning();
            
            services.AddVersionedApiExplorer(c =>
            {
                c.GroupNameFormat = "'v'VV";
                c.ApiVersionParameterSource = new UrlSegmentApiVersionReader();
                c.SubstituteApiVersionInUrl = true;
            });
            
            services.AddSwaggerGen(c =>
            {
                c.SwaggerDoc("v1.0", new OpenApiInfo {Title = "PluralKit", Version = "1.0"});
                
                c.EnableAnnotations();
                c.AddSecurityDefinition("TokenAuth",
                    new OpenApiSecurityScheme {Name = "Authorization", Type = SecuritySchemeType.ApiKey});
                
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

        public virtual void ConfigureContainer(ContainerBuilder builder)
        {
            builder.RegisterInstance(Configuration).As<IConfiguration>();
            builder.RegisterModule(new ConfigModule<ApiConfig>("API"));
            builder.RegisterModule(new LoggingModule("api"));
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
                app.UseSwaggerUI(c =>
                {
                    c.SwaggerEndpoint("/swagger/v1.0/swagger.json", "PluralKit (v1)");
                });
            }
            else
            {
                // The default HSTS value is 30 days. You may want to change this for production scenarios, see https://aka.ms/aspnetcore-hsts.
                //app.UseHsts();
            }

            //app.UseHttpsRedirection();
            app.UseCors(opts => opts.AllowAnyMethod().AllowAnyOrigin().WithHeaders("Content-Type", "Authorization"));
            
            app.UseRouting();
            app.UseAuthentication();
            app.UseAuthorization();
            app.UseEndpoints(endpoints => endpoints.MapControllers());
        }
    }
}