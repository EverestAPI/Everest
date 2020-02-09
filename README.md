# Everest - Celeste Mod Loader / Base API

### License: MIT

----

[![Build Status](https://dev.azure.com/EverestAPI/Everest/_apis/build/status/EverestAPI.Everest?branchName=master)](https://dev.azure.com/EverestAPI/Everest/_build/latest?definitionId=1?branchName=master)

[**Check the website for installation / usage instructions.**](https://everestapi.github.io/)

Using [MonoMod](https://github.com/0x0ade/MonoMod), an open-source C# modding utility.

**We're in the game modding category on the "Mt. Celeste Climbing Association" Discord server:**

[![Discord invite](github/invite.png)](https://discord.gg/6qjaePQ)

### Mod Development:
Use [Fabeline](https://github.com/EverestAPI/RainbowMod) as an example mod. It already contains:
- Everest as a submodule
- The required references (`lib/`, `lib-stripped/`) with "Copy Local" set to "False"
- The mod `metadata.yaml`

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

### macOS / Linux (no Nix)
- [Install the mono runtime](https://www.mono-project.com/download/stable/)
- Build all
    - _With MonoDevelop:_ Open the .sln in the repo with MonoDevelop
    - _Manually:_ Open the terminal in the Everest directory and run `msbuild` or `xbuild`
- Copy everything in `MiniInstaller/bin/Debug` and `Celeste.Mod.mm/bin/Debug` to your Celeste directory
    - macOS: `Celeste.app/Contents/MacOS`
- Run `mono MiniInstaller.exe`

### Nix
**Note:** At the time of writing, no member of the Everest Team is using Nix.
- Run `nix-env -f . -iA everest` in the Everest repo
- Wait for it to install
- Run `miniinstaller ~/Celeste`, where `~/Celeste` is your Celeste path.
    - **`miniinstaller` is a *wrapper* over the MiniInstaller.exe in the other methods.**
