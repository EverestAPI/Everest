using System.Xml;

namespace Celeste.Mod.Registry.DecalRegistryHandlers; 

internal sealed class FlagSwapDecalRegistryHandler : DecalRegistryHandler {
    private string _flag, _offPath, _onPath;
    
    public override string Name => "flagSwap";
    
    public override void Parse(XmlAttributeCollection xml) {
        _flag = GetString(xml, "flag", null);
        _offPath = GetString(xml, "offPath", null);
        _onPath = GetString(xml, "onPath", null);
    }

    public override void ApplyTo(Decal decal) {
        if (_flag is { } flag) {
            ((patch_Decal)decal).MakeFlagSwap(flag, _offPath, _onPath);
        }
    }
}