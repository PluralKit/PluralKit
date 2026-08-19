{ inputs, ... }:
{
  perSystem =
    {
      config,
      lib,
      pkLib,
      pkgs,
      ...
    }:
    {
      process-compose."dev" =
        let
          dataDir = ".nix-process-compose";
          sourceDotenv = ''
            # shellcheck disable=SC2046
            [[ -f ".env" ]] && echo "sourcing .env file..." && export $(xargs < .env)
          '';
          rustOutputs = config.nci.outputs;
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
              cargoTarget = pkgs.stdenv.hostPlatform.rust.rustcTarget;
              mkInit =
                name: crate:
                let
                  shell = rustOutputs.${name}.devShell;
                in
                {
                  command = pkgs.writeShellApplication {
                    name = "pluralkit-${name}-init";
                    runtimeInputs = [
                      pkgs.coreutils
                      shell.stdenv.cc
                    ]
                    ++ shell.nativeBuildInputs
                    ++ crate.nativeBuildInputs;
                    text = ''
                      ${sourceDotenv}
                      set -x
                      exec cargo build --bin ${crate.bin} --target ${cargoTarget}
                    '';
                  };
                };
              mkRun =
                name: crate:
                lib.recursiveUpdate {
                  command = pkgs.writeShellApplication {
                    name = "pluralkit-${name}";
                    runtimeInputs = [ pkgs.coreutils ];
                    text = ''
                      ${sourceDotenv}
                      set -x
                      exec target/${cargoTarget}/debug/${crate.bin}
                    '';
                  };
                  depends_on."pluralkit-${name}-init".condition = "process_completed_successfully";
                  depends_on.postgres.condition = "process_healthy";
                  depends_on.redis.condition = "process_healthy";
                } (crate.runCompose.settings or { });
              services = lib.concatMapAttrs (name: crate: {
                "pluralkit-${name}-init" = mkInit name crate;
                "pluralkit-${name}" = mkRun name crate;
              }) composeCrates;
            in
            services
            // {
              ### bot ###
              pluralkit-bot-init = {
                command = pkgs.writeShellApplication {
                  name = "pluralkit-bot-init";
                  runtimeInputs = [
                    pkgs.coreutils
                    pkgs.git
                  ];
                  text = ''
                    ${sourceDotenv}
                    set -x
                    exec ${pkLib.mkBotEnv "dotnet build -c Release -o obj/"}/bin/env
                  '';
                };
              };
              pluralkit-bot = {
                command = pkgs.writeShellApplication {
                  name = "pluralkit-bot";
                  runtimeInputs = [ pkgs.coreutils ];
                  text = ''
                    ${sourceDotenv}
                    set -x
                    exec ${pkLib.mkBotEnv "dotnet obj/PluralKit.Bot.dll"}/bin/env
                  '';
                };
                depends_on.pluralkit-bot-init.condition = "process_completed_successfully";
                depends_on.postgres.condition = "process_healthy";
                depends_on.redis.condition = "process_healthy";
                depends_on.pluralkit-gateway.condition = "process_healthy";
                # TODO: add liveness check
                ready_log_line = "Received Ready";
              };

            };
        };
    };
}
