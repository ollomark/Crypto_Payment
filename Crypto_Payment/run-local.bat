@echo off
cd /d "%~dp0"

set DOTNET=
where dotnet >nul 2>&1 && set DOTNET=dotnet
if "%DOTNET%"=="" if exist "C:\Program Files\dotnet\dotnet.exe" set "DOTNET=C:\Program Files\dotnet\dotnet.exe"
if "%DOTNET%"=="" if exist "%LOCALAPPDATA%\Microsoft\dotnet\dotnet.exe" set "DOTNET=%LOCALAPPDATA%\Microsoft\dotnet\dotnet.exe"

if "%DOTNET%"=="" (
    echo.
    echo HATA: .NET SDK bulunamadi!
    echo https://dotnet.microsoft.com/download/dotnet/8.0 adresinden indirin.
    pause
    exit /b 1
)

echo Uygulama baslatiliyor: http://localhost:5000
echo Durdurmak icin Ctrl+C basin.
"%DOTNET%" run --urls "http://localhost:5000"
pause
