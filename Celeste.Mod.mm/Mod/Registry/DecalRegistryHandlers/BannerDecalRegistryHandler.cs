using Microsoft.Xna.Framework;
using System.Xml;

namespace Celeste.Mod.Registry.DecalRegistryHandlers; 

internal sealed class BannerDecalRegistryHandler : DecalRegistryHandler {
    private float _speed, _amplitude, _sliceSinIncrement, _offset;
    private int _sliceSize;
    private bool _easeDown, _onlyIfWindy;
    
    public override string Name => "banner";
    
    public override void Parse(XmlAttributeCollection xml) {
        _speed = Get(xml, "speed", 1f);
        _amplitude = Get(xml, "amplitude", 1f);
        _sliceSize = Get(xml, "sliceSize", 1);
        _sliceSinIncrement = Get(xml, "sliceSinIncrement", 1f);
        _easeDown = GetBool(xml, "easeDown", false);
        _offset = Get(xml, "offset", 0f);
        _onlyIfWindy = GetBool(xml, "onlyIfWindy", false);
    }

    public override void ApplyTo(Decal decal) {
        Vector2 scale = ((patch_Decal) decal).Scale;
        float amplitude = _amplitude * scale.X;
        float offset = _offset * float.Sign(scale.X) * float.Abs(scale.Y);

        ((patch_Decal)decal).MakeBanner(_speed, amplitude, _sliceSize, _sliceSinIncrement, _easeDown, offset, _onlyIfWindy);
    }
}
