param (
	[switch]$NoArchive,
	[string]$OutputDirectory = $PSScriptRoot,
	[switch]$NoCleanup
)

Set-Location "$PSScriptRoot"
$FilesToInclude = "info.json","build/*","LICENSE"

$modInfo = Get-Content -Raw -Path "info.json" | ConvertFrom-Json
$modId = $modInfo.Id
$modVersion = $modInfo.Version

$DistDir = "$OutputDirectory/dist"
if ($NoArchive) {
	$ZipWorkDir = "$OutputDirectory"
} else {
	$ZipWorkDir = "$DistDir/tmp"
}
$ZipOutDir = "$ZipWorkDir/$modId"

New-Item "$ZipOutDir" -ItemType Directory -Force
Copy-Item -Force -Path $FilesToInclude -Destination "$ZipOutDir"
Invoke-WebRequest -OutFile "$ZipOutDir/offline_translations.csv" -Uri "https://docs.google.com/spreadsheets/d/e/2PACX-1vQGeGpv-zk-TxxN3c87vjhtwJdP2oOYeJHF5nI2cJshF7mrNGHTeqQFOda0fo-zOltSRfNdT_nrHNiW/pub?gid=1191351766&single=true&output=csv"

if (!$NoArchive)
{
	$FILE_NAME = "$DistDir/${modId}_v$modVersion.zip"
	Compress-Archive -Update -CompressionLevel Fastest -Path "$ZipOutDir/*" -DestinationPath "$FILE_NAME"

	if (!$NoCleanup)
	{
		Remove-Item -LiteralPath "$ZipWorkDir" -Force -Recurse
	}
}
