Add-Type -AssemblyName System.IO.Compression.FileSystem
Add-Type -Path "azure-pipelines-ext.cs" -ReferencedAssemblies "System.IO.Compression.FileSystem"

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
[EverestPS]::Zip($env:Build_ArtifactStagingDirectory, $ZIP)

Write-Output "Building Olympus metadata artifact"
Write-Output (Get-Item -Path $ZIP).length | Out-File -FilePath $OLYMPUS/meta/size.txt
