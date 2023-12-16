# Patch verification
if ([string]::IsNullOrEmpty("$env:BIN_URL") -or ($env:BIN_URL -eq '$(BIN_URL)')) {
	Write-Output "Skipping patch verification"

	if ($env:CACHE_RESTORED -eq "false") {
		# Add placeholder for vanilla path to prevent cache warnings
		New-Item -ItemType "directory" -Path $env:VANILLA_CACHE
		Write-Output "Vanilla cache not available" | Out-File -FilePath $env:VANILLA_CACHE/placeholder.txt
	}

	Exit 0
}

$PATCH="$env:BUILD_ARTIFACTSTAGINGDIRECTORY/patch"
if ($PATCH -eq "/patch") {
	$PATCH = "./tmp-patch"
}

Write-Output "Creating patch directories"
Remove-Item -ErrorAction Ignore -Recurse -Force -Path $PATCH
New-Item -ItemType "directory" -Path $PATCH

if ($env:CACHE_RESTORED -eq "false") {
	Write-Output "Downloading Celeste package"
	$creds = [System.Convert]::ToBase64String([System.Text.Encoding]::ASCII.GetBytes("$($env:BIN_USERNAME):$($env:BIN_PASSWORD)"))
	$headers = @{'Authorization'= "Basic $creds"}
	Invoke-WebRequest -URI "$env:BIN_URL/Celeste_Linux.zip" -OutFile "$env:AGENT_TEMPDIRECTORY/Celeste.zip" -Headers $headers
	Expand-Archive -Path "$env:AGENT_TEMPDIRECTORY/Celeste.zip" -DestinationPath $env:VANILLA_CACHE
}

Copy-Item -Path "$env:VANILLA_CACHE/*" -Destination $PATCH -Recurse

Write-Output "Applying Everest patch"
Copy-Item -Path "$env:BUILD_ARTIFACTSTAGINGDIRECTORY/main/*" -Destination $PATCH -Recurse
$MINIINSTALLER = Start-Process -FilePath "$PATCH/MiniInstaller-linux" -WorkingDirectory $PATCH -Wait -PassThru
if ($MINIINSTALLER.ExitCode -ne 0) {
	Write-Output "##vso[task.logissue type=error]Patch verification step failed."
}
Exit $MINIINSTALLER.ExitCode
