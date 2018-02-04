# Everest - Celeste Mod Loader / Base API

### License: MIT

----

### WIP.

Using [MonoMod](https://github.com/0x0ade/MonoMod), an open-source C# modding utility.

### Windows users: Switch to the opengl / FNA branch.
- In Steam, it's listed as under "betas"
- In itch.io, **TODO**

### Everest Installation:
- Copy files from inside `lib` into Celeste dir.
- Copy built `Celeste.Mod.mm.dll` into Celeste dir.
- Copy `mod.bat` / `mod.sh` into Celeste dir.
- Run `mod.bat` / `mod.sh`

### Mod Installation:
- If it's missing, create a `Mods` directory where Celeste is.
- Put the mod `.zip` into the `Mods` directory.
    - For prototyping: Create a subdirectory, pretend it's a `.zip`
- That's it.

### Mod Development:
- Follow the installation instructions.
- Use RainbowMod as an example mod. It already contains:
    - The required references (`lib/`, `lib-stripped/`) with "Copy Local" set to "False"
    - The mod `metadata.yaml`
