using System;
using System.Xml;

namespace Celeste.Mod.Registry.DecalRegistryHandlers; 

/// <summary>
/// A wrapper class which allows legacy callback-style decal registry handlers to work with the new system
/// </summary>
internal sealed class LegacyDecalRegistryHandler : DecalRegistryHandler {
    private XmlAttributeCollection _xml;
    private readonly Action<Decal, XmlAttributeCollection> _callback;

    public LegacyDecalRegistryHandler(string propName, Action<Decal, XmlAttributeCollection> callback) {
        _callback = callback;
        Name = propName;
    }

    public override string Name { get; }

    public override void Parse(XmlAttributeCollection xml) {
        _xml = xml;
    }

    public override void ApplyTo(Decal decal) {
        _callback(decal, _xml);
    }
}