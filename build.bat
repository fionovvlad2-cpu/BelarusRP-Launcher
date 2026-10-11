@echo off
REM Локальная сборка BelarusRP.exe. Нужен .NET 8 SDK: https://dotnet.microsoft.com/download/dotnet/8.0
where dotnet >nul 2>nul
if errorlevel 1 (
  echo [ERROR] .NET 8 SDK not found.
  pause
  exit /b 1
)

dotnet publish "%~dp0src\BelarusRP.Launcher\BelarusRP.Launcher.csproj" ^
  -c Release -r win-x64 --self-contained true ^
  -p:PublishSingleFile=true ^
  -p:IncludeNativeLibrariesForSelfExtract=true ^
  -p:EnableCompressionInSingleFile=true ^
  -o "%~dp0publish"

if errorlevel 1 (
  echo [ERROR] Build failed.
  pause
  exit /b 1
)
echo.
echo Done: %~dp0publish\BelarusRP.exe
pause
