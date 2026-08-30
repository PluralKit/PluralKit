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

      commonTools = with pkgs; [
        nixd
        postgresql
      ];
      withCommon =
        shell:
        shell.overrideAttrs (old: {
          nativeBuildInputs = (old.nativeBuildInputs or [ ]) ++ commonTools;
          env.PGHOST = "127.0.0.1";
          env.PGUSER = "postgres";
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