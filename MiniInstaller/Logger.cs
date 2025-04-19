using Celeste.Mod.Helpers;
using System;
using System.IO;

namespace MiniInstaller;

// public static partial class Program {
//     
//     // This can be set from the in-game installer via reflection.
//     public static Action<string> LineLogger;
// }

public static class Logger {
    private static Action<string> LineLogger;
    
    public static void LogLine(string line) {
        LineLogger?.Invoke(line);
        Console.WriteLine(line);
    }
    
    public static void LogErr(string line) {
        LineLogger?.Invoke(line);
        Console.Error.WriteLine(line);
    }
    
    public static DisposableTuple SetupLogger() {
        if (File.Exists(Globals.PathLog))
            File.Delete(Globals.PathLog);
        Stream fileStream = File.OpenWrite(Globals.PathLog);
        StreamWriter fileWriter = new StreamWriter(fileStream, Console.OutputEncoding);
        LogWriter logWriter = new LogWriter(Console.Out, Console.Error, fileWriter);
            
        return new DisposableTuple(logWriter, fileWriter, fileStream);
    }
    
    
    public class DisposableTuple : IDisposable {
        private readonly IDisposable[] _iDisposables;
        public DisposableTuple(params IDisposable[] disposables) {
            _iDisposables = disposables;
        }

        public void Dispose() {
            foreach (IDisposable iDisposable in _iDisposables) {
                iDisposable.Dispose();
            }
        }
    }
}