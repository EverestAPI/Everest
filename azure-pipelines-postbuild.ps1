Add-Type -AssemblyName System.IO.Compression.FileSystem
Add-Type -Path "azure-pipelines-ext.cs" -ReferencedAssemblies @(
	"System.IO.Compression"
	"System.IO.Compression.FileSystem"
	"System.IO.Compression.ZipFile"
	"System.Text.Encoding.Extensions"
)

$OLYMPUS="$env:Build_ArtifactStagingDirectory/olympus/"
if ($OLYMPUS -eq "/olympus/") {
	$OLYMPUS = "./tmp-olympus/"
}

$ZIP="$OLYMPUS/build/build.zip"

Write-Output "Creating Olympus artifact directories"
Remove-Item -ErrorAction Ignore -Recurse -Force -Path $OLYMPUS
New-Item -ItemType "directory" -Path $OLYMPUS
New-Item -ItemType "directory" -Path $OLYMPUS/meta
New-Item -ItemType "directory" -Path $OLYMPUS/build

Write-Output "Building Olympus build artifact"
# Azure Pipelines apparently hates to write to the artifact staging dir directly.
[EverestPS]::Zip("$env:Build_ArtifactStagingDirectory/main", "olympus-build.zip")
Move-Item -Path "olympus-build.zip" -Destination $ZIP

Write-Output "Building Olympus metadata artifact"
Write-Output (Get-Item -Path $ZIP).length | Out-File -FilePath $OLYMPUS/meta/size.txt

# lib-stripped setup
$LIB_STRIPPED="$env:Build_ArtifactStagingDirectory/lib-stripped"
if ($LIB_STRIPPED -eq "/lib-stripped/") {
	$LIB_STRIPPED = "./tmp-lib-stripped/"
}

$ZIP="$LIB_STRIPPED/build/build.zip"

Write-Output "Installing SteamRE DepotDownloader"
Invoke-WebRequest -URI 'https://github.com/SteamRE/DepotDownloader/releases/download/DepotDownloader_2.4.4/depotdownloader-2.4.4.zip' -OutFile "$env:Agent_TempDirectory/depotdownloader.zip"
Expand-Archive -Path "$env:Agent_TempDirectory/depotdownloader.zip" -Destination $env:Agent_ToolsDirectory

Write-Output "Downloading Celeste package"
dotnet DepotDownloader.dll -app 504230 -beta opengl -username $env:STEAM_USERNAME -password $env:STEAM_PASSWORD -dir $LIB_STRIPPED

Write-Output "Applying Everest patch"
Copy-Item -Path "$env:Build_ArtifactStagingDirectory/main/*" -Destination $LIB_STRIPPED -Recurse
Start-Process -FilePath "$LIB_STRIPPED/Miniinstaller.exe" -WorkingDirectory "$LIB_STRIPPED"

Write-Output "Generating stripped files"
$files = Get-ChildItem -Path $LIB_STRIPPED* -Include *.dll,*.exe
foreach ($dll in $files) {
	mono-cil-strip -q $dll
}

Write-Output "Building lib-stripped artifact"
$compress = @{
	Path = "$LIB_STRIPPED/*.dll", "$LIB_STRIPPED/*.exe"
	CompressionLevel = "Optimal"
	DestinationPath = "$ZIP"
}
Compress-Archive @compress