@echo off
cd /d "%~dp0"
dotnet run --project ".\RemoteDesktop.Host\RemoteDesktop.Host.csproj" -- 5050 8 40
pause
