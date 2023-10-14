param ($version)
# Make the Everest version match the build number.
$BuildNumber = [string]([int]$env:BUILD_BUILDID + [int]$env:BUILDIDOFFSET)
$EverestPath = [io.path]::combine($env:BUILD_SOURCESDIRECTORY, 'Celeste.Mod.mm', 'Mod', 'Everest', 'Everest.cs')
(Get-Content $EverestPath) -replace '(?<=public readonly static string VersionString = ")[^"]*', $version | Set-Content $EverestPath

# Currently unstable/in development
$HelperPath = [io.path]::combine($env:BUILD_SOURCESDIRECTORY, 'Celeste.Mod.mm', 'Mod', 'Helpers', 'EverestVersion.cs')
echo @"
namespace Celeste.Mod.Helpers {
    internal static class EverestVersion$BuildNumber {
        public static string EverestBuild = "EverestBuild$BuildNumber";
        public static string EverestVersion = "EverestVersion$version";
    }
}
"@ | Set-Content $HelperPath
