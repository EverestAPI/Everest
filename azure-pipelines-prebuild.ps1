# Make the Everest version match the build number.
$BuildNumber = [string]([int]$env:Build_BuildId + [int]$env:Build_BuildIdOffset)
$EverestPath = [io.path]::combine('Celeste.Mod.mm', 'Mod', 'Everest', 'Everest.cs')
(Get-Content $EverestPath) -replace '(?<=public readonly static string VersionString = ")[^"]*', "1.$BuildNumber.0-azure-$(($env:BUILD_SOURCEVERSION).Substring(0, 5))" | Set-Content $EverestPath
