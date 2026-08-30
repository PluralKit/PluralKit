{ inputs, ... }:
{
  perSystem =
    {
      config,
      pkgs,
      self',
      ...
    }:
    let
      uniffi-bindgen-cs = config.nci.lib.buildCrate {
        src = inputs.uniffi-bindgen-cs;
        cratePath = "bindgen";
      };
    in
    {
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
          '';
        };
      };
    };
}
