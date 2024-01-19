using MonoMod;

namespace Celeste.Mod.Helpers {
    public class LoggedMonoModder : MonoModder {

        public override void Log(string text)
            => Logger.Debug("monomod", text);

        public override void LogVerbose(string text) {
            if (LogVerboseEnabled)
                Logger.Verbose("monomod", text);
        }

    }
}