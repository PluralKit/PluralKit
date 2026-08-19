{ ... }:
{
  perSystem =
    {
      config,
      pkgs,
      pkLib,
      ...
    }:
    let
      rustOutputs = config.nci.outputs;
      rustDevshell = rustOutputs."pluralkit-services".devShell;

      commonTools = with pkgs; [ nixd ];
      withCommon =
        shell:
        shell.overrideAttrs (old: {
          nativeBuildInputs = (old.nativeBuildInputs or [ ]) ++ commonTools;
        });

      docs = pkgs.mkShellNoCC {
        buildInputs = with pkgs; [
          nodejs
          yarn
        ];
        NODE_OPTIONS = "--openssl-legacy-provider";
      };
    in
    {
      devShells = {
        services = withCommon rustDevshell;
        bot = (pkLib.mkBotEnv "bash").env;
        docs = withCommon docs;
        default = withCommon rustDevshell;
      };
    };
}
