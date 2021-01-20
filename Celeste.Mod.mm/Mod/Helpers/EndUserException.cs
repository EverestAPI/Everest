using System;

namespace Celeste.Mod {
    public class EndUserException : Exception {

        public EndUserException(string message, Exception innerException)
            : base(message, innerException) {
        }

        public override string ToString() {
            return Message + Environment.NewLine + Environment.NewLine + base.ToString();
        }

    }
}
