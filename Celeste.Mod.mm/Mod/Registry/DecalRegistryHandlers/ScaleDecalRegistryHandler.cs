using Microsoft.Xna.Framework;
using System.Xml;

namespace Celeste.Mod.Registry.DecalRegistryHandlers; 

internal sealed class ScaleDecalRegistryHandler : DecalRegistryHandler {
    private Vector2 _scale;
    
    public override string Name => "scale";
    
    public override void Parse(XmlAttributeCollection xml) {
        _scale = GetVector2(xml, "multiplyX", "multiplyY", Vector2.One);
    }

    public override void ApplyTo(Decal decal) {
        ((patch_Decal)decal).Scale *= _scale;
    }
}
