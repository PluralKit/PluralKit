using Autofac;

using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

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
            services.AddControllers()
                .SetCompatibilityVersion(CompatibilityVersion.Latest)
                .AddNewtonsoftJson(); // sorry MS, this just does *more*
        }

        public void ConfigureContainer(ContainerBuilder builder)
        {
            builder.RegisterInstance(Configuration);
            builder.RegisterModule(new ConfigModule<object>());
            builder.RegisterModule(new LoggingModule("api"));
            builder.RegisterModule(new MetricsModule("API"));
            builder.RegisterModule<DataStoreModule>();
            builder.RegisterModule<APIModule>();
        }

        // This method gets called by the runtime. Use this method to configure the HTTP request pipeline.
        public void Configure(IApplicationBuilder app, IWebHostEnvironment env)
        {
            SchemaService.Initialize();

            if (env.IsDevelopment())
            {
                app.UseDeveloperExceptionPage();
            }
            else
            {
                // The default HSTS value is 30 days. You may want to change this for production scenarios, see https://aka.ms/aspnetcore-hsts.
                //app.UseHsts();
            }

            //app.UseHttpsRedirection();
            app.UseCors(opts => opts.AllowAnyMethod().AllowAnyOrigin().WithHeaders("Content-Type", "Authorization"));
            app.UseMiddleware<TokenAuthService>();
            
            app.UseRouting();
            app.UseEndpoints(endpoints => endpoints.MapControllers());
        }
    }
}