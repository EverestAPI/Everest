using MonoMod;
using System;
using System.Globalization;

namespace NETCoreifier {
    // The float formatting behavior was changed for .NET Core 3.0+
    // This causes some CelesteTAS jank to break, but maybe other mods as well, so patch it
    // (see https://devblogs.microsoft.com/dotnet/floating-point-parsing-and-formatting-improvements-in-net-core-3-0/)
    public static class FloatFormattingShims {

        private static string FixupFormatString(string format, bool isDouble) {
            if (format == null || format.Equals("G", StringComparison.OrdinalIgnoreCase))
                return isDouble ? "G15" : "G6";

            if (format.StartsWith("G", StringComparison.OrdinalIgnoreCase) && int.TryParse(format[1..], NumberStyles.Integer, CultureInfo.InvariantCulture, out int i) && i > (isDouble ? 15 : 6))
                return isDouble ? "G17" : "G9";

            return format;
        }

        [MonoModLinkFrom("System.String System.Single::ToString()")]
        public static string Float_ToString(ref float v) => Float_ToString(ref v, (string) null);
        [MonoModLinkFrom("System.String System.Single::ToString(System.String)")]
        public static string Float_ToString(ref float v, string format) => v.ToString(FixupFormatString(format, false));
        [MonoModLinkFrom("System.String System.Single::ToString(System.IFormatProvider)")]
        public static string Float_ToString(ref float v, IFormatProvider prov) => Float_ToString(ref v, (string) null, prov);
        [MonoModLinkFrom("System.String System.Single::ToString(System.String,System.IFormatProvider)")]
        public static string Float_ToString(ref float v, string format, IFormatProvider prov) => v.ToString(FixupFormatString(format, false), prov);

        [MonoModLinkFrom("System.String System.Double::ToString()")]
        public static string Double_ToString(ref double v) => Double_ToString(ref v, (string) null);
        [MonoModLinkFrom("System.String System.Double::ToString(System.String)")]
        public static string Double_ToString(ref double v, string format) => v.ToString(FixupFormatString(format, true));
        [MonoModLinkFrom("System.String System.Double::ToString(System.IFormatProvider)")]
        public static string Double_ToString(ref double v, IFormatProvider prov) => Double_ToString(ref v, (string) null, prov);
        [MonoModLinkFrom("System.String System.Double::ToString(System.String,System.IFormatProvider)")]
        public static string Double_ToString(ref double v, string format, IFormatProvider prov) => v.ToString(FixupFormatString(format, true), prov);

    }
}