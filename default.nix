{ fetchNuGet, buildDotnetPackage }:

let
  HookedMethod = fetchNuGet {
    baseName = "HookedMethod";
    version = "0.3.3-beta";
    sha256 = "5abc349e55d57777fed6fc5d65cda4cc97aa8cb1dc87927dea7d4182f3fa57df";
    outputFiles = [ "*" ];
  };

  Cecil = fetchNuGet {
    baseName = "Mono.Cecil";
    version = "0.10.0";
    sha256 = "f4c64a1dd69df48fe50952a9ece8c1430e54650703be432c41c93b52802cb864";
    outputFiles = [ "*" ];
  };

  ValueTuple = fetchNuGet {
    baseName = "System.ValueTuple";
    version = "4.4.0";
    sha256 = "68c3ad8ff7deb843c13710fd115c8e8d64492f639fa434059e1440a311a04424";
    outputFiles = ["*"];
  };

in buildDotnetPackage rec {
  baseName = "Everest";
  version = "0.0.0";
  name = "${baseName}-dev-${version}";

  src = ./.;

  xBuildFiles = [ "Celeste.Mod.mm/Celeste.Mod.mm.csproj" "MiniInstaller/MiniInstaller.csproj" ];
  outputFiles = [ "Celeste.Mod.mm/bin/Release/*" /* vim syntax bug workaround */ "MiniInstaller/bin/Release/*" /* vim syntax bug workaround */ ];

  patchPhase = ''
    # $(SolutionDir) does not work for some reason
    substituteInPlace Celeste.Mod.mm/Celeste.Mod.mm.csproj --replace '$(SolutionDir)' ".."
    substituteInPlace MiniInstaller/MiniInstaller.csproj --replace '$(SolutionDir)' ".."
  '';

  preBuild = ''
    # Fake nuget restore, not very elegant but it works.
    mkdir -p  packages
    ln -sn ${HookedMethod}/lib/dotnet/HookedMethod packages/HookedMethod.${HookedMethod.version}
    ln -sn ${Cecil}/lib/dotnet/Mono.Cecil packages/Mono.Cecil.${Cecil.version}
    ln -sn ${ValueTuple}/lib/dotnet/System.ValueTuple packages/System.ValueTuple.${ValueTuple.version}
  '';

  postInstall = ''
    mkdir -pv "$out/lib/dotnet/${baseName}"
    sed -i "2i cp -r $out/lib/dotnet/Everest/* "'$1' $out/bin/miniinstaller
    sed -i '2i cd $1' $out/bin/miniinstaller
  '';
}
