with import <nixpkgs> {};

let
  Everest = callPackage ./default.nix {};

in stdenv.mkDerivation {
  name = "everestEnv";
  buildInputs = [ Everest ];
}
