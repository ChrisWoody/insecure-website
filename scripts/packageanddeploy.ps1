Param (
    [ValidateNotNullOrEmpty()]
    [string] $subscriptionId = "",

    [ValidateNotNullOrEmpty()]
    [string] $resourceGroupName = "",

    [ValidateNotNullOrEmpty()]
    [string] $appServiceName = ""
)

$ErrorActionPreference = "Stop"
Set-StrictMode -Version "Latest"

try
{
    # Package
    $srcPath = Join-Path -Path $PSScriptRoot -ChildPath '..\'
    $artifactsPath = Join-Path -Path $PSScriptRoot -ChildPath '\artifacts'
    $artifactsTempPath = Join-Path -Path $PSScriptRoot -ChildPath '\artifactsTemp'

    if (Test-path $artifactsPath) {
        Remove-Item -Recurse -Force $artifactsPath
    }
    if (Test-path $artifactsTempPath) {
        Remove-Item -Recurse -Force $artifactsTempPath
    }

    New-Item -Path $artifactsPath -ItemType Directory | Out-Null
    New-Item -Path $artifactsTempPath -ItemType Directory | Out-Null

    dotnet publish "$srcPath\InsecureWebsite\InsecureWebsite.csproj" -c Release --no-restore --output "$artifactsTempPath\InsecureWebsite\"

    Compress-Archive -Path "$artifactsTempPath\InsecureWebsite\*" -DestinationPath "$artifactsPath\InsecureWebsite.zip" -CompressionLevel Optimal -Force

    # Deploy
    az account set --subscription $subscriptionId

    az webapp deployment source config-zip --resource-group $resourceGroupName --name $appServiceName --src "$artifactsPath\InsecureWebsite.zip" --timeout 120
    Write-Host "Deployed web app" -ForegroundColor Green

    if ($LASTEXITCODE -ne 0)
    {
        exit 1
    }

    exit 0
}
catch
{
    Write-Error $_ -ErrorAction Continue
    exit 1
}