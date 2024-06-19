using System.Xml;

namespace Celeste.Mod.Registry.DecalRegistryHandlers; 

internal sealed class SolidDecalRegistryHandler : DecalRegistryHandler {
    private int _x, _y, _width, _height, _index;
    private bool _blockWaterfalls, _safe;
    
    public override string Name => "solid";
    
    public override void Parse(XmlAttributeCollection xml) {
        _x = Get(xml, "x", 0);
        _y = Get(xml, "y", 0);
        _width = Get(xml, "width", 16);
        _height = Get(xml, "height", 16);

        _index = Get(xml, "index", SurfaceIndex.ResortRoof);
        _blockWaterfalls = GetBool(xml, "blockWaterfalls", true);
        _safe = GetBool(xml, "safe", true);
    }

    public override void ApplyTo(Decal decal) {
        int x = _x, y = _y, width = _width, height = _height;
        
        decal.ScaleRectangle(ref x, ref y, ref width, ref height);

        ((patch_Decal)decal).MakeSolid(x, y, width, height, _index, _blockWaterfalls, _safe);
    }
}