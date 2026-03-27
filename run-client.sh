#!/usr/bin/env bash
set -e
cd "$(dirname "$0")"
dotnet run --project "./RemoteDesktop.Client.Cross/RemoteDesktop.Client.Cross.csproj"
