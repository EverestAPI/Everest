using Mono.Cecil;
using Mono.Cecil.Cil;
using System.IO;

namespace Celeste.Mod.HookGen;

public static class Generator {
    
    // Invoked dynamically from MiniInstaller
    public static void Run(string vanillaAsm, string moddedAsm, string outputAsm) {
        if (File.Exists(outputAsm)) {
            File.Delete(outputAsm);
        }

        using var vanillaModule = ReadModule(vanillaAsm, out _);
        using var moddedModule = ReadModule(moddedAsm, out bool symbols);

        using var outputModule = ModuleDefinition.CreateModule(Path.GetFileName(outputAsm), new ModuleParameters {
            Architecture = moddedModule.Architecture,
            AssemblyResolver = moddedModule.AssemblyResolver,
            Kind = ModuleKind.Dll,
            Runtime = moddedModule.Runtime
        });

        using var modder = new HookGeneratorModder(moddedModule, outputModule);
        modder.Generate(vanillaModule);
        
        outputModule.Write(outputAsm, new WriterParameters { WriteSymbols = symbols });
    }

    private static ModuleDefinition ReadModule(string inputAsm, out bool readSymbols) {
        ReaderParameters readerParams = new(ReadingMode.Immediate)  { ReadSymbols = true };
        readSymbols = true;

        try {
            return ModuleDefinition.ReadModule(inputAsm, readerParams);
        } catch (SymbolsNotFoundException) {
            readerParams.ReadSymbols = false;
            readSymbols = false;
            return ModuleDefinition.ReadModule(inputAsm, readerParams);
        } catch (SymbolsNotMatchingException) {
            readerParams.ReadSymbols = false;
            readSymbols = false;
            return ModuleDefinition.ReadModule(inputAsm, readerParams);
        }
    }
}
