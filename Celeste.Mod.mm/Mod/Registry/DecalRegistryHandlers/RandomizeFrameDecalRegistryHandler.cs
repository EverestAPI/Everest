using System.Xml;

namespace Celeste.Mod.Registry.DecalRegistryHandlers; 

internal sealed class RandomizeFrameDecalRegistryHandler : DecalRegistryHandler {
    public override string Name => "randomizeFrame";
    
    public override void Parse(XmlAttributeCollection xml) {
    }

    public override void ApplyTo(Decal decal) {
        ((patch_Decal)decal).RandomizeStartingFrame();
    }
}