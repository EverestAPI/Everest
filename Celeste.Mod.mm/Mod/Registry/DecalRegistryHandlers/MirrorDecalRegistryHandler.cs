using System;
using System.Xml;

namespace Celeste.Mod.Registry.DecalRegistryHandlers; 

internal sealed class MirrorDecalRegistryHandler : DecalRegistryHandler {
    private bool _keepOffsetsClose;
    
    public override string Name => "mirror";
    
    public override void Parse(XmlAttributeCollection xml) {
        _keepOffsetsClose = GetBool(xml, "keepOffsetsClose", false);
    }

    public override void ApplyTo(Decal decal) {
        string text = decal.Name.ToLower();
        if (text.StartsWith("decals/", StringComparison.Ordinal))
            text = text.Substring(7);
        
        ((patch_Decal)decal).MakeMirror(text, _keepOffsetsClose);
    }
}