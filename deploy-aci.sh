#!/bin/bash
# =============================================================================
# deploy-aci.sh
#
# Builds the Docker image, pushes it to Azure Container Registry (ACR),
# and deploys it as an Azure Container Instance (ACI) with a managed
# identity that reads the Discord bot token from Azure Key Vault.
#
# Prerequisites:
#   - Azure CLI (`az`) installed and logged in
#   - Docker installed and running
#
# Usage:
#chmod +x deploy-aci.sh
#   ./deploy-aci.sh
#
# Override any default with environment variables before running:
#   RESOURCE_GROUP=my-rg DISCORD_TOKEN=xxx ./deploy-aci.sh
# =============================================================================

set -euo pipefail

# --------------- configurable parameters ---------------
RESOURCE_GROUP="${RESOURCE_GROUP:-commendbot-rg}"
LOCATION="${LOCATION:-eastus}"
ACR_NAME="${ACR_NAME:-commendbotacr}"   # must be globally unique, lowercase, no dashes
KEY_VAULT_NAME="${KEY_VAULT_NAME:-commendbot-kv}" # must be globally unique
IMAGE_NAME="${IMAGE_NAME:-commendbot}"
IMAGE_TAG="${IMAGE_TAG:-latest}"
CONTAINER_NAME="${CONTAINER_NAME:-commendbot}"

# Secrets — set these as env vars before running the script
DISCORD_TOKEN="${DISCORD_TOKEN:?Set DISCORD_TOKEN env var}"
DISCORD_GUILD_ID="${DISCORD_GUILD_ID:?Set DISCORD_GUILD_ID env var}"
CONNECTION_STRING="${CONNECTION_STRING:?Set CONNECTION_STRING env var (Azure SQL recommended)}"

# --------------- create resource group ---------------
echo "==> Creating resource group '${RESOURCE_GROUP}' in '${LOCATION}'..."
az group create --name "$RESOURCE_GROUP" --location "$LOCATION" --output none

# --------------- create ACR ---------------
echo "==> Creating Azure Container Registry '${ACR_NAME}'..."
az acr create \
  --resource-group "$RESOURCE_GROUP" \
  --name "$ACR_NAME" \
  --sku Basic \
  --admin-enabled true \
  --output none

# --------------- create Key Vault & store secret ---------------
echo "==> Creating Azure Key Vault '${KEY_VAULT_NAME}'..."
az keyvault create \
  --resource-group "$RESOURCE_GROUP" \
  --name "$KEY_VAULT_NAME" \
  --location "$LOCATION" \
  --enable-rbac-authorization false \
  --output none

echo "==> Storing Discord token in Key Vault..."
# Key Vault secret names use '--' as separator which maps to ':' in .NET configuration
az keyvault secret set \
  --vault-name "$KEY_VAULT_NAME" \
  --name "Discord--Token" \
--value "$DISCORD_TOKEN" \
  --output none

KEY_VAULT_URI="https://${KEY_VAULT_NAME}.vault.azure.net/"

# --------------- build & push image ---------------
ACR_LOGIN_SERVER=$(az acr show --name "$ACR_NAME" --query loginServer --output tsv)
FULL_IMAGE="${ACR_LOGIN_SERVER}/${IMAGE_NAME}:${IMAGE_TAG}"

echo "==> Logging in to ACR..."
az acr login --name "$ACR_NAME"

echo "==> Building Docker image '${FULL_IMAGE}'..."
docker build -t "$FULL_IMAGE" .

echo "==> Pushing image to ACR..."
docker push "$FULL_IMAGE"

# --------------- retrieve ACR credentials ---------------
ACR_USERNAME=$(az acr credential show --name "$ACR_NAME" --query username --output tsv)
ACR_PASSWORD=$(az acr credential show --name "$ACR_NAME" --query "passwords[0].value" --output tsv)

# --------------- deploy ACI with system-assigned managed identity ---------------
echo "==> Deploying Azure Container Instance '${CONTAINER_NAME}' with managed identity..."
az container create \
  --resource-group "$RESOURCE_GROUP" \
  --name "$CONTAINER_NAME" \
  --image "$FULL_IMAGE" \
  --cpu 1 \
  --memory 0.5 \
  --restart-policy Always \
  --os-type Linux \
  --assign-identity \
  --registry-login-server "$ACR_LOGIN_SERVER" \
  --registry-username "$ACR_USERNAME" \
  --registry-password "$ACR_PASSWORD" \
  --secure-environment-variables \
    KeyVault__VaultUri="$KEY_VAULT_URI" \
    Discord__GuildId="$DISCORD_GUILD_ID" \
    ConnectionStrings__CommendBotDb="$CONNECTION_STRING" \
  --output none

# --------------- grant Key Vault access to ACI managed identity ---------------
echo "==> Granting Key Vault access to container's managed identity..."
IDENTITY_PRINCIPAL_ID=$(az container show \
  --resource-group "$RESOURCE_GROUP" \
  --name "$CONTAINER_NAME" \
  --query identity.principalId \
  --output tsv)

az keyvault set-policy \
  --name "$KEY_VAULT_NAME" \
  --object-id "$IDENTITY_PRINCIPAL_ID" \
  --secret-permissions get list \
  --output none

echo ""
echo "==> Deployment complete!"
echo "    Container:  ${CONTAINER_NAME}"
echo "    Image:      ${FULL_IMAGE}"
echo "    Key Vault:  ${KEY_VAULT_URI}"
echo ""
echo "    The Discord token is stored in Key Vault and accessed via managed identity."
echo "    No token is passed as an environment variable to the container."
echo ""
echo "NOTE: The container may need to be restarted after the Key Vault policy is applied:"
echo "  az container restart --resource-group ${RESOURCE_GROUP} --name ${CONTAINER_NAME}"
echo ""
echo "View logs with:"
echo "  az container logs --resource-group ${RESOURCE_GROUP} --name ${CONTAINER_NAME} --follow"
