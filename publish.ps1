param(
	[string]$rid = "win-x64",
	[string]$configuration = "Release"
)

$project = "LeaveTrackerPro.csproj"
$publishDir = Join-Path -Path $PSScriptRoot -ChildPath "publish\$rid"

Write-Host "Publishing $project ($configuration, $rid) to $publishDir"

dotnet publish $project -c $configuration -r $rid --self-contained true /p:PublishSingleFile=true /p:PublishTrimmed=false -o $publishDir

if ($LASTEXITCODE -ne 0) {
	Write-Error "dotnet publish failed with exit code $LASTEXITCODE"
	exit $LASTEXITCODE
}

# Create a ZIP of the publish output
$zipPath = Join-Path -Path $PSScriptRoot -ChildPath "publish\LeaveTrackerPro-$rid.zip"
if (Test-Path $zipPath) { Remove-Item $zipPath -Force }
Compress-Archive -Path (Join-Path $publishDir "*") -DestinationPath $zipPath
Write-Host "Publish complete. ZIP: $zipPath"
