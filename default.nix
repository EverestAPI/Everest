let nixpkgs = import <nixpkgs> {};
    stdenv = nixpkgs.stdenv;
    nuget = nixpkgs.dotnetPackages.Nuget;

in rec {
  everest = nixpkgs.buildDotnetPackage rec {
    baseName = "Everest";
    version = "0.0.0";
    name = "${baseName}-dev-${version}";
    
    src = ./.;

    xBuildFiles = [ "Celeste.Mod.mm/Celeste.Mod.mm.csproj" "MiniInstaller/MiniInstaller.csproj" ];

    outputFiles = [ "Celeste.Mod.mm/bin/Debug/*" /* vim syntax bug workaround */ "MiniInstaller/bin/Debug/*" /* vim syntax bug workaround */ ];
    
    postInstall = ''
        mkdir -pv "$out/lib/dotnet/${baseName}"
        sed -i '2i cd $1' $out/bin/miniinstaller
    '';
  };
}
