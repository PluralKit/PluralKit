{ ... }:
{
  perSystem = { pkgs, ... }: {
    _module.args.pkLib.mkBotShell = pkgs.mkShell {
      name = "pluralkit-dotnet";
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
  };
}
