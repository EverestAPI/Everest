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
	Path = "$env:BUILD_ARTIFACTSTAGINGDIRECTORY/main/*"
	CompressionLevel = "Optimal"
	DestinationPath = "$ZIP"
}
Compress-Archive @compress

Write-Output "Building Olympus metadata artifact"
Write-Output (Get-Item -Path $ZIP).length | Out-File -FilePath $OLYMPUS/meta/size.txt
Write-Host "##vso[task.setvariable variable=olympus]True"

# lib-stripped setup
if ([string]::IsNullOrEmpty("$env:BIN_URL") -or ($env:BIN_URL -eq '$(BIN_URL)')) {
	Write-Output "Skipping lib-stripped artifact"
	Exit 0
}

$LIB_STRIPPED="$env:BUILD_ARTIFACTSTAGINGDIRECTORY/lib-stripped"
if ($LIB_STRIPPED -eq "/lib-stripped") {
	$LIB_STRIPPED = "./tmp-lib-stripped"
}

Write-Output "Creating lib-stripped artifact directories"
Remove-Item -ErrorAction Ignore -Recurse -Force -Path $LIB_STRIPPED
New-Item -ItemType "directory" -Path $LIB_STRIPPED
New-Item -ItemType "directory" -Path $LIB_STRIPPED/build

Write-Output "Copying patched files"
Copy-Item -Path "$env:BUILD_ARTIFACTSTAGINGDIRECTORY/patch/*" -Destination $LIB_STRIPPED

Write-Output "Generating stripped files"
$files = Get-ChildItem -Path "$LIB_STRIPPED/*" -Include *.dll,*.exe
foreach ($dll in $files) {
	mono-cil-strip -q $dll
}
Copy-Item $files -Destination "$LIB_STRIPPED/build"
Write-Host "##vso[task.setvariable variable=lib_stripped]True"
