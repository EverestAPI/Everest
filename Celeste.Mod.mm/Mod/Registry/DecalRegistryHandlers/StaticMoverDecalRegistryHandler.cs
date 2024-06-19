using System.Xml;

namespace Celeste.Mod.Registry.DecalRegistryHandlers; 

internal sealed class StaticMoverDecalRegistryHandler : DecalRegistryHandler {
    private int _x, _y, _width, _height;
    
    public override string Name => "staticMover";
    
    public override void Parse(XmlAttributeCollection xml) {
        _x = Get(xml, "x", 0);
        _y = Get(xml, "y", 0);
        _width = Get(xml, "width", 16);
        _height = Get(xml, "height", 16);
    }

    public override void ApplyTo(Decal decal) {
        int x = _x, y = _y, width = _width, height = _height;
        
        decal.ScaleRectangle(ref x, ref y, ref width, ref height);

        ((patch_Decal)decal).MakeStaticMover(x, y, width, height);
    }
}