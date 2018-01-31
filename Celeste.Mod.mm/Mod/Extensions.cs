using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Celeste.Mod {
    public static class Extensions {

        public static string ToHexadecimalString(this byte[] data)
            => BitConverter.ToString(data).Replace("-", string.Empty);

    }
}
