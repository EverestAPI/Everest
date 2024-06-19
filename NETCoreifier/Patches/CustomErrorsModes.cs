using MonoMod;

namespace NETCoreifier {
    // Speedrun Tool references this for some god forsaken reason ._.
    [MonoModLinkFrom("System.Runtime.Remoting.CustomErrorsModes")]
    public enum CustomErrorsModes {

        Off = 1,
        On = 0,
        RemoteOnly = 2

    }
}