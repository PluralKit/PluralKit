{
  description = "flake for pluralkit";

  inputs = {
    nixpkgs.url = "https://channels.nixos.org/nixpkgs-unstable/nixexprs.tar.xz";
    parts.url = "github:hercules-ci/flake-parts";
    systems.url = "github:nix-systems/default";
    # process compose
    process-compose.url = "github:Platonic-Systems/process-compose-flake";
    services.url = "github:juspay/services-flake";
    # rust
    d2n.url = "github:nix-community/dream2nix";
    d2n.inputs.nixpkgs.follows = "nixpkgs";
    nci.url = "github:90-008/nix-cargo-integration";
    nci.inputs.parts.follows = "parts";
    nci.inputs.nixpkgs.follows = "nixpkgs";
    nci.inputs.dream2nix.follows = "d2n";
    nci.inputs.treefmt.follows = "treefmt";
    uniffi-bindgen-cs.url = "git+https://github.com/90-008/uniffi-bindgen-cs?ref=refs/heads/main&submodules=1";
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
        ./nix
      ];
      perSystem =
        {
          pkgs,
          ...
        }:
        {
          treefmt = {
            projectRootFile = "flake.nix";
            programs.nixfmt.enable = true;
          };

          pluralkit.crates = {
            api = { };
            avatars = { };
            dispatch = { };
            gateway = {
              runCompose = {
                enable = true;
                settings =
                  let
                    probeCmd = ''curl -s -o /dev/null -w "%{http_code}" http://localhost:5000/stats | grep "302"'';
                  in
                  {
                    liveness_probe.exec.command = probeCmd;
                    liveness_probe.period_seconds = 5;
                    readiness_probe.exec.command = probeCmd;
                    readiness_probe.period_seconds = 5;
                    readiness_probe.initial_delay_seconds = 3;
                  };
              };
            };
            migrate = {
              runCompose.enable = true;
            };
            scheduled_tasks = {
              addlPkgs = [ pkgs.wal-g ];
            };
          };
        };
    };
}
