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
    - macOS: Right-click and browse the Celeste app in Finder, then naviagte to `Contents`, then `MacOS`.
- Clone the Everest repo, either in your IDE, via the CLI, or by downloading the .zip from GitHub.
- Restore Nuget packages either via your IDE or the command line.

### Windows
- Open the .sln in the repo with Visual Studio
- Build all
- Copy everything in `MiniInstaller\bin\Debug` and `Celeste.Mod.mm\bin\Debug` to your Celeste directory
- Run MiniInstaller.exe

### macOS / Linux
- [Install the mono runtime](https://www.mono-project.com/download/stable/)
- Build all
    - _With MonoDevelop:_ Open the .sln in the repo with MonoDevelop
    - _Manually:_ Open the terminal in the Everest directory and run `msbuild` or `dotnet build`
- Copy everything in `MiniInstaller/bin/Debug` and `Celeste.Mod.mm/bin/Debug` to your Celeste directory
    - macOS: `Celeste.app/Contents/MacOS`
- Run `mono MiniInstaller.exe`
