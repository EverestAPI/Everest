{ pkgs ? import <nixpkgs> {}, fetchNuGet ? pkgs.fetchNuGet, buildDotnetPackage ? pkgs.buildDotnetPackage }:

let
  Cecil = fetchNuGet {
    baseName = "Mono.Cecil";
    version = "0.10.0";
    sha256 = "0yg9c0papkdlvmhas5jh8d849hwmigam4whyljn6ig1npx6lmsik";
    outputFiles = [ "*" ];
  };

  ValueTuple = fetchNuGet {
    baseName = "System.ValueTuple";
    version = "4.4.0";
    sha256 = "1wydfgszs00yxga57sam66vzv9fshk2pw7gim57saplsnkfliaif";
    outputFiles = ["*"];
  };

in buildDotnetPackage rec {
  baseName = "Everest";
  version = "0.0.0";
  name = "${baseName}-dev-${version}";

  src = ./.;

  xBuildFiles = [ "Celeste.Mod.mm/Celeste.Mod.mm.csproj" "MiniInstaller/MiniInstaller.csproj" ];
  outputFiles = [ "Celeste.Mod.mm/bin/Release/*" "MiniInstaller/bin/Release/*" ];

  patchPhase = ''
    # $(SolutionDir) does not work for some reason
    substituteInPlace Celeste.Mod.mm/Celeste.Mod.mm.csproj --replace '$(SolutionDir)' ".."
    substituteInPlace MiniInstaller/MiniInstaller.csproj --replace '$(SolutionDir)' ".."
  '';

  preBuild = ''
    # Fake nuget restore, not very elegant but it works.
    mkdir -p packages
    ln -sn ${Cecil}/lib/dotnet/Mono.Cecil packages/Mono.Cecil.${Cecil.version}
    ln -sn ${ValueTuple}/lib/dotnet/System.ValueTuple packages/System.ValueTuple.${ValueTuple.version}
  '';

  postInstall = ''
    mkdir -pv "$out/lib/dotnet/${baseName}"
    sed -i "2i cp -r $out/lib/dotnet/Everest/* "'$1' $out/bin/miniinstaller
    sed -i '2i cd $1' $out/bin/miniinstaller
  '';
} // { shell = import ./shell.nix; }
