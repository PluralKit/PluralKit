using System;
using System.Collections.Generic;
using System.Data;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.HttpsPolicy;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using NodaTime;
using NodaTime.Serialization.JsonNet;
using Npgsql;

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
            services.AddMvc(opts => { })
                .SetCompatibilityVersion(CompatibilityVersion.Version_2_2)
                .AddJsonOptions(opts => { opts.SerializerSettings.BuildSerializerSettings(); });

            services
                .AddTransient<SystemStore>()
                .AddTransient<MemberStore>()
                .AddTransient<SwitchStore>()
                .AddTransient<MessageStore>()

                .AddScoped<TokenAuthService>()

                .AddTransient(_ => Configuration.GetSection("PluralKit").Get<CoreConfig>() ?? new CoreConfig())
                .AddSingleton(svc => new DbConnectionFactory(svc.GetRequiredService<CoreConfig>().Database));
        }

        // This method gets called by the runtime. Use this method to configure the HTTP request pipeline.
        public void Configure(IApplicationBuilder app, IHostingEnvironment env)
        {
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
            app.UseCors(opts => opts.AllowAnyMethod().AllowAnyOrigin());
            app.UseMiddleware<TokenAuthService>();
            app.UseMvc();
        }
    }
}