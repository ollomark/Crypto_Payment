# Local gelistirme icin calistirma scripti
Set-Location $PSScriptRoot

# Dotnet yolunu dene
$dotnet = $null
if (Get-Command dotnet -ErrorAction SilentlyContinue) { $dotnet = "dotnet" }
elseif (Test-Path "C:\Program Files\dotnet\dotnet.exe") { $dotnet = "C:\Program Files\dotnet\dotnet.exe" }
elseif (Test-Path "${env:LOCALAPPDATA}\Microsoft\dotnet\dotnet.exe") { $dotnet = "${env:LOCALAPPDATA}\Microsoft\dotnet\dotnet.exe" }

if (-not $dotnet) {
    Write-Host ""
    Write-Host "HATA: .NET SDK bulunamadi!" -ForegroundColor Red
    Write-Host ""
    Write-Host "Cozum adimlari:" -ForegroundColor Yellow
    Write-Host "1. https://dotnet.microsoft.com/download/dotnet/8.0 adresine gidin"
    Write-Host "2. 'Download .NET SDK x64' butonuna tiklayin"
    Write-Host "3. Kurulumu tamamlayin"
    Write-Host "4. Terminal/PowerShell'i KAPATIP yeniden acin"
    Write-Host "5. Bu scripti tekrar calistirin"
    Write-Host ""
    Write-Host "Veya Visual Studio 2022 kullaniyorsaniz: Projeyi acip F5 ile calistirabilirsiniz." -ForegroundColor Cyan
    Read-Host "Cikmak icin Enter'a basin"
    exit 1
}

Write-Host "Uygulama baslatiliyor: http://localhost:5000" -ForegroundColor Green
Write-Host "Durdurmak icin Ctrl+C basin." -ForegroundColor Yellow
& $dotnet run --urls "http://localhost:5000"
