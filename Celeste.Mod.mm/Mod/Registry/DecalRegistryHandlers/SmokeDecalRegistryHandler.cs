using Microsoft.Xna.Framework;
using System.Xml;

namespace Celeste.Mod.Registry.DecalRegistryHandlers; 

internal sealed class SmokeDecalRegistryHandler : DecalRegistryHandler {
    private float _offX, _offY;
    private bool _inbg;
    
    public override string Name => "smoke";
    
    public override void Parse(XmlAttributeCollection xml) {
        _offX = Get(xml, "offsetX", 0f);
        _offY = Get(xml, "offsetY", 0f);
        _inbg = GetBool(xml, "inbg", false);
    }

    public override void ApplyTo(Decal decal) {
        Vector2 offset = decal.GetScaledOffset(_offX, _offY);

        ((patch_Decal)decal).CreateSmoke(offset, _inbg);
    }
}