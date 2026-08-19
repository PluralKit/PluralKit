{ ... }:
{
  perSystem =
    {
      config,
      lib,
      pkgs,
      ...
    }:
    let
      binaries = lib.mapAttrs (
        name: _: config.nci.outputs.${name}.packages.release
      ) config.pluralkit.crates;
    in
    {
      packages = lib.mapAttrs' (
        name: conf:
        lib.nameValuePair "${name}-image" (
          pkgs.dockerTools.streamLayeredImage {
            name = "pluralkit-${name}";
            tag = "latest";
            contents = [
              binaries.${name}
              pkgs.dockerTools.caCertificates
            ]
            ++ conf.addlPkgs;
            config.Cmd = [ "${binaries.${name}}/bin/${conf.bin}" ];
          }
        )
      ) config.pluralkit.crates;
    };
}
