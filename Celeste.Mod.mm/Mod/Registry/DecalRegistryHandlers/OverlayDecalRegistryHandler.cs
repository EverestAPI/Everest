using System.Xml;

namespace Celeste.Mod.Registry.DecalRegistryHandlers; 

internal sealed class OverlayDecalRegistryHandler : DecalRegistryHandler {
    public override string Name => "overlay";
    public override void Parse(XmlAttributeCollection xml) {
    }

    public override void ApplyTo(Decal decal) {
        ((patch_Decal)decal).MakeOverlay();
    }
}