# Everest - Celeste Mod Loader / Base API

### License: MIT

----

[![Build Status](https://dev.azure.com/EverestAPI/Everest/_apis/build/status/EverestAPI.Everest?branchName=dev)](https://dev.azure.com/EverestAPI/Everest/_build?definitionId=3)

[**Check the website for installation / usage instructions.**](https://everestapi.github.io/)

Using [MonoMod](https://github.com/MonoMod/MonoMod), an open-source C# modding utility.

**We're in the game modding category on the "Mt. Celeste Climbing Association" Discord server:**

[![Discord invite](github/invite.png)](https://discord.gg/6qjaePQ)

### Mod Development:
For information about mod development, check out the [Everest Wiki](https://github.com/EverestAPI/Resources/wiki) or ask questions in the `#modding_help` channel on discord.

## Compiling Everest yourself
- ***If you just want to install Everest, go to the [Everest website](https://everestapi.github.io/).***
- If you've **previously installed Everest and updated Celeste** or switched betas / branches, delete the `orig` directory where Celeste is installed.
    - macOS: Right-click and browse the Celeste app in Finder, then navigate to `Contents`, then `MacOS`.
- Clone the Everest repo **+ submodules**, either in your IDE or via the CLI.
- Restore Nuget packages either via your IDE or the command line.
- Everest requires version 7.0.200 or higher of .NET SDK, as well as the .NET 6.0 runtime, for the build process.

### Windows
- Open the .sln in the repo with Visual Studio
- Publish all projects
    - **NOTE:** It is very important that you *publish* the project instead of simply building it, as otherwise required dependency DLLs won't be copied!
- Copy everything in `MiniInstaller\bin\Release\net7.0\publish` and `Celeste.Mod.mm\bin\Release\net7.0\publish` to your Celeste directory, replacing existing files
- Run MiniInstaller-win64.exe on 64-bit or MiniInstaller-win.exe on 32-bit

### macOS / Linux
- [Install the mono runtime](https://www.mono-project.com/download/stable/)
- Publish all projects
    - _With MonoDevelop:_ Open the .sln in the repo with MonoDevelop
    - _Manually:_ Open the terminal in the Everest directory and run `msbuild` or `dotnet publish`
    - **NOTE:** It is very important that you *publish* the project instead of simply building it, as otherwise required dependency DLLs won't be copied!
- Copy everything in `MiniInstaller/bin/Release/net7.0/publish` and `Celeste.Mod.mm/bin/Release/net7.0/publish` to your Celeste directory
    - macOS: `Celeste.app/Contents/Resources`
- Run `./MiniInstaller-linux` or `./MiniInstaller-osx`

## Contributing
Contributions of any kind are welcome, and a guide on how to contribute effectively to the Everest project is available [here](./CONTRIBUTING.md).
Make sure to join the discussion in the [Celeste Discord](https://discord.gg/6qjaePQ), and feel free to ask any questions you have there as well.

### Other Ways To Contribute
This project is created, improved, and maintained entirely by volunteers.
If you would like to show your support for this project, consider donating to one or more of its contributors.
