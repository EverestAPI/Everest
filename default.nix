{ fetchNuGet, buildDotnetPackage, dotnetPackages }:

let
  HookedMethod = fetchNuGet {
    baseName = "HookedMethod";
    version = "0.3.3-beta";
    sha256 = "5abc349e55d57777fed6fc5d65cda4cc97aa8cb1dc87927dea7d4182f3fa57df";
    outputFiles = [ "lib/*" ];
  };

  Cecil = fetchNuGet {
    baseName = "Mono.Cecil";
    version = "0.10.0";
    sha256 = "f4c64a1dd69df48fe50952a9ece8c1430e54650703be432c41c93b52802cb864";
    outputFiles = [ "lib/*" ];
  };

  ValueTuple = fetchNuGet {
    baseName = "System.ValueTuple";
    version = "4.4.0";
    sha256 = "68c3ad8ff7deb843c13710fd115c8e8d64492f639fa434059e1440a311a04424";
    outputFiles = ["lib/*"];
  };

in buildDotnetPackage rec {
  baseName = "Everest";
  version = "0.0.0";
  name = "${baseName}-dev-${version}";

  src = ./.;
  buildInputs = [ HookedMethod Cecil ValueTuple ];

  # Re-add Miniinstaler once everything else works
  xBuildFiles = [ "Celeste.Mod.mm/Celeste.Mod.mm.csproj" ]; # "MiniInstaller/MiniInstaller.csproj" ];
  outputFiles = [ "Celeste.Mod.mm/bin/Debug/*" /* vim syntax bug workaround */ ]; # "MiniInstaller/bin/Debug/*" /* vim syntax bug workaround */ ];

  # could probably loop over buildInputs
  preBuild = ''
    # Fake nuget restore
    mkdir -p  Celeste.Mod.mm/packages/HookedMethod.${HookedMethod.version}
    ln -sn ${HookedMethod}/lib/ Celeste.Mod.mm/packages/HookedMethod.${HookedMethod.version}/lib
    mkdir -p  Celeste.Mod.mm/packages/Mono.Cecil.${Cecil.version}
    ln -sn ${Cecil}/lib/ Celeste.Mod.mm/packages/Mono.Cecil.${Cecil.version}/lib
    mkdir -p  Celeste.Mod.mm/packages/ValueTuple.${ValueTuple.version}
    ln -sn ${ValueTuple}/lib/ Celeste.Mod.mm/packages/ValueTuple.${ValueTuple.version}/lib
  '';

  postInstall = ''
    mkdir -pv "$out/lib/dotnet/${baseName}"
    sed -i '2i cd $1' $out/bin/miniinstaller
  '';
}
