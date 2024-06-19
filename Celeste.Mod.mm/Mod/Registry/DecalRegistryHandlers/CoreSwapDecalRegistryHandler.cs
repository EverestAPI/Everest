using System.Xml;

namespace Celeste.Mod.Registry.DecalRegistryHandlers; 

internal sealed class CoreSwapDecalRegistryHandler : DecalRegistryHandler {
    private string _hotPath, _coldPath;
    
    public override string Name => "coreSwap";
    
    public override void Parse(XmlAttributeCollection xml) {
        _hotPath = GetString(xml, "hotPath", null);
        _coldPath = GetString(xml, "coldPath", null);
    }

    public override void ApplyTo(Decal decal) {
        ((patch_Decal)decal).MakeFlagSwap("cold", _hotPath, _coldPath);
    }
}