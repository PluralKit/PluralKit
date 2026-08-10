{ flake-parts-lib, ... }:
{
  options.perSystem = flake-parts-lib.mkPerSystemOption (
    { lib, ... }: {
      options.pluralkit = {
        crates = lib.mkOption {
          description = "Rust crates to package by cargo package name";
          default = { };
          type = lib.types.attrsOf (
            lib.types.submodule (
              { name, ... }:
              {
                options = {
                  buildInputs = lib.mkOption {
                    description = "Extra buildInputs for this crate";
                    type = lib.types.listOf lib.types.package;
                    default = [ ];
                  };
                  nativeBuildInputs = lib.mkOption {
                    description = "Extra nativeBuildInputs for this crate";
                    type = lib.types.listOf lib.types.package;
                    default = [ ];
                  };
                  addlPkgs = lib.mkOption {
                    description = "Extra packages to include in the docker image";
                    type = lib.types.listOf lib.types.package;
                    default = [ ];
                  };
                  bin = lib.mkOption {
                    description = "Binary to run as the image's Cmd";
                    type = lib.types.str;
                    default = name;
                  };
                  runCompose = lib.mkOption {
                    description = "Run this crate as a process in the dev compose stack";
                    default = { };
                    type = lib.types.submodule {
                      options = {
                        enable = lib.mkEnableOption "run ${name} in the dev compose stack";
                        runtimeInputs = lib.mkOption {
                          type = lib.types.listOf lib.types.package;
                          default = [ ];
                        };
                        settings = lib.mkOption {
                          type = lib.types.attrs;
                          default = { };
                          description = "Extra process-compose settings";
                        };
                      };
                    };
                  };
                };
              }
            )
          );
        };
      };
    }
  );
}
