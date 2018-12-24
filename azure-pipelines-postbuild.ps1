param([string]$S3Key="")
param([string]$S3Secret="")

Add-Type -AssemblyName System.IO.Compression.FileSystem
Add-Type -Path "azure-pipelines-ext.cs" -ReferencedAssemblies "System.IO.Compression.FileSystem"

$BuildNumber = [string]([int]$env:Build_BuildId + [int]$env:Build_BuildIdOffset)
$Suffix = "-$env:Build_SourceBranchName"

if ($Suffix -eq "-master") {
	$Suffix = ""
}

$ZIP="build-$BuildNumber$Suffix.zip"

echo "Create .zip"
[EverestPS]::Zip($env:Build_ArtifactStagingDirectory, $ZIP)

echo "Pushing .zip to S3"
[EverestPS]::PutS3($S3Key, $S3Secret, ".", $ZIP, "/everest-travis/", "application/x-compressed-zip")

echo "Get latest builds_index.txt"
Invoke-WebRequest -Uri "https://lollyde.ams3.digitaloceanspaces.com/everest-travis/builds_index.txt" -OutFile "builds_index.txt"

echo "Update builds_index.txt"
Add-Content builds_index.txt "/lollyde/everest-travis/$ZIP $ZIP`n"

echo "Pushing builds_index.txt to S3"
[EverestPS]::PutS3($S3Key, $S3Secret, ".", "builds_index.txt", "/everest-travis/", "text/plain")

echo "Generating new index.html"
[EverestPS]::RebuildHTML("builds_index.txt", "index.html")

echo "Pushing index.html to S3"
[EverestPS]::PutS3($S3Key, $S3Secret, ".", "index.html", "/", "text/html")
