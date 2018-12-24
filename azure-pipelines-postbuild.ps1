Add-Type -AssemblyName System.IO.Compression.FileSystem
Add-Type -Path "azure-pipelines-ext.cs" -ReferencedAssemblies "System.IO.Compression.FileSystem"

$BuildNumber = [string]([int]$env:BUILD_BUILDID + [int]$env:BUILD_BUILDIDOFFSET)
$Suffix = "-$env:BUILD_SOURCEBRANCHNAME"

if ($Suffix -eq "-master") {
	$Suffix = ""
}

$ZIP="build-$BuildNumber$Suffix.zip"

echo "Create .zip"
[EverestPS]::Zip($env:BUILD_ARTIFACTSTAGINGDIRECTORY, $ZIP)

echo "Get latest builds_index.txt"
Invoke-WebRequest -Uri "https://lollyde.ams3.digitaloceanspaces.com/everest-travis/builds_index.txt" -OutFile "builds_index.txt"

echo "Update builds_index.txt"
Add-Content builds_index.txt "/lollyde/everest-travis/$ZIP $ZIP`n"

echo "Pushing build to S3"
[EverestPS]::PutS3(".", $ZIP, "/everest-travis/", "application/x-compressed-zip")

echo "Generating new index.html"
[EverestPS]::RebuildHTML("builds_index.txt", "index.html")

echo "Pushing index.html to S3"
[EverestPS]::PutS3(".", "index.html", "/", "text/html")

echo "Pushing builds_index.txt to S3"
[EverestPS]::PutS3(".", "builds_index.txt", "/everest-travis/", "text/plain")
