using System;
using System.IO;

namespace Celeste.Mod.HookGen;

public static class Generator {
    
    // Invoked dynamically from MiniInstaller
    public static void Generate(string vanillaAsm, string patchedAsm, string outputAsm) {
        Console.WriteLine(vanillaAsm);
        Console.WriteLine(patchedAsm);
        Console.WriteLine(outputAsm);
        
        File.Create(outputAsm).Close();
    }
}
