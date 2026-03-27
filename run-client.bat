@echo off
cd /d "%~dp0"
dotnet run --project ".\RemoteDesktop.Client.Cross\RemoteDesktop.Client.Cross.csproj"
pause
