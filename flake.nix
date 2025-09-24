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
    uniffi-bindgen-cs.url = "git+https://github.com/NordSecurity/uniffi-bindgen-cs?ref=refs/tags/v0.8.3+v0.25.0&submodules=1";
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
          ...
        }:
        let
          uniffi-bindgen-cs = config.nci.lib.buildCrate {
            src = inp.uniffi-bindgen-cs;
            cratePath = "bindgen";
            # TODO: uniffi fails to build with our toolchain because the ahash dep that uniffi-bindgen-cs uses is too old and uses removed stdsimd feature
            mkRustToolchain = pkgs: pkgs.cargo;
          };

          rustOutputs = config.nci.outputs;

          sourceDotenv = ''
            # shellcheck disable=SC1091
            [[ -f ".env" ]] && echo "sourcing .env file..." && set -a && source .env && set +a
          '';
        in
        {
          treefmt = {
            projectRootFile = "flake.nix";
            programs.nixfmt.enable = true;
          };

          nci.projects."pk-services" = {
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
                pkgs.dotnet-sdk_8
                pkgs.csharpier
                pkgs.coreutils
                uniffi-bindgen-cs
              ];
              text = ''
                set -x
                commandslib="''${1:-}"
                if [ "$commandslib" == "" ]; then
                  cargo -Z unstable-options build --package commands --lib --release --artifact-dir obj/
                  commandslib="obj/libcommands.so"
                else
                  cp -f "$commandslib" obj/
                fi
                uniffi-bindgen-cs "$commandslib" --library --out-dir="''${2:-./PluralKit.Bot}"
                cargo run --package commands --bin write_cs_glue -- "''${2:-./PluralKit.Bot}"/commandtypes.cs
                dotnet format ./PluralKit.Bot/PluralKit.Bot.csproj
              '';
            };
          };

          # TODO: expose other rust packages after it's verified they build and work properly
          packages = lib.genAttrs [ "gateway" "commands" ] (name: rustOutputs.${name}.packages.release);
          # TODO: package the bot itself (dotnet)

          devShells = rec {
            services = rustOutputs."pk-services".devShell;
            bot = pkgs.mkShell {
              name = "pkbot-devshell";
              nativeBuildInputs = with pkgs; [
                coreutils
                git
                dotnet-sdk_8
                gcc
                omnisharp-roslyn
                bashInteractive
                postgresql
              ];
            };
            all = (pkgs.mkShell.override { stdenv = services.stdenv; }) {
              name = "pk-devshell";
              nativeBuildInputs = bot.nativeBuildInputs ++ services.nativeBuildInputs;
              shellHook = ''
                ${sourceDotenv}
              '';
            };
            docs = pkgs.mkShellNoCC {
              buildInputs = with pkgs; [ nodejs yarn ];
              NODE_OPTIONS = "--openssl-legacy-provider";
            };
          };

          process-compose."dev" =
            let
              dataDir = ".nix-process-compose";
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
                  mkServiceProcess =
                    name: attrs:
                    attrs
                    // {
                      command = pkgs.writeShellApplication {
                        name = "pluralkit-${name}";
                        runtimeInputs = [ pkgs.coreutils ];
                        text = ''
                          ${sourceDotenv}
                          set -x
                          nix develop .#services -c cargo run --package ${name}
                        '';
                      };
                    };
                in
                {
                  ### bot ###
                  pluralkit-bot = {
                    command = pkgs.writeShellApplication {
                      name = "pluralkit-bot";
                      runtimeInputs = [ pkgs.coreutils ];
                      text = ''
                        ${sourceDotenv}
                        set -x
                        ${self'.apps.generate-command-parser-bindings.program}
                        nix develop .#bot -c bash -c "dotnet build ./PluralKit.Bot/PluralKit.Bot.csproj -c Release -o obj/ && dotnet obj/PluralKit.Bot.dll"
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
                  ### gateway ###
                  pluralkit-gateway = mkServiceProcess "gateway" {
                    depends_on.postgres.condition = "process_healthy";
                    depends_on.redis.condition = "process_healthy";
                    # configure health checks
                    # TODO: don't assume port?
                    liveness_probe.exec.command = ''${pkgs.curl}/bin/curl -s -o /dev/null -w "%{http_code}" http://localhost:5000/stats | ${pkgs.busybox}/bin/grep "302"'';
                    liveness_probe.period_seconds = 7;
                    # TODO: add actual listening or running line in gateway
                    ready_log_line = "Running ";
                    availability.restart = "on_failure";
                    availability.max_restarts = 3;
                  };
                  # TODO: add the rest of the services
                };
            };
        };
    };
}
