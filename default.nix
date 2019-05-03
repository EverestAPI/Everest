{ pkgs ? import <nixpkgs> {}, fetchNuGet ? pkgs.fetchNuGet, buildDotnetPackage ? pkgs.buildDotnetPackage }:

let
  Jdenticon = fetchNuGet {
    baseName = "Jdenticon-net";
    version = "2.2.1";
    sha256 = "1li1flpfpj8dwz7sy5imbvgwpqxzn7nqk2znr21rx2sn761029vz";
    outputFiles = ["*"];
  };

  Cecil = fetchNuGet {
    baseName = "Mono.Cecil";
    version = "0.10.0";
    sha256 = "0yg9c0papkdlvmhas5jh8d849hwmigam4whyljn6ig1npx6lmsik";
    outputFiles = [ "*" ];
  };

  Json = fetchNuGet {
    baseName = "Newtonsoft.Json";
    version  = "12.0.1";
    sha256 = "11f30cfxwn0z1hr5y69hxac0yyjz150ar69nvqhn18n9k92zfxz1";
    outputFiles = ["*"];
  };

in buildDotnetPackage rec {
  baseName = "Everest";
  version = pkgs.lib.commitIdFromGitRepo ./.git;
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
    ln -sn ${Jdenticon}/lib/dotnet/Jdenticon-net packages/Jdenticon-net.${Jdenticon.version}
    ln -sn ${Cecil}/lib/dotnet/Mono.Cecil packages/Mono.Cecil.${Cecil.version}
    ln -sn ${Json}/lib/dotnet/Newtonsoft.Json packages/Newtonsoft.Json.${Json.version}
  '';

  postInstall = ''
    mkdir -pv "$out/lib/dotnet/${baseName}"
    sed -i "2i cp -r $out/lib/dotnet/Everest/* "'$1' $out/bin/miniinstaller
    sed -i '2i cd $1' $out/bin/miniinstaller
  '';
} // { shell = import ./shell.nix; }
