# Make the Everest version match the build number.
$BuildNumber = [string]([int]$env:BUILD_BUILDID + [int]$env:BUILD_BUILDIDOFFSET)
$EverestPath = [io.path]::combine($env:BUILD_SOURCESDIRECTORY, 'Celeste.Mod.mm', 'Mod', 'Everest', 'Everest.cs')
# Get the branch name from Azure Pipelines
if ($env:SYSTEM_PULLREQUEST_PULLREQUESTID -ne $null) {
  # Build triggered by a PR
  $BranchName = "pr$env:SYSTEM_PULLREQUEST_PULLREQUESTNUMBER"
} else {
  # Build triggered by a regular push
  $BranchName = $env:BUILD_SOURCEBRANCHNAME
}
# Compose the version string with branch info
$SourceVersionShort = $env:BUILD_SOURCEVERSION.Substring(0, 5)
$VersionString = "1.$BuildNumber.0-azure-$SourceVersionShort-$BranchName"

# Set Version String
(Get-Content $EverestPath) -replace '(?<=public readonly static string VersionString = ")[^"]*', $VersionString | Set-Content $EverestPath

# Currently unstable/in development
$HelperPath = [io.path]::combine($env:BUILD_SOURCESDIRECTORY, 'Celeste.Mod.mm', 'Mod', 'Helpers', 'EverestVersion.cs')
echo @"
namespace Celeste.Mod.Helpers {
    internal static class EverestBuild$BuildNumber {
        public static string EverestBuild = "EverestBuild$BuildNumber";
    }
}
"@ | Set-Content $HelperPath
