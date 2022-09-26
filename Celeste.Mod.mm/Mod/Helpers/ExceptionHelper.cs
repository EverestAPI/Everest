using MonoMod.Utils;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Reflection;
using System.Text.RegularExpressions;

namespace Celeste.Mod.Helpers {
    public static class ExceptionExt {

        public static bool TypeInStacktrace(this Exception e, Type t) {
            if (e != null && t != null) {
                IEnumerable<MethodBase> methods = new StackTrace(e).GetFrames().Select(f => f.GetMethod());
                // DMD names are in the format DMD<Type::Method>
                return methods.Any(m => m.DeclaringType == t || (m.IsDynamicMethod() && Regex.Match(m.Name, @"DMD<(.*)::").Value == t.ToString()));
            }
            return false;
        }

        public static bool MethodInStacktrace(this Exception e, Type t, string methodName) {
            MethodBase target = t?.GetMethod(methodName);
            if (e != null && target != null) {
                IEnumerable<MethodBase> methods = new StackTrace(e).GetFrames().Select(f => f.GetMethod());
                return methods.Any(m => m == target || (m.IsDynamicMethod() && m.Name == $"DMD<{target.GetID(simple: true)}>"));
            }
            return false;
        }

    }

    internal class AutotilerException : Exception {

        public char ID { get; set; }
        public int X { get; set; }
        public int Y { get; set; }

        public AutotilerException() { }

        public AutotilerException(string message) :
            base(message) {
        }

        public AutotilerException(string message, Exception inner) :
            base(message, inner) {
        }

    }

    public class EndUserException : Exception {

        public EndUserException(string message, Exception innerException)
            : base(message, innerException) {
        }

        public override string ToString() {
            return Message + Environment.NewLine + Environment.NewLine + base.ToString();
        }

    }

}
