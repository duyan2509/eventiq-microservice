# =============================================================================
# 02-build-push.ps1 — build every service image in ACR (cloud build, no local
# Docker daemon needed) and tag it $IMAGE_TAG.
#
# .NET services build from the repo root context (their Dockerfiles do
# `COPY . .`). AnalyticsService builds from its own folder.
# =============================================================================
$ErrorActionPreference = "Stop"
. "$PSScriptRoot\config.ps1"

az account set --subscription $SUBSCRIPTION

# name = ACR repository / future container app name ; dockerfile + context relative to $REPO_ROOT
$dotnet = @(
  @{ name = "api-gateway";       dir = "Eventiq.ApiGateway"          },
  @{ name = "user-service";      dir = "Eventiq.UserService"         },
  @{ name = "org-service";       dir = "Eventiq.OrganizationService" },
  @{ name = "event-service";     dir = "Eventiq.EventService"        },
  @{ name = "seat-service";      dir = "Eventiq.SeatService"         },
  @{ name = "payment-service";   dir = "Eventiq.PaymentService"      },
  @{ name = "email-service";     dir = "Eventiq.EmailService"        }
)

Push-Location $REPO_ROOT
try {
  foreach ($s in $dotnet) {
    Write-Host "==> az acr build  $($s.name)" -ForegroundColor Cyan
    az acr build --registry $ACR `
      --image "$($s.name):$IMAGE_TAG" `
      --file "$($s.dir)/Dockerfile" `
      . --only-show-errors
  }

  Write-Host "==> az acr build  analytics-service (Python)" -ForegroundColor Cyan
  az acr build --registry $ACR `
    --image "analytics-service:$IMAGE_TAG" `
    --file "Eventiq.AnalyticsService/Dockerfile" `
    "Eventiq.AnalyticsService" --only-show-errors
}
finally { Pop-Location }

Write-Host "All images built and pushed to $ACR_LOGIN" -ForegroundColor Green
