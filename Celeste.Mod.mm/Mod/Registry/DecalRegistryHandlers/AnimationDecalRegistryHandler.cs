using System.Xml;

namespace Celeste.Mod.Registry.DecalRegistryHandlers; 

internal sealed class AnimationDecalRegistryHandler : DecalRegistryHandler {
    private int[] _frames;
    
    public override string Name => "animation";
    
    public override void Parse(XmlAttributeCollection xml) {
        _frames = GetCSVIntWithTricks(xml, "frames", "0");
    }

    public override void ApplyTo(Decal decal) {
        ((patch_Decal)decal).MakeAnimation(_frames);
    }
}