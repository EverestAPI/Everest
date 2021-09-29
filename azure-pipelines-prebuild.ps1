# Make the Everest version match the build number.
echo $env:Build_BuildId
echo $env:Build_BuildIdOffset
$BuildNumber = [string]([int]$env:Build_BuildId + [int]$env:Build_BuildIdOffset)
$EverestPath = [io.path]::combine($env:Build_SourcesDirectory, 'Celeste.Mod.mm', 'Mod', 'Everest', 'Everest.cs')
echo $BuildNumber
(Get-Content $EverestPath) -replace '(?<=public readonly static string VersionString = ")[^"]*', "1.$BuildNumber.0-azure-$(($env:BUILD_SOURCEVERSION).Substring(0, 5))" | Set-Content $EverestPath
echo ""
cat $EverestPath

# Currently unstable/in development
$HelperPath = [io.path]::combine($env:Build_SourcesDirectory, 'Celeste.Mod.mm', 'Mod', 'Helpers', 'EverestVersion.cs')
echo @"
namespace Celeste.Mod.Helpers {
    internal static class EverestBuild$BuildNumber {
        public static string EverestBuild = "EverestBuild$BuildNumber";
    }
}
"@ | Set-Content $HelperPath
