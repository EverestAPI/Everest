using System.Xml;

namespace Celeste.Mod.Registry.DecalRegistryHandlers;

internal sealed class AnimationSpeedDecalRegistryHandler : DecalRegistryHandler {
    private float? _value;

    public override string Name => "animationSpeed";

    public override void Parse(XmlAttributeCollection xml) {
        _value = GetNullable<float>(xml, "value");
    }

    public override void ApplyTo(Decal decal) {
        if (_value is { } value)
            decal.AnimationSpeed = value;
    }
}
