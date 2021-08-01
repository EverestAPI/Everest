# Make the Everest version match the build number.
$BuildNumber = [string]([int]$env:Build_BuildId + [int]$env:Build_BuildIdOffset)
$EverestPath = [io.path]::combine('Celeste.Mod.mm', 'Mod', 'Everest', 'Everest.cs')
(Get-Content $EverestPath) -replace '(?<=public readonly static string VersionString = ")[^"]*', "1.$BuildNumber.0-azure-$(($env:BUILD_SOURCEVERSION).Substring(0, 5))" | Set-Content $EverestPath

# Currently unstable/in development
$HelperPath = [io.path]::combine('Celeste.Mod.mm', 'Mod', 'Helpers', 'EverestVersion.cs')
echo @"
namespace Celeste.Mod.Helpers {
    private static class EverestBuild$BuildNumber {
        public static string EverestBuild = "EverestBuild$BuildNumber";
    }
}
"@ | Set-Content $HelperPath