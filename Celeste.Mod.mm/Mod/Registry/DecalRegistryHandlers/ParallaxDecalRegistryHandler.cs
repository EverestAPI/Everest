using System.Globalization;
using System.Xml;

namespace Celeste.Mod.Registry.DecalRegistryHandlers; 

internal sealed class ParallaxDecalRegistryHandler : DecalRegistryHandler {
    private float? _amount;
    
    public override string Name => "parallax";
    
    public override void Parse(XmlAttributeCollection xml) {
        _amount = GetNullable<float>(xml, "amount");
    }

    public override void ApplyTo(Decal decal) {
        if (_amount is { } amount)
            ((patch_Decal)decal).MakeParallax(amount);
    }
}