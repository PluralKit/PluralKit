{ inputs, ... }:
{
  perSystem = { lib, config, ... }: {
    nci = {
      toolchainConfig = ../rust-toolchain.toml;

      projects."pluralkit-services" = {
        path = inputs.self;
        export = false;
      };

      crates = lib.mapAttrs (_: crate: {
        export = true;
        drvConfig.mkDerivation = {
          inherit (crate) buildInputs nativeBuildInputs;
        };
        depsDrvConfig.mkDerivation = {
          inherit (crate) buildInputs nativeBuildInputs;
        };
      }) config.pluralkit.crates;
    };
  };
}
