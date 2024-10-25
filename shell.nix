{ pkgs ? import <nixpkgs> {} }:

pkgs.mkShellNoCC {
  packages = with pkgs; [
    cargo rust-analyzer rustfmt
    gcc
    protobuf
    dotnet-sdk_6
    go
    nodejs yarn
  ];

  NODE_OPTIONS = "--openssl-legacy-provider";
}
