{ inputs, ... }:
{
  perSystem =
    {
      config,
      lib,
      pkgs,
      self',
      ...
    }:
    {
      process-compose."dev" =
        let
          dataDir = ".nix-process-compose";
        in
        {
          imports = [ inputs.services.processComposeModules.default ];

          settings.log_location = "${dataDir}/log";

          settings.environment = {
            DOTNET_CLI_TELEMETRY_OPTOUT = "1";
            NODE_OPTIONS = "--openssl-legacy-provider";
          };

          services.redis."redis" = {
            enable = true;
            dataDir = "${dataDir}/redis";
          };
          services.postgres."postgres" = {
            enable = true;
            dataDir = "${dataDir}/postgres";
            initialScript.before = ''
              CREATE DATABASE pluralkit;
              CREATE USER postgres WITH password 'postgres';
              GRANT ALL PRIVILEGES ON DATABASE pluralkit TO postgres;
              ALTER DATABASE pluralkit OWNER TO postgres;
            '';
          };

          settings.processes =
            let
              composeCrates = lib.filterAttrs (_: crate: crate.runCompose.enable) config.pluralkit.crates;
              mkRun =
                name: crate:
                lib.recursiveUpdate {
                  command = pkgs.writeShellApplication {
                    name = "pluralkit-${name}";
                    runtimeInputs = [ pkgs.coreutils ];
                    text = ''
                      set -x
                      nix develop .#services -c cargo run --package ${name}
                    '';
                  };
                  depends_on.postgres.condition = "process_healthy";
                  depends_on.redis.condition = "process_healthy";
                  availability.restart = "on_failure";
                  availability.max_restarts = 3;
                } (crate.runCompose.settings or { });
              services = lib.concatMapAttrs (name: crate: {
                "pluralkit-${name}" = mkRun name crate;
              }) composeCrates;
            in
            services
            // {
              ### bot ###
              pluralkit-bot = {
                command = pkgs.writeShellApplication {
                  name = "pluralkit-bot";
                  runtimeInputs = [ pkgs.coreutils ];
                  text = ''
                    set -x
                    ${self'.apps.generate-command-parser-bindings.program}
                    nix develop .#bot -c dotnet run --project ./PluralKit.Bot
                  '';
                };
                depends_on.postgres.condition = "process_healthy";
                depends_on.redis.condition = "process_healthy";
                depends_on.pluralkit-gateway.condition = "process_log_ready";
                # TODO: add liveness check
                ready_log_line = "Connected! All is good (probably).";
                availability.restart = "on_failure";
                availability.max_restarts = 3;
              };
            };
        };
    };
}
