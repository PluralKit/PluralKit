{ ... }:
{
  perSystem = { pkgs, ... }: {
    _module.args.pkLib.mkBotEnv =
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
            nixd
          ];
        runScript = cmd;
      };
  };
}
