using Microsoft.Xna.Framework;
using System.Xml;

namespace Celeste.Mod.Registry.DecalRegistryHandlers;

internal sealed class BloomDecalRegistryHandler : DecalRegistryHandler {
    private float _offX, _offY, _alpha, _radius;
    
    public override string Name => "bloom";
    
    public override void Parse(XmlAttributeCollection xml) {
        _offX = Get(xml, "offsetX", 0f);
        _offY = Get(xml, "offsetY", 0f);
        _alpha = Get(xml, "alpha", 1f);
        _radius = Get(xml, "radius", 1f);
    }

    public override void ApplyTo(Decal decal) {
        Vector2 offset = decal.GetScaledOffset(_offX, _offY);
        float radius = decal.GetScaledRadius(_radius);

        decal.Add(new BloomPoint(offset, _alpha, radius));
    }
}