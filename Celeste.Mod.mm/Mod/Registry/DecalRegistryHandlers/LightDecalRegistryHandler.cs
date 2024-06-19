using Microsoft.Xna.Framework;
using Monocle;
using System.Xml;

namespace Celeste.Mod.Registry.DecalRegistryHandlers; 

internal sealed class LightDecalRegistryHandler : DecalRegistryHandler {
    private float _offX, _offY, _alpha;
    private int _startFade, _endFade;
    private Color _color;
    
    public override string Name => "light";
    
    public override void Parse(XmlAttributeCollection xml) {
        _offX = Get(xml, "offsetX", 0f);
        _offY = Get(xml, "offsetY", 0f);
        _color = GetHexColor(xml, "color", Color.White);
        _alpha = Get(xml, "alpha", 1f);
        _startFade = Get(xml, "startFade", 16);
        _endFade = Get(xml, "endFade", 24);
    }

    public override void ApplyTo(Decal decal) {
        Vector2 offset = decal.GetScaledOffset(_offX, _offY);
        int startFade = (int) decal.GetScaledRadius(_startFade);
        int endFade = (int) decal.GetScaledRadius(_endFade);

        decal.Add(new VertexLight(offset, _color, _alpha, startFade, endFade));
    }
}