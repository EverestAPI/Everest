// ReSharper disable InconsistentNaming
using MonoMod;

namespace Celeste;

public struct patch_EntityID
{
    public string Level;
    public int ID;

    // default(EntityID) has a null Level, which will NRE when attempting to add it
    // to a HashSet or Dictionary (as those need to evaluate the object's hash);
    // default to EntityID.None's Level in this case, which is the "null" string
    [MonoModReplace]
    public override int GetHashCode()
        => (Level ?? EntityID.None.Level).GetHashCode() ^ ID;
}
