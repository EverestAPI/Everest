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
$LIB_STRIPPED="$env:BUILD_ARTIFACTSTAGINGDIRECTORY/lib-stripped"
if ($LIB_STRIPPED -eq "/lib-stripped") {
	$LIB_STRIPPED = "./tmp-lib-stripped"
}

Write-Output "Creating lib-stripped artifact directories"
Remove-Item -ErrorAction Ignore -Recurse -Force -Path $LIB_STRIPPED
New-Item -ItemType "directory" -Path $LIB_STRIPPED
New-Item -ItemType "directory" -Path $LIB_STRIPPED/build

Write-Output "Downloading Celeste package"
$creds = [System.Convert]::ToBase64String([System.Text.Encoding]::ASCII.GetBytes("$env:BIN_USERNAME:$env:BIN_PASSWORD"))
$headers = @{'Authorization'= "Basic $creds"}
Invoke-WebRequest -URI "$env:BIN_URL/Celeste_Linux.zip" -OutFile "$env:AGENT_TEMPDIRECTORY/Celeste.zip" -Headers $headers
Expand-Archive -Path "$env:AGENT_TEMPDIRECTORY/Celeste.zip" -DestinationPath $LIB_STRIPPED

Write-Output "Applying Everest patch"
Copy-Item -Path "$env:BUILD_ARTIFACTSTAGINGDIRECTORY/main/*" -Destination $LIB_STRIPPED
Start-Process -FilePath "mono" -ArgumentList "$LIB_STRIPPED/MiniInstaller.exe" -WorkingDirectory $LIB_STRIPPED -Wait

Write-Output "Generating stripped files"
$files = Get-ChildItem -Path "$LIB_STRIPPED/*" -Include *.dll,*.exe
foreach ($dll in $files) {
	mono-cil-strip -q $dll
}
Copy-Item $files -Destination "$LIB_STRIPPED/build"
