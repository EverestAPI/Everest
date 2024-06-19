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
        _amplitude *= ((patch_Decal)decal).Scale.X;
        _offset *= float.Sign(((patch_Decal)decal).Scale.X) * float.Abs(((patch_Decal)decal).Scale.Y);

        ((patch_Decal)decal).MakeBanner(_speed, _amplitude, _sliceSize, _sliceSinIncrement, _easeDown, _offset, _onlyIfWindy);
    }
}