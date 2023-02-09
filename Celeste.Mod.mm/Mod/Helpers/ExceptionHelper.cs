using MonoMod;
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
                IEnumerable<MethodBase> methods = new StackTrace(e).GetFrames()?.Select(f => f.GetMethod());
                // DMD names are in the format DMD<Type::Method>
                return methods?.Any(m => m != null && (m.DeclaringType == t || (m.IsDynamicMethod() && Regex.Match(m.Name, @"DMD<(.*)::").Groups[1].Value == t.ToString()))) ?? false;
            }
            return false;
        }

        public static bool MethodInStacktrace(this Exception e, Type t, string methodName) {
            MethodBase target = t?.GetMethod(methodName);
            if (e != null && target != null) {
                IEnumerable<MethodBase> methods = new StackTrace(e).GetFrames()?.Select(f => f.GetMethod());
                return methods?.Any(m => m != null && (m == target || (m.IsDynamicMethod() && m.Name == $"DMD<{target.GetID(simple: true)}>"))) ?? false;
            }
            return false;
        }

        [RelinkLegacyMonoMod("System.Void MonoMod.Utils.Extensions::LogDetailed(System.Exception,System.String)")]
        public static void LogDetailed(this Exception e, string tag = null) {
            if (tag == null) {
                Console.WriteLine("--------------------------------");
                Console.WriteLine("Detailed exception log:");
            }
            for (Exception e_ = e; e_ != null; e_ = e_.InnerException) {
                Console.WriteLine("--------------------------------");
                Console.WriteLine(e_.GetType().FullName + ": " + e_.Message + "\n" + e_.StackTrace);
                if (e_ is ReflectionTypeLoadException rtle) {
                    for (int i = 0; i < rtle.Types.Length; i++) {
                        Console.WriteLine("ReflectionTypeLoadException.Types[" + i + "]: " + rtle.Types[i]);
                    }
                    for (int i = 0; i < rtle.LoaderExceptions.Length; i++) {
                        LogDetailed(rtle.LoaderExceptions[i], tag + (tag == null ? "" : ", ") + "rtle:" + i);
                    }
                }
                if (e_ is TypeLoadException) {
                    Console.WriteLine("TypeLoadException.TypeName: " + ((TypeLoadException) e_).TypeName);
                }
                if (e_ is BadImageFormatException) {
                    Console.WriteLine("BadImageFormatException.FileName: " + ((BadImageFormatException) e_).FileName);
                }
            }
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
