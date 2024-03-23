using System.Xml;

namespace Celeste.Mod.Registry.DecalRegistryHandlers; 

internal sealed class FloatyDecalRegistryHandler : DecalRegistryHandler {
    public override string Name => "floaty";
    
    public override void Parse(XmlAttributeCollection xml) {
    }

    public override void ApplyTo(Decal decal) {
        ((patch_Decal)decal).MakeFloaty();
    }
}