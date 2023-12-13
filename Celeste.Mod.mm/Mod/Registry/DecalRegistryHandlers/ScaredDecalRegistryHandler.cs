using Monocle;
using System.Xml;

namespace Celeste.Mod.Registry.DecalRegistryHandlers; 

internal sealed class ScaredDecalRegistryHandler : DecalRegistryHandler {
    private int[] _idleFrames, _hiddenFrames, _hideFrames, _showFrames;
    private int _hideRange, _showRange;
    
    public override string Name => "scared";
    
    public override void Parse(XmlAttributeCollection xml) {
        _hideRange = Get(xml, "range", 32);
        _showRange = Get(xml, "range", 48);
        
        _hideRange = Get(xml, "hideRange", _hideRange);
        _showRange = Get(xml, "showRange", _showRange);
        
        _idleFrames = GetCSVIntWithTricks(xml, "idleFrames", "0");
        _hiddenFrames = GetCSVIntWithTricks(xml, "hiddenFrames", "0");
        _hideFrames = GetCSVIntWithTricks(xml, "hideFrames", "0");
        _showFrames = GetCSVIntWithTricks(xml, "showFrames", "0");
    }

    public override void ApplyTo(Decal decal) {
        int hideRange = (int) decal.GetScaledRadius(_hideRange);
        int showRange = (int) decal.GetScaledRadius(_showRange);

        ((patch_Decal)decal).MakeScaredAnimation(hideRange, showRange, _idleFrames, _hiddenFrames, _showFrames, _hideFrames);
    }
}