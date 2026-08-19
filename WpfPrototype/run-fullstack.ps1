$ErrorActionPreference = 'Stop'

# Compatibility launcher: the application now runs as one WPF process.
$workspace = Split-Path -Parent $MyInvocation.MyCommand.Path
$wpfProject = Join-Path $workspace 'Cmie.MotorTest.Wpf\Cmie.MotorTest.Wpf.csproj'

Write-Host 'CMIE now uses integrated local storage; starting WPF...' -ForegroundColor Green
dotnet run --project $wpfProject --no-launch-profile
