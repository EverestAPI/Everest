using MonoMod;
using System.IO;
using System.IO.MemoryMappedFiles;
using System.Runtime.Versioning;
using System.Security;

namespace NETCoreifier {
    // I'm 100% sure only CelesteTas will ever run into this breaking change between net452 and net5.0+ .-.
    public static class MemoryMappedFileShims {
        private const string FileStreamFName = "System.IO.FileStream", HandleInheritabilityFName = "System.IO.HandleInheritability";

        private const string MemoryMappedFilesNSpace = "System.IO.MemoryMappedFiles";
        private const string MemoryMappedFileFName = $"{MemoryMappedFilesNSpace}.MemoryMappedFile";
        private const string MemoryMappedFileAccessFName = $"{MemoryMappedFilesNSpace}.MemoryMappedFileAccess";
        private const string MemoryMappedFileOptionsFName = $"{MemoryMappedFilesNSpace}.MemoryMappedFileOptions";
        private const string MemoryMappedFileSecurityFName = "NETCoreifier.MemoryMappedFileShims/MemoryMappedFileSecurity"; // Relinked

        [MonoModLinkFrom($"{MemoryMappedFilesNSpace}.MemoryMappedFileSecurity")]
        public class MemoryMappedFileSecurity {
            internal MemoryMappedFileSecurity() {}
        }

        [SecurityCritical]
        [MonoModLinkFrom($"{MemoryMappedFileFName} {MemoryMappedFileFName}::CreateFromFile({FileStreamFName},System.String,System.Int64,{MemoryMappedFileAccessFName},{MemoryMappedFileSecurityFName},{HandleInheritabilityFName},System.Boolean)")]
        public static MemoryMappedFile CreateFromFile(FileStream stream, string name, long capacity, MemoryMappedFileAccess access, MemoryMappedFileSecurity security, HandleInheritability inherit, bool leaveOpen)
            => MemoryMappedFile.CreateFromFile(stream, name, capacity, access, inherit, leaveOpen);

        [SecurityCritical]
        [SupportedOSPlatform("windows")]
        [MonoModLinkFrom($"{MemoryMappedFileFName} {MemoryMappedFileFName}::CreateOrOpen(System.String,System.Int64,{MemoryMappedFileAccessFName},{MemoryMappedFileOptionsFName},{MemoryMappedFileSecurityFName},{HandleInheritabilityFName})")]
        public static MemoryMappedFile CreateOrOpen(string name, long capacity, MemoryMappedFileAccess access, MemoryMappedFileOptions options, MemoryMappedFileSecurity security, HandleInheritability inherit)
            => MemoryMappedFile.CreateOrOpen(name, capacity, access, options, inherit);

        [SecurityCritical]
        [MonoModLinkFrom($"{MemoryMappedFileSecurityFName} {MemoryMappedFileFName}::GetAccessControl()")]
        public static MemoryMappedFileSecurity GetAccessControl(MemoryMappedFile file) => new MemoryMappedFileSecurity();

        [SecurityCritical]
        [MonoModLinkFrom($"System.Void {MemoryMappedFileFName}::SetAccessControl({MemoryMappedFileSecurityFName})")]
        public static void SetAccessControl(MemoryMappedFile file, MemoryMappedFileSecurity security) {}

    }
}