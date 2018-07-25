using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using YamlDotNet.Serialization;

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
