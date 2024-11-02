{
  inputs.nixpkgs.url = "github:nixos/nixpkgs/nixos-unstable";
  inputs.nci.url = "github:yusdacra/nix-cargo-integration";
  inputs.nci.inputs.nixpkgs.follows = "nixpkgs";
  inputs.parts.url = "github:hercules-ci/flake-parts";
  inputs.parts.inputs.nixpkgs-lib.follows = "nixpkgs";

  outputs = inputs @ {
    parts,
    nci,
    ...
  }:
    parts.lib.mkFlake {inherit inputs;} {
      systems = ["x86_64-linux"];
      imports = [nci.flakeModule];
      perSystem = {
        config,
        pkgs,
        ...
      }: let
        rustOutputs = config.nci.outputs;
        rustDeps = with pkgs; [rust-analyzer];
        webDeps = with pkgs; [yarn nodejs];
        csDeps = with pkgs; [gcc protobuf dotnet-sdk_6 go];
      in {
        nci.toolchainConfig = ./rust-toolchain.toml;
        nci.projects."pluralkit" = {
          path = ./.;
          export = true;
        };
        # configure crates
        nci.crates = {
          # see for usage: https://github.com/yusdacra/nix-cargo-integration/blob/master/examples/simple-workspace/crates.nix
        };

        devShells.default = rustOutputs."pluralkit".devShell.overrideAttrs (old: {
          nativeBuildInputs = old.nativeBuildInputs ++ webDeps ++ csDeps ++ rustDeps;
        });
        devShells.web = pkgs.mkShell {nativeBuildInputs = webDeps;};
        devShells.rust = rustOutputs."pluralkit".devShell.overrideAttrs (old: { nativeBuildInputs = old.nativeBuildInputs ++ rustDeps; });
        devShells.cs= pkgs.mkShell {
          nativeBuildInputs = csDeps;
        };
      };
    };
}
