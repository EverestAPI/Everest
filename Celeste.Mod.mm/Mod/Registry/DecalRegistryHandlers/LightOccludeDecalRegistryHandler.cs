using Microsoft.Xna.Framework;
using System.Xml;

namespace Celeste.Mod.Registry.DecalRegistryHandlers; 

internal sealed class LightOccludeDecalRegistryHandler : DecalRegistryHandler {
    private int _x, _y, _width, _height;
    private float _alpha;
    
    public override string Name => "lightOcclude";
    
    public override void Parse(XmlAttributeCollection xml) {
        _x = Get(xml, "x", 0);
        _y = Get(xml, "y", 0);
        _width = Get(xml, "width", 16);
        _height = Get(xml, "height", 16);
        _alpha = Get(xml, "alpha", 1f);
    }

    public override void ApplyTo(Decal decal) {
        int x = _x, y = _y, width = _width, height = _height;
        
        decal.ScaleRectangle(ref x, ref y, ref width, ref height);

        decal.Add(new LightOcclude(new Rectangle(x, y, width, height), _alpha));
    }
}