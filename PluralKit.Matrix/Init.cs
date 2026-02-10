using System.Security.Cryptography;

using Autofac.Extensions.DependencyInjection;

using PluralKit.Core;

using Sentry;

using Serilog;

namespace PluralKit.Matrix;

public class Program
{
    public static async Task Main(string[] args)
    {
        if (args.Contains("--generate-registration"))
        {
            GenerateRegistration(args);
            return;
        }

        InitUtils.InitStatic();
        await BuildInfoService.LoadVersion();

        var host = CreateHostBuilder(args).Build();
        var config = host.Services.GetRequiredService<CoreConfig>();

        using var _ = SentrySdk.Init(opts =>
        {
            opts.Dsn = config.SentryUrl ?? "";
            opts.Release = BuildInfoService.FullVersion;
            opts.AutoSessionTracking = true;
        });

        // Initialize Redis
        await host.Services.GetRequiredService<RedisService>().InitAsync(config);

        // Run Matrix-specific database migrations
        var logger = host.Services.GetRequiredService<ILogger>().ForContext<Program>();
        logger.Information("Running Matrix database migrations...");
        var db = host.Services.GetRequiredService<IDatabase>();
        var matrixMigrator = host.Services.GetRequiredService<MatrixMigrator>();
        await db.Execute(conn => matrixMigrator.ApplyMigrations(conn));
        logger.Information("Matrix database migrations complete");

        var matrixConfig = host.Services.GetRequiredService<MatrixConfig>();
        logger.Information("Starting PluralKit Matrix appservice on port {Port}", matrixConfig.Port);

        await host.RunAsync();
    }

    public static IHostBuilder CreateHostBuilder(string[] args) =>
        Host.CreateDefaultBuilder(args)
            .UseServiceProviderFactory(new AutofacServiceProviderFactory())
            .UseSerilog()
            .ConfigureWebHostDefaults(whb => whb
                .UseConfiguration(InitUtils.BuildConfiguration(args).Build())
                .ConfigureKestrel(opts =>
                {
                    opts.ListenAnyIP(opts.ApplicationServices.GetRequiredService<MatrixConfig>().Port);
                })
                .UseStartup<Startup>());

    private static void GenerateRegistration(string[] args)
    {
        var config = InitUtils.BuildConfiguration(args).Build();
        var matrixConfig = config.GetSection("PluralKit").GetSection("Matrix").Get<MatrixConfig>() ?? new MatrixConfig();

        var asToken = Convert.ToHexString(RandomNumberGenerator.GetBytes(32)).ToLower();
        var hsToken = Convert.ToHexString(RandomNumberGenerator.GetBytes(32)).ToLower();

        var yaml = $@"id: ""pluralkit""
hs_token: ""{hsToken}""
as_token: ""{asToken}""
url: ""http://localhost:{matrixConfig.Port}""
sender_localpart: ""{matrixConfig.BotLocalpart}""
namespaces:
  users:
    - exclusive: true
      regex: ""@_pk_.*""
  aliases: []
  rooms: []
rate_limited: false
";

        var outputPath = "pluralkit-registration.yaml";
        File.WriteAllText(outputPath, yaml);
        Console.WriteLine($"Registration file written to {outputPath}");
        Console.WriteLine($"AS Token: {asToken}");
        Console.WriteLine($"HS Token: {hsToken}");
        Console.WriteLine();
        Console.WriteLine("Add these to your PluralKit configuration:");
        Console.WriteLine($"  PluralKit__Matrix__AsToken={asToken}");
        Console.WriteLine($"  PluralKit__Matrix__HsToken={hsToken}");
        Console.WriteLine();
        Console.WriteLine("Register this file with your Synapse homeserver in homeserver.yaml:");
        Console.WriteLine("  app_service_config_files:");
        Console.WriteLine($"    - /path/to/{outputPath}");
    }
}
