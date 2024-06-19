using System.Xml;

namespace Celeste.Mod.Registry.DecalRegistryHandlers; 

internal sealed class DepthDecalRegistryHandler : DecalRegistryHandler {
    private int? _depth;
    
    public override string Name => "depth";
    
    public override void Parse(XmlAttributeCollection xml) {
        _depth = GetNullable<int>(xml, "value");
    }

    public override void ApplyTo(Decal decal) {
        if (_depth is { } depth && !((patch_Decal)decal).DepthSetByPlacement)
            decal.Depth = depth;
    }
}
