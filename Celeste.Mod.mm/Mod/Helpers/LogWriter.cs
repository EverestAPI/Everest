using System;
using System.Collections.Generic;
using System.IO;
using System.Text;

namespace Celeste.Mod.Helpers {
    public class LogWriter : IDisposable {

        public OutputStreamCapture STDOUT;
        public OutputStreamCapture STDERR;
        public TextWriter File;

        private bool captureStarted = false;

        public LogWriter(TextWriter @out, TextWriter err, TextWriter file, bool beginCapture = true) {
            STDOUT = new OutputStreamCapture(@out, this);
            STDERR = new OutputStreamCapture(err, this);
            File = file;
            if (beginCapture)
                BeginCapture();
        }

        public Encoding Encoding => STDOUT.Encoding ?? STDERR.Encoding ?? File.Encoding;

        public void BeginCapture() {
            if (captureStarted)
                return;
            Console.SetOut(STDOUT);
            Console.SetError(STDERR);
            captureStarted = true;
        }

        public void EndCapture() {
            if (!captureStarted)
                return;
            Console.SetOut(STDOUT.Stream);
            Console.SetError(STDERR.Stream);
            captureStarted = false;
        }

        public void Dispose() {
            EndCapture();
            // TextWriter.Close has no effect if the stream is a console stream
            STDOUT?.Close();
            STDERR?.Close();
            File?.Close();
        }
    }

    public class OutputStreamCapture : TextWriter {
        public TextWriter Stream;
        public LogWriter Writer;

        public OutputStreamCapture(TextWriter stream, LogWriter writer) {
            Stream = stream;
            Writer = writer;
        }

        public override Encoding Encoding => Stream.Encoding;

        public override void Write(string value) {
            Stream.Write(value);
            Writer.File.Write(value);
            Writer.File.Flush();
        }

        public override void WriteLine(string value) {
            Stream.WriteLine(value);
            Writer.File.WriteLine(value);
            Writer.File.Flush();
        }

        public override void Write(char value) {
            Stream.Write(value);
            Writer.File.Write(value);
            Writer.File.Flush();
        }

        public override void Write(char[] buffer, int index, int count) {
            Stream.Write(buffer, index, count);
            Writer.File.Write(buffer, index, count);
            Writer.File.Flush();
        }

        public override void Flush() {
            Stream.Flush();
            Writer.File.Flush();
        }

        public override void Close() => Stream.Close();
    }
}
