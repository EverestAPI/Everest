using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection.Metadata;
using System.Reflection.PortableExecutable;
using System.Xml;

namespace MiniInstaller;

public static class MiscUtil {
    
    public static Version GetPEAssemblyVersion(string path) {
        using (FileStream fs = File.OpenRead(path))
        using (PEReader pe = new PEReader(fs))
            return pe.GetMetadataReader().GetAssemblyDefinition().Version;
    }

    public static Dictionary<string, Version> GetPEAssemblyReferences(string path) {
        using (FileStream fs = File.OpenRead(path))
        using (PEReader pe = new PEReader(fs)) {
            MetadataReader meta = pe.GetMetadataReader();

            Dictionary<string, Version> deps = new Dictionary<string, Version>();
            foreach (AssemblyReference asmRef in meta.AssemblyReferences.Select(meta.GetAssemblyReference))
                deps.TryAdd(meta.GetString(asmRef.Name), asmRef.Version);

            return deps;
        }
    }
    
    public static void ParseMonoNativeLibConfig(string configFile, string os, Dictionary<string, string> dllMap, string dllNameScheme) {
        if (!File.Exists(configFile))
            return;

        Logger.LogLine($"Parsing Mono config file {configFile}");

        //Read the config file
        XmlDocument configDoc = new XmlDocument();
        configDoc.Load(configFile);
        foreach (XmlNode node in configDoc.DocumentElement) {
            if (node is not XmlElement dllmapElement || node.Name != "dllmap")
                continue;

            // Check the dllmap entry OS
            if (!dllmapElement.GetAttribute("os").Split(',').Contains(os))
                continue;
    
            // Add an entry to the dllmap
            dllMap[dllmapElement.GetAttribute("target")] = string.Format(dllNameScheme, dllmapElement.GetAttribute("dll"));
        }
    }
    
    public static bool IsSystemLibrary(string file) {
        if (Path.GetExtension(file) != ".dll")
            return false;

        if (Path.GetFileName(file).StartsWith("System.") && !Globals.EverestSystemLibs.Contains(Path.GetFileName(file)))
            return true;

        return new string[] {
            "mscorlib.dll",
            "Mono.Posix.dll",
            "Mono.Security.dll"
        }.Any(name => Path.GetFileName(file).Equals(name, StringComparison.OrdinalIgnoreCase));
    }
    
    // This is not "pure" but I guess it also somewhat fits here
    public static void MoveExecutable(string srcPath, string dstPath) {
        File.Delete(dstPath);
        File.Move(srcPath, dstPath);

        if (Path.GetFullPath(Path.ChangeExtension(srcPath, null)) != Path.GetFullPath(Path.ChangeExtension(dstPath, null))) {
            if (File.Exists(Path.ChangeExtension(srcPath, ".pdb"))) {
                File.Delete(Path.ChangeExtension(dstPath, ".pdb"));
                File.Move(Path.ChangeExtension(srcPath, ".pdb"), Path.ChangeExtension(dstPath, ".pdb"));
            }

            if (File.Exists(Path.ChangeExtension(srcPath, ".mdb"))) {
                File.Delete(Path.ChangeExtension(dstPath, ".mdb"));
                File.Move(Path.ChangeExtension(srcPath, ".mdb"), Path.ChangeExtension(dstPath, ".mdb"));
            }
        }
    }
}