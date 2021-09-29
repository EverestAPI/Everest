# Make the Everest version match the build number.
$BuildNumber = [string]([int]$env:BUILD_BUILDID + [int]$env:BUILD_BUILDIDOFFSET)
$EverestPath = [io.path]::combine($env:BUILD_SOURCESDIRECTORY, 'Celeste.Mod.mm', 'Mod', 'Everest', 'Everest.cs')
(Get-Content $EverestPath) -replace '(?<=public readonly static string VersionString = ")[^"]*', "1.$BuildNumber.0-azure-$(($env:BUILD_SOURCEVERSION).Substring(0, 5))" | Set-Content $EverestPath

# Currently unstable/in development
$HelperPath = [io.path]::combine($env:BUILD_SOURCESDIRECTORY, 'Celeste.Mod.mm', 'Mod', 'Helpers', 'EverestVersion.cs')
echo @"
namespace Celeste.Mod.Helpers {
    internal static class EverestBuild$BuildNumber {
        public static string EverestBuild = "EverestBuild$BuildNumber";
    }
}
"@ | Set-Content $HelperPath
