{
  description = "flake for pluralkit";

  inputs = {
    nixpkgs.url = "nixpkgs/nixpkgs-unstable";
    parts.url = "github:hercules-ci/flake-parts";
    systems.url = "github:nix-systems/x86_64-linux";
    # process compose
    process-compose.url = "github:Platonic-Systems/process-compose-flake";
    services.url = "github:juspay/services-flake";
    # rust
    d2n.url = "github:nix-community/dream2nix";
    d2n.inputs.nixpkgs.follows = "nixpkgs";
    nci.url = "github:yusdacra/nix-cargo-integration";
    nci.inputs.parts.follows = "parts";
    nci.inputs.nixpkgs.follows = "nixpkgs";
    nci.inputs.dream2nix.follows = "d2n";
    nci.inputs.treefmt.follows = "treefmt";
    uniffi-bindgen-cs.url = "git+https://github.com/NordSecurity/uniffi-bindgen-cs?tag=v0.8.3+v0.25.0&submodules=1";
    uniffi-bindgen-cs.flake = false;
    # misc
    treefmt.url = "github:numtide/treefmt-nix";
    treefmt.inputs.nixpkgs.follows = "nixpkgs";
    flake-compat.url = "https://flakehub.com/f/edolstra/flake-compat/1.tar.gz";
  };

  outputs =
    inp:
    inp.parts.lib.mkFlake { inputs = inp; } {
      systems = import inp.systems;
      imports = [
        inp.process-compose.flakeModule
        inp.nci.flakeModule
        inp.treefmt.flakeModule
      ];
      perSystem =
        {
          config,
          self',
          pkgs,
          lib,
          system,
          ...
        }:
        let
          # this is used as devshell for bot, and in the process-compose processes as environment
          mkBotEnv =
            cmd:
            pkgs.buildFHSEnv {
              name = "env";
              targetPkgs =
                pkgs: with pkgs; [
                  coreutils
                  git
                  dotnet-sdk_8
                  gcc
                  omnisharp-roslyn
                  bashInteractive
                ];
              runScript = cmd;
            };
          uniffi-bindgen-cs = config.nci.lib.buildCrate {
            src = inp.uniffi-bindgen-cs;
            cratePath = "bindgen";
            # TODO: uniffi fails to build with our toolchain because the ahash dep that uniffi-bindgen-cs uses is too old and uses removed stdsimd feature
            mkRustToolchain = pkgs: pkgs.cargo;
          };

          rustOutputs = config.nci.outputs;
          composeCfg = config.process-compose."dev";
        in
        {
          treefmt = {
            projectRootFile = "flake.nix";
            programs.nixfmt.enable = true;
          };

          nci.projects."pluralkit-services" = {
            path = ./.;
            export = false;
          };
          nci.crates."commands" = rec {
            depsDrvConfig.env = {
              # we don't really need this since the lib is just used to generate the bindings
              doNotRemoveReferencesToVendorDir = true;
            };
            depsDrvConfig.mkDerivation = {
              # also not really needed
              dontPatchShebangs = true;
            };
            drvConfig = depsDrvConfig;
          };

          apps = {
            generate-command-parser-bindings.program = pkgs.writeShellApplication {
              name = "generate-command-parser-bindings";
              runtimeInputs = [
                (config.nci.toolchains.mkBuild pkgs)
                self'.devShells.services.stdenv.cc
                pkgs.csharpier
                pkgs.coreutils
                uniffi-bindgen-cs
              ];
              text = ''
                set -x
                [ "''${1:-}" == "" ] && cargo build --package commands --release
                uniffi-bindgen-cs "''${1:-target/debug/libcommands.so}" --library --out-dir="''${2:-./PluralKit.Bot}"
              '';
            };
          };

          # TODO: expose other rust packages after it's verified they build and work properly
          packages = lib.genAttrs [ "gateway" "commands" ] (name: rustOutputs.${name}.packages.release);
          # TODO: package the bot itself (dotnet)

          devShells = {
            services = rustOutputs."pluralkit-services".devShell;
            bot = (mkBotEnv "bash").env;
          };

          process-compose."dev" =
            let
              dataDir = ".nix-process-compose";
              pluralkitConfCheck = ''
                [[ -f "pluralkit.conf" ]] || (echo "pluralkit config not found, please copy pluralkit.conf.example to pluralkit.conf and edit it" && exit 1)
              '';
              sourceDotenv = ''
                [[ -f ".env" ]] && echo "sourcing .env file..." && export "$(xargs < .env)"
              '';
            in
            {
              imports = [ inp.services.processComposeModules.default ];

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
                  procCfg = composeCfg.settings.processes;
                  mkServiceInitProcess =
                    {
                      name,
                      inputs ? [ ],
                      ...
                    }:
                    let
                      shell = rustOutputs.${name}.devShell;
                    in
                    {
                      command = pkgs.writeShellApplication {
                        name = "pluralkit-${name}-init";
                        runtimeInputs =
                          (with pkgs; [
                            coreutils
                            shell.stdenv.cc
                          ])
                          ++ shell.nativeBuildInputs
                          ++ inputs;
                        text = ''
                          ${sourceDotenv}
                          set -x
                          ${pluralkitConfCheck}
                          exec cargo build --package ${name}
                        '';
                      };
                    };
                in
                {
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
                        ${pluralkitConfCheck}
                        ${self'.apps.generate-command-parser-bindings.program}
                        exec ${mkBotEnv "dotnet build -c Release -o obj/"}/bin/env
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
                        exec ${mkBotEnv "dotnet obj/PluralKit.Bot.dll"}/bin/env
                      '';
                    };
                    depends_on.pluralkit-bot-init.condition = "process_completed_successfully";
                    depends_on.postgres.condition = "process_healthy";
                    depends_on.redis.condition = "process_healthy";
                    depends_on.pluralkit-gateway.condition = "process_healthy";
                    # TODO: add liveness check
                    ready_log_line = "Received Ready";
                  };
                  ### gateway ###
                  pluralkit-gateway-init = mkServiceInitProcess {
                    name = "gateway";
                  };
                  pluralkit-gateway = {
                    command = pkgs.writeShellApplication {
                      name = "pluralkit-gateway";
                      runtimeInputs = with pkgs; [
                        coreutils
                        curl
                        gnugrep
                      ];
                      text = ''
                        ${sourceDotenv}
                        set -x
                        exec target/debug/gateway
                      '';
                    };
                    depends_on.postgres.condition = "process_healthy";
                    depends_on.redis.condition = "process_healthy";
                    depends_on.pluralkit-gateway-init.condition = "process_completed_successfully";
                    # configure health checks
                    # TODO: don't assume port?
                    liveness_probe.exec.command = ''curl -s -o /dev/null -w "%{http_code}" http://localhost:5000/stats | grep "302"'';
                    liveness_probe.period_seconds = 5;
                    readiness_probe.exec.command = procCfg.pluralkit-gateway.liveness_probe.exec.command;
                    readiness_probe.period_seconds = 5;
                    readiness_probe.initial_delay_seconds = 3;
                  };
                  # TODO: add the rest of the services
                };
            };
        };
    };
}
