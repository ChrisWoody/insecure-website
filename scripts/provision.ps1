Param (
    [ValidateNotNullOrEmpty()]
    [string] $subscriptionId = "",

    [ValidateNotNullOrEmpty()]
    [string]
    $name = "",

    [ValidateNotNullOrEmpty()]
    [ValidateSet("CentralUS", "EastUS", "EastUS2", "NorthCentralUS", "SouthCentralUS", "WestUS", "WestUS2",
      "NorthEurope", "WestEurope", "EastAsia", "SoutheastAsia", "JapanEast", "JapanWest", 
      "BrazilSouth", "AustraliaEast", "AustraliaSoutheast", "CentralIndia", "SouthIndia", "WestIndia")]
    [string] $location = "AustraliaEast",

    [ValidateNotNullOrEmpty()]
    [string]
    $sqlServerAllowedIp = "",

    [ValidateNotNullOrEmpty()]
    [string]
    $sqlServerAdminPassword = ""
)

$ErrorActionPreference = "Stop"
Set-StrictMode -Version "Latest"

try
{
    $resourceGroupName = $name + "rg";
    $appServicePlanName = $name + "plan";
    $appServiceName = $name + "app";
    $appInsightsWorkspaceName = $name + "ws";
    $appInsightsName = $name + "ai";
    $storageAccountName = $name + "str";
    $sqlServerName = $name + "sql";
    $sqlDatabaseName = $name + "db";
    $sqlServerAdminUsername = "sqladmin";
    $sqlDatabaseConnectionString = "Server=tcp:$sqlServerName.database.windows.net,1433;Database=$sqlDatabaseName;User ID=$sqlServerAdminUsername;Password=$sqlServerAdminPassword;Encrypt=true;TrustServerCertificate=false;Connection Timeout=30;"
    $sqlDatabaseMasterConnectionString = $sqlDatabaseConnectionString -replace $sqlDatabaseName, "master"

    az account set --subscription $subscriptionId

    # Resouce group
    if ((az group exists --name $resourceGroupName) -eq 'false')
    {
        Write-Host "Creating the resource group $resourceGroupName"
        az group create --name $resourceGroupName --location $location
    }
    else
    {
        Write-Host "Resource group $resourceGroupName already exists"
    }

    # Storage account (for sql audit logs)
    if (((az storage account check-name --name $storageAccountName) | ConvertFrom-Json).nameAvailable -eq $true)
    {
        Write-Host "Creating the storage account $storageAccountName"
        az storage account create --resource-group $resourceGroupName --name $storageAccountName --location $location --sku Standard_LRS --min-tls-version TLS1_2
    }
    else
    {
        Write-Host "Storage account $storageAccountName already exists"
    }

    # SQL Server
    $sqlserver = (az sql server list --resource-group $resourceGroupName --query "[?name=='$sqlServerName']" | ConvertFrom-Json)
    if ($sqlserver.Length -eq 0)
    {
        Write-Host "Creating the SQL server $sqlServerName"
        az sql server create --resource-group $resourceGroupName --name $sqlServerName --location $location --admin-user $sqlServerAdminUsername --admin-password $sqlServerAdminPassword --minimal-tls-version 1.2
        az sql server firewall-rule create --resource-group $resourceGroupName --server $sqlServerName --name "rule1" --start-ip-address $sqlServerAllowedIp --end-ip-address $sqlServerAllowedIp
        az sql server firewall-rule create --resource-group $resourceGroupName --server $sqlServerName --name "rule2" --start-ip-address "0.0.0.0" --end-ip-address "0.0.0.0"
        az sql server audit-policy update --resource-group $resourceGroupName --name $sqlServerName --state Enabled --bsts Enabled --storage-account $storageAccountName
    }
    else
    {
        Write-Host "SQL server $sqlServerName already exists"
    }

    # SQL database (also create user with limited access so people can't delete/drop data)
    $sqlDatabase = (az sql db list --resource-group $resourceGroupName --server $sqlServerName --query "[?name=='$sqlDatabaseName']" | ConvertFrom-Json)
    if ($sqlDatabase.Length -eq 0)
    {
        Write-Host "Creating the SQL database $sqlDatabaseName"
        az sql db create --resource-group $resourceGroupName --server $sqlServerName --name $sqlDatabaseName --service-objective S3
        Write-Host "SQL database $sqlDatabaseName created, waiting a bit before seeding database"
        Start-Sleep -Seconds 10
        Invoke-Sqlcmd -ConnectionString $sqlDatabaseConnectionString -InputFile "..\InsecureWebsite\Models\DatabaseScript.sql"
        Invoke-Sqlcmd -ConnectionString $sqlDatabaseMasterConnectionString -Query "CREATE LOGIN [WebAppLogin] WITH PASSWORD=N'$sqlServerAdminPassword'"
        Invoke-Sqlcmd -ConnectionString $sqlDatabaseConnectionString -Query "CREATE USER [WebAppUser] FOR LOGIN [WebAppLogin]"
        Invoke-Sqlcmd -ConnectionString $sqlDatabaseConnectionString -Query "GRANT SELECT, INSERT, UPDATE ON SCHEMA::[dbo] TO [WebAppUser]"
    }
    else
    {
        Write-Host "SQL database $sqlDatabaseName already exists"
    }

    # App service plan
    $appServicePlan = (az appservice plan list --resource-group $resourceGroupName --query "[?name=='$appServicePlanName']" | ConvertFrom-Json)
    if ($appServicePlan.Length -eq 0)
    {
        Write-Host "Creating the app service plan $appServicePlanName"
        az appservice plan create --name $appServicePlanName --resource-group $resourceGroupName --location $location --sku S3
    }
    else
    {
        Write-Host "App service plan $appServicePlanName already exists"
    }

    # App service
    $appService = (az webapp list --query "[?name=='$appServiceName']" | ConvertFrom-Json)
    if ($appService.Length -eq 0)
    {
        Write-Host "Creating the app service $appServiceName"
        az webapp create --name $appServiceName --resource-group $resourceGroupName --plan $appServicePlanName --runtime "DOTNET:6.0"
        az webapp cors add --name $appServiceName --resource-group $resourceGroupName --allowed-origins "https://localhost"
        az resource update --name web --resource-group $resourceGroupName --namespace Microsoft.Web --resource-type config --parent sites/$appServiceName --set properties.cors.supportCredentials=true

        $webAppLoginConnectionString = $sqlDatabaseConnectionString -replace $sqlServerAdminUsername, "WebAppLogin"
        az webapp config connection-string set --resource-group $resourceGroupName --name $appServiceName --connection-string-type SQLAzure --settings "DatabaseConnectionString=$webAppLoginConnectionString"
        az webapp config appsettings set --resource-group $resourceGroupName --name $appServiceName --settings "ASPNETCORE_ENVIRONMENT=Development"
        az webapp config appsettings set --resource-group $resourceGroupName --name $appServiceName --settings "ShowPublicBoard=true"
        az webapp config appsettings set --resource-group $resourceGroupName --name $appServiceName --settings "ShowMessenger=true"
    }
    else
    {
        Write-Host "App service $appServiceName already exists"
    }

    # Log analytics workspace
    $workspace = (az monitor log-analytics workspace list --query "[?name=='$appInsightsWorkspaceName']" | ConvertFrom-Json)
    if ($workspace.Length -eq 0)
    {
        Write-Host "Creating the app insights workspace $appInsightsWorkspaceName"
        az monitor log-analytics workspace create --resource-group $resourceGroupName --workspace-name $appInsightsWorkspaceName --location $location
    }
    else
    {
        Write-Host "App insights workspace $appInsightsWorkspaceName already exists"
    }
    
    # App insights
    $appInsights = (az monitor app-insights component show --app $appInsightsName --resource-group $resourceGroupName | ConvertFrom-Json)
    if ($null -eq $appInsights)
    {
        Write-Host "Creating the app insights $appInsightsName"
        az monitor app-insights component create --app $appInsightsName --resource-group $resourceGroupName --location $location --kind web --application-type web --workspace $appInsightsWorkspaceName
        az monitor app-insights component connect-webapp --resource-group $resourceGroupName --web-app $appServiceName --app $appInsightsName
    }
    else
    {
        Write-Host "App insights $appInsightsName already exists"
    }

    if ($LASTEXITCODE -eq 1)
    {
        Write-Warning "Provisioning completed with issues"
        exit 1
    }
    
    Write-Host "Provisioning completed!" -ForegroundColor Green
    exit 0
}
catch
{
    Write-Error $_ -ErrorAction Continue
    exit 1
}