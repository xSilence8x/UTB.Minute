#!/usr/bin/env powershell

# Start AppHost in Testing environment
Write-Host "Starting Aspire AppHost in Testing mode..." -ForegroundColor Cyan
$appHostProcess = Start-Process -FilePath "dotnet" -ArgumentList "run --project UTB.Minute.AppHost/UTB.Minute.AppHost.csproj --environment=Testing" -PassThru -NoNewWindow

# Wait for AppHost to be ready (5 seconds should be enough)
Write-Host "Waiting for services to start..." -ForegroundColor Yellow
Start-Sleep -Seconds 5

# Run tests
Write-Host "Running tests..." -ForegroundColor Green
dotnet test UTB.Minute.WebApi.Tests --logger "console;verbosity=minimal"

# Cleanup
Write-Host "Stopping AppHost..." -ForegroundColor Cyan
Stop-Process -InputObject $appHostProcess -Force -ErrorAction SilentlyContinue

Write-Host "Done!" -ForegroundColor Green
