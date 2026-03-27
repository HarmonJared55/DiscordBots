# =============================================================================
# deploy-aci.ps1
#
# Builds the Docker image, pushes it to Azure Container Registry (ACR),
# and deploys it as an Azure Container Instance (ACI) with a managed
# identity that reads the Discord bot token from Azure Key Vault.
#
# Prerequisites:
#   - Azure CLI (az) installed and logged in
#   - Docker installed and running
#
# Usage:
#   $env:DISCORD_TOKEN = "your-bot-token"
#   $env:DISCORD_GUILD_ID = "your-guild-id"
#   $env:CONNECTION_STRING = "Server=tcp:yourserver.database.windows.net,1433;..."
#   .\deploy-aci.ps1
# =============================================================================

param (
    [string]$ResourceGroup= $(if ($env:RESOURCE_GROUP)    { $env:RESOURCE_GROUP }    else { "commendbot-rg" }),
    [string]$Location         = $(if ($env:LOCATION)          { $env:LOCATION }          else { "eastus" }),
    [string]$AcrName  = $(if ($env:ACR_NAME)          { $env:ACR_NAME }       else { "commendbotacr" }),
  [string]$KeyVaultName     = $(if ($env:KEY_VAULT_NAME)    { $env:KEY_VAULT_NAME }    else { "commendbot-kv" }),
[string]$ImageName        = $(if ($env:IMAGE_NAME)        { $env:IMAGE_NAME }        else { "commendbot" }),
    [string]$ImageTag         = $(if ($env:IMAGE_TAG)         { $env:IMAGE_TAG }   else { "latest" }),
    [string]$ContainerName    = $(if ($env:CONTAINER_NAME)    { $env:CONTAINER_NAME }    else { "commendbot" }),
    [string]$DiscordToken     = $(if ($env:DISCORD_TOKEN)     { $env:DISCORD_TOKEN }     else { throw "Set `$env:DISCORD_TOKEN before running." }),
    [string]$DiscordGuildId   = $(if ($env:DISCORD_GUILD_ID)  { $env:DISCORD_GUILD_ID }  else { throw "Set `$env:DISCORD_GUILD_ID before running." }),
    [string]$ConnectionString = $(if ($env:CONNECTION_STRING)  { $env:CONNECTION_STRING }  else { throw "Set `$env:CONNECTION_STRING before running." })
)

$ErrorActionPreference = "Stop"

# --------------- create resource group ---------------
Write-Host "==> Creating resource group '$ResourceGroup' in '$Location'..."
az group create --name $ResourceGroup --location $Location --output none

# --------------- create ACR ---------------
Write-Host "==> Creating Azure Container Registry '$AcrName'..."
az acr create `
    --resource-group $ResourceGroup `
    --name $AcrName `
    --sku Basic `
    --admin-enabled true `
  --output none

# --------------- create Key Vault & store secret ---------------
Write-Host "==> Creating Azure Key Vault '$KeyVaultName'..."
az keyvault create `
    --resource-group $ResourceGroup `
    --name $KeyVaultName `
    --location $Location `
    --enable-rbac-authorization false `
    --output none

Write-Host "==> Storing Discord token in Key Vault..."
# Key Vault secret names use '--' as separator which maps to ':' in .NET configuration
az keyvault secret set `
    --vault-name $KeyVaultName `
    --name "Discord--Token" `
 --value $DiscordToken `
    --output none

$KeyVaultUri = "https://${KeyVaultName}.vault.azure.net/"

# --------------- build & push image ---------------
$AcrLoginServer = az acr show --name $AcrName --query loginServer --output tsv
$FullImage = "${AcrLoginServer}/${ImageName}:${ImageTag}"

Write-Host "==> Logging in to ACR..."
az acr login --name $AcrName

Write-Host "==> Building Docker image '$FullImage'..."
docker build -t $FullImage .

Write-Host "==> Pushing image to ACR..."
docker push $FullImage

# --------------- retrieve ACR credentials ---------------
$AcrUsername = az acr credential show --name $AcrName --query username --output tsv
$AcrPassword = az acr credential show --name $AcrName --query "passwords[0].value" --output tsv

# --------------- deploy ACI with system-assigned managed identity ---------------
Write-Host "==> Deploying Azure Container Instance '$ContainerName' with managed identity..."
az container create `
    --resource-group $ResourceGroup `
    --name $ContainerName `
    --image $FullImage `
    --cpu 1 `
    --memory 0.5 `
    --restart-policy Always `
    --os-type Linux `
    --assign-identity `
    --registry-login-server $AcrLoginServer `
    --registry-username $AcrUsername `
    --registry-password $AcrPassword `
    --secure-environment-variables `
  KeyVault__VaultUri=$KeyVaultUri `
      Discord__GuildId=$DiscordGuildId `
        ConnectionStrings__CommendBotDb=$ConnectionString `
    --output none

# --------------- grant Key Vault access to ACI managed identity ---------------
Write-Host "==> Granting Key Vault access to container's managed identity..."
$IdentityPrincipalId = az container show `
    --resource-group $ResourceGroup `
    --name $ContainerName `
    --query identity.principalId `
    --output tsv

az keyvault set-policy `
    --name $KeyVaultName `
    --object-id $IdentityPrincipalId `
    --secret-permissions get list `
    --output none

Write-Host ""
Write-Host "==> Deployment complete!"
Write-Host "    Container:  $ContainerName"
Write-Host "    Image:      $FullImage"
Write-Host "    Key Vault:  $KeyVaultUri"
Write-Host ""
Write-Host "    The Discord token is stored in Key Vault and accessed via managed identity."
Write-Host "    No token is passed as an environment variable to the container."
Write-Host ""
Write-Host "NOTE: The container may need to be restarted after the Key Vault policy is applied:"
Write-Host "  az container restart --resource-group $ResourceGroup --name $ContainerName"
Write-Host ""
Write-Host "View logs with:"
Write-Host "  az container logs --resource-group $ResourceGroup --name $ContainerName --follow"
