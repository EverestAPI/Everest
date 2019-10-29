{ pkgs ? import <nixpkgs> {}, fetchNuGet ? pkgs.fetchNuGet, buildDotnetPackage ? pkgs.buildDotnetPackage }:

let
  DotNetZip = fetchNuGet {
    baseName = "DotNetZip";
    version = "1.13.4";
    sha256 = "0j7b8b12dz7wxmhr1i9xs9mr9hq65xnsxdg4vlaz53k7ddi36v39";
    outputFiles = ["*"];
  };

  Jdenticon = fetchNuGet {
    baseName = "Jdenticon-net";
    version = "2.2.1";
    sha256 = "1li1flpfpj8dwz7sy5imbvgwpqxzn7nqk2znr21rx2sn761029vz";
    outputFiles = ["*"];
  };

  KeraLua = fetchNuGet {
    baseName = "KeraLua";
    version = "1.0.22";
    sha256 = "09b4kp6rnzkxdz69bk2w964l3vkypga6p1lnp5g7vzcnq24zhn6y";
    outputFiles = ["*"];
  };

  Cecil = fetchNuGet {
    baseName = "Mono.Cecil";
    version = "0.10.4";
    sha256 = "16iabjrizkh3g4g9dj40bm2z1kba7752pp5qfszy06x82ahs8l9l";
    outputFiles = ["*"];
  };

  MonoMod = fetchNuGet {
    baseName = "MonoMod";
    version = "19.9.1.6";
    sha256 = "1z5rz44m62i5f6n87z71fsgdy00xc445s29fca80cjvb8qwmwwz4";
    outputFiles = ["*"];
  };

  MonoMod-RD = fetchNuGet {
    baseName = "MonoMod.RuntimeDetour";
    version = "19.9.1.6";
    sha256 = "07ggcssl9xyf5g6b80xsd0y4nlzap2cnm9fhv7bywiynvlkh2rd8";
    outputFiles = ["*"];
  };

  MonoMod-RD-HG = fetchNuGet {
    baseName = "MonoMod.RuntimeDetour.HookGen";
    version = "19.9.1.6";
    sha256 = "04vf06ascqph6yl0c6i0iqzw3sqhn1m1hwhgn0jm02ps0wjgvvqa";
    outputFiles = ["*"];
  };

  MonoMod-Utils = fetchNuGet {
    baseName = "MonoMod.Utils";
    version = "19.9.1.6";
    sha256 = "174pfw9d8kwk64rdy75aw6acag619fvd5vin5iwzbrhxniv3pb69";
    outputFiles = ["*"];
  };

  Json = fetchNuGet {
    baseName = "Newtonsoft.Json";
    version = "12.0.2";
    sha256 = "0w2fbji1smd2y7x25qqibf1qrznmv4s6s0jvrbvr6alb7mfyqvh5";
    outputFiles = ["*"];
  };

  NLua = fetchNuGet {
    baseName = "NLua";
    version = "1.4.24";
    sha256 = "0gcn2gfbrf8ib4dw1j0dy0pn256x3171gvws225gg9lkm96n3dqn";
    outputFiles = ["*"];
  };

  YamlDotNet = fetchNuGet {
    baseName = "YamlDotNet";
    version = "7.0.0";
    sha256 = "1vckldz58qn2pmnc9kfvvfqayyxiy8yzyini8s7fl2c7fm3nrjyg";
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

    # See c4263f8 Celeste.Mod.mm/Mod/Everest/Everest.cs line 31
    # This is normally set by Azure
    substituteInPlace Celeste.Mod.mm/Mod/Everest/Everest.cs --replace '0.0.0-dev' "0.0.0-nix-${builtins.substring 0 7 version}"
  '';

  preBuild = ''
    # Fake nuget restore, not very elegant but it works.
    mkdir -p packages
    ln -sn ${Jdenticon}/lib/dotnet/Jdenticon-net                     packages/Jdenticon-net.${Jdenticon.version}
    ln -sn ${KeraLua}/lib/dotnet/KeraLua                             packages/KeraLua.${KeraLua.version}
    ln -sn ${DotNetZip}/lib/dotnet/DotNetZip                         packages/DotNetZip.${DotNetZip.version}
    ln -sn ${Cecil}/lib/dotnet/Mono.Cecil                            packages/Mono.Cecil.${Cecil.version}
    ln -sn ${MonoMod}/lib/dotnet/MonoMod                             packages/MonoMod.${MonoMod.version}
    ln -sn ${MonoMod-RD}/lib/dotnet/MonoMod.RuntimeDetour            packages/MonoMod.RuntimeDetour.${MonoMod-RD.version}
    ln -sn ${MonoMod-RD-HG}/lib/dotnet/MonoMod.RuntimeDetour.HookGen packages/MonoMod.RuntimeDetour.HookGen.${MonoMod-RD-HG.version}
    ln -sn ${MonoMod-Utils}/lib/dotnet/MonoMod.Utils                 packages/MonoMod.Utils.${MonoMod-Utils.version}
    ln -sn ${Json}/lib/dotnet/Newtonsoft.Json                        packages/Newtonsoft.Json.${Json.version}
    ln -sn ${NLua}/lib/dotnet/NLua                                   packages/NLua.${NLua.version}
    ln -sn ${YamlDotNet}/lib/dotnet/YamlDotNet                       packages/YamlDotNet.${YamlDotNet.version}
  '';

  postInstall = ''
    mv \
      $out/lib/dotnet/Everest/libMonoPosixHelper.dylib.dSYM/Contents/Resources/DWARF/libMonoPosixHelper.dylib \
      $out/lib/dotnet/Everest/libMonoPosixHelper.dylib.dSYM/Contents/Info.plist \
      $out/lib/dotnet/Everest/lib64/* \
      $out/lib/dotnet/Everest/
    rm -r $out/lib/dotnet/Everest/lib64 $out/lib/dotnet/Everest/libMonoPosixHelper.dylib.dSYM
    sed -i "2i chmod -R u+w ." $out/bin/miniinstaller
    sed -i "2i cp -r $out/lib/dotnet/Everest/* "'$1' $out/bin/miniinstaller
    sed -i '2i cd $1' $out/bin/miniinstaller
  '';
} // { shell = import ./shell.nix; }
