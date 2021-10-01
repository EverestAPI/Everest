# Patch verification
if ([string]::IsNullOrEmpty("$env:BIN_URL") -or ($env:BIN_URL -eq '$(BIN_URL)')) {
	Write-Output "Skipping patch verification"
	Exit 0
}

$PATCH="$env:BUILD_ARTIFACTSTAGINGDIRECTORY/patch"
if ($PATCH -eq "/patch") {
	$PATCH = "./tmp-patch"
}

Write-Output "Creating patch directories"
Remove-Item -ErrorAction Ignore -Recurse -Force -Path $PATCH
New-Item -ItemType "directory" -Path $PATCH
New-Item -ItemType "directory" -Path $PATCH/build

Write-Output "Downloading Celeste package"
$creds = [System.Convert]::ToBase64String([System.Text.Encoding]::ASCII.GetBytes("$($env:BIN_USERNAME):$($env:BIN_PASSWORD)"))
$headers = @{'Authorization'= "Basic $creds"}
Invoke-WebRequest -URI "$env:BIN_URL/Celeste_Linux.zip" -OutFile "$env:AGENT_TEMPDIRECTORY/Celeste.zip" -Headers $headers
Expand-Archive -Path "$env:AGENT_TEMPDIRECTORY/Celeste.zip" -DestinationPath $PATCH

Write-Output "Applying Everest patch"
Copy-Item -Path "$env:BUILD_ARTIFACTSTAGINGDIRECTORY/main/*" -Destination $PATCH
Start-Process -FilePath "mono" -ArgumentList "$PATCH/MiniInstaller.exe" -WorkingDirectory $PATCH -Wait
