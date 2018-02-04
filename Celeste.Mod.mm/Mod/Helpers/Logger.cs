using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;

namespace Celeste.Mod {
    public static class Logger {

        // TODO: Allow displaying mod log in future ImGui UI
        // TODO: Log levels (verbose, info, debug, error)

        public static void Log(string tag, string str) {
            Console.Write("(");
            Console.Write(DateTime.Now);
            Console.Write(") [Everest] [");
            Console.Write(tag);
            Console.Write("] ");
            Console.WriteLine(str);


        }

        /// <summary>
        /// Method printing extended loading / reflection exception data to the console.
        /// </summary>
        public static void LogDetailed(this Exception e, string tag = null) {
            Console.WriteLine("--------------------------------");
            Console.WriteLine("Detailed exception log:");
            for (Exception e_ = e; e_ != null; e_ = e_.InnerException) {
                Console.WriteLine("--------------------------------");
                Console.WriteLine(e_.GetType().FullName + ": " + e_.Message + "\n" + e_.StackTrace);
                if (e_ is ReflectionTypeLoadException) {
                    ReflectionTypeLoadException rtle = (ReflectionTypeLoadException) e_;
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
}
