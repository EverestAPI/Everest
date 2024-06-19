using MonoMod;

namespace Celeste.Mod.Helpers {
    public class LoggedMonoModder : MonoModder {

        public override void Log(string text)
            => Logger.Log(LogLevel.Debug, "monomod", text);

        public override void LogVerbose(string text) {
            if (LogVerboseEnabled)
                Logger.Log(LogLevel.Verbose, "monomod", text);
        }

    }
}