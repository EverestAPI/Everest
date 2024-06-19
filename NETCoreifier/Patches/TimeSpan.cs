using MonoMod;
using System;

namespace NETCoreifier {
    // TimeSpan.FromXYZ is "broken" on .NET Framework, in the sense that it rounds the value given to it to the nearest ms
    // (even though TimeSpans have a much higher internal precision)
    // .NET Core seems to do this conversion properly now, so we have to shim these methods as otherwise e.g. the speedrun timer desyncs
    public static class TimeSpanShims {

        // Taken from the MS reference source code
        
        private const int MillisPerSecond = 1000, MillisPerMinute = MillisPerSecond * 60, MillisPerHour = MillisPerMinute * 60, MillisPerDay = MillisPerHour * 24;

        private static TimeSpan Interval(double value, int scale) {
            if (Double.IsNaN(value))
                throw new ArgumentException("TimeSpan does not accept floating point Not-a-Number values.");

            double tmp = value * scale;
            double millis = tmp + (value >= 0 ? +0.5: -0.5);
            if ((millis > Int64.MaxValue / TimeSpan.TicksPerMillisecond) || (millis < Int64.MinValue / TimeSpan.TicksPerMillisecond))
                throw new OverflowException("TimeSpan overflowed because the duration is too long.");

            return new TimeSpan((long) millis * TimeSpan.TicksPerMillisecond);
        }

        [MonoModLinkFrom("System.TimeSpan System.TimeSpan::FromMilliseconds(System.Double)")]
        public static TimeSpan FromMilliseconds(double val) => Interval(val, 1);

        [MonoModLinkFrom("System.TimeSpan System.TimeSpan::FromSeconds(System.Double)")]
        public static TimeSpan FromSeconds(double val) => Interval(val, MillisPerSecond);

        [MonoModLinkFrom("System.TimeSpan System.TimeSpan::FromMinutes(System.Double)")]
        public static TimeSpan FromMinutes(double val) => Interval(val, MillisPerMinute);

        [MonoModLinkFrom("System.TimeSpan System.TimeSpan::FromHours(System.Double)")]
        public static TimeSpan FromHours(double val) => Interval(val, MillisPerHour);

        [MonoModLinkFrom("System.TimeSpan System.TimeSpan::FromDays(System.Double)")]
        public static TimeSpan FromDays(double val) => Interval(val, MillisPerDay);

    }
}