Add-Type -AssemblyName System.IO.Compression.FileSystem

$OLYMPUS="$env:BUILD_ARTIFACTSTAGINGDIRECTORY/olympus/"
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
$compress = @{
	Path = "$env:BUILD_ARTIFACTSTAGINGDIRECTORY/main"
	CompressionLevel = "Optimal"
	DestinationPath = "$ZIP"
}
Compress-Archive @compress

Write-Output "Building Olympus metadata artifact"
Write-Output (Get-Item -Path $ZIP).length | Out-File -FilePath $OLYMPUS/meta/size.txt

# lib-stripped setup
$LIB_STRIPPED="$env:BUILD_ARTIFACTSTAGINGDIRECTORY/lib-stripped/"
if ($LIB_STRIPPED -eq "/lib-stripped/") {
	$LIB_STRIPPED = "./tmp-lib-stripped/"
}

$ZIP="$LIB_STRIPPED/build/build.zip"

Write-Output "Installing SteamRE DepotDownloader"
Invoke-WebRequest -URI 'https://github.com/SteamRE/DepotDownloader/releases/download/DepotDownloader_2.4.4/depotdownloader-2.4.4.zip' -OutFile "$env:AGENT_TEMPDIRECTORY/depotdownloader.zip"
Expand-Archive -Path "$env:AGENT_TEMPDIRECTORY/depotdownloader.zip" -Destination $env:AGENT_TOOLSDIRECTORY

Write-Output "Downloading Celeste package"
dotnet $env:AGENT_TOOLSDIRECTORY/DepotDownloader.dll -app 504230 -beta opengl -username $env:STEAM_USERNAME -password $env:STEAM_PASSWORD -dir $LIB_STRIPPED

Write-Output "Applying Everest patch"
Copy-Item -Path "$env:BUILD_ARTIFACTSTAGINGDIRECTORY/main/*" -Destination $LIB_STRIPPED -Recurse
Start-Process -FilePath "Miniinstaller.exe" -WorkingDirectory "$LIB_STRIPPED"

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