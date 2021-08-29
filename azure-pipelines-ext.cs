// This file is used in the Azure Pipelines PowerShell scripts via:
// Add-Type -Path "azure-pipelines-ext.cs"

// Note that this file is restricted to C# 5.0

using System;
using System.IO;
using System.Text;
using System.Security.Cryptography;
using System.Globalization;
using System.Net;
using System.IO.Compression;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;

public class EverestPS {

    class ZipPathEncoder : UTF8Encoding {
        public ZipPathEncoder()
            : base(true) {
        }
        public override byte[] GetBytes(string s) {
            return base.GetBytes(s.Replace("\\", "/"));
        }
    }
    public static UTF8Encoding ZipPathEncoding = new ZipPathEncoder();

    public static void Zip(string dir, string file) {
        ZipFile.CreateFromDirectory(dir, file, CompressionLevel.Optimal, false, ZipPathEncoding);
    }

}
