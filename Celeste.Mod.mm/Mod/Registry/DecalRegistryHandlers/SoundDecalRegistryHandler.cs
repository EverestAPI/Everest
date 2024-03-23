using System.Xml;

namespace Celeste.Mod.Registry.DecalRegistryHandlers; 

internal sealed class SoundDecalRegistryHandler : DecalRegistryHandler {
    private string _event;
    
    public override string Name => "sound";
    
    public override void Parse(XmlAttributeCollection xml) {
        _event = GetString(xml, "event", null);
    }

    public override void ApplyTo(Decal decal) {
        if (_event is { } @event) {
            decal.Add(new SoundSource(@event));
        }
    }
}