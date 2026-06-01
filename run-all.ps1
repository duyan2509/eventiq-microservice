Write-Host "Stopping any running dotnet processes..." -ForegroundColor Yellow
Get-Process dotnet -ErrorAction SilentlyContinue | Stop-Process -Force
Start-Sleep -Seconds 1

$services = @(
    @{ Name = "UserService";         Path = "Eventiq.UserService";         Port = 5228 },
    @{ Name = "OrganizationService"; Path = "Eventiq.OrganizationService"; Port = 5230 },
    @{ Name = "EventService";        Path = "Eventiq.EventService";        Port = 5232 },
    @{ Name = "SeatService";         Path = "Eventiq.SeatService";         Port = 5234 },
    @{ Name = "PaymentService";      Path = "Eventiq.PaymentService";      Port = 5236 },
    @{ Name = "ApiGateway";          Path = "Eventiq.ApiGateway";          Port = 5001 }
)

$root = $PSScriptRoot

foreach ($svc in $services) {
    $svcPath = Join-Path $root $svc.Path
    Write-Host "Starting $($svc.Name) on :$($svc.Port)..." -ForegroundColor Cyan
    Start-Process powershell -ArgumentList "-NoExit", "-Command", "cd '$svcPath'; dotnet run --launch-profile http"
    Start-Sleep -Seconds 2
}

# Start Python AnalyticsService (Text2SQL pipeline) on :5238
$analyticsPath = Join-Path $root "Eventiq.AnalyticsService"
$venvPy = Join-Path $analyticsPath ".venv\Scripts\python.exe"
if (Test-Path $venvPy) {
    Write-Host "Starting AnalyticsService on :5238..." -ForegroundColor Cyan
    Start-Process powershell -ArgumentList "-NoExit", "-Command", "cd '$analyticsPath'; & '$venvPy' -m uvicorn src.api:app --host 0.0.0.0 --port 5238"
} else {
    Write-Host "Skipped AnalyticsService (.venv not found)" -ForegroundColor Yellow
}

Write-Host ""
Write-Host "All services started." -ForegroundColor Green
Write-Host "  ApiGateway        -> http://localhost:5001"
Write-Host "  UserService       -> http://localhost:5228"
Write-Host "  OrgService        -> http://localhost:5230"
Write-Host "  EventService      -> http://localhost:5232"
Write-Host "  SeatService       -> http://localhost:5234"
Write-Host "  PaymentService    -> http://localhost:5236"
Write-Host "  AnalyticsService  -> http://localhost:5238"
