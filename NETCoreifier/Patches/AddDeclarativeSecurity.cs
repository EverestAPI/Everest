using MonoMod;
using System.Reflection.Emit;
using System.Security;
using System.Security.Permissions;

namespace NETCoreifier {
#pragma warning disable SYSLIB0003 // Code security is no longer honored
    public static class DeclarativeSecurityShims {

        private const string EmitNSpace = "System.Reflection.Emit";
        private const string SecurityActionFName = "System.Security.Permissions.SecurityAction";
        private const string PermissionSetFName = "System.Security.PermissionSet";


        [MonoModLinkFrom($"System.Void {EmitNSpace}.TypeBuilder::AddDeclarativeSecurity({SecurityActionFName},{PermissionSetFName})")]
        public static void AddDeclarativeSecurity(TypeBuilder builder, SecurityAction action, PermissionSet perms) {}

        [MonoModLinkFrom($"System.Void {EmitNSpace}.MethodBuilder::AddDeclarativeSecurity({SecurityActionFName},{PermissionSetFName})")]
        public static void AddDeclarativeSecurity(MethodBuilder builder, SecurityAction action, PermissionSet perms) {}

        [MonoModLinkFrom($"System.Void {EmitNSpace}.ConstructorBuilder::AddDeclarativeSecurity({SecurityActionFName},{PermissionSetFName})")]
        public static void AddDeclarativeSecurity(ConstructorBuilder builder, SecurityAction action, PermissionSet perms) {}

    }
#pragma warning restore SYSLIB0003
}