# Remote Desktop Access and Management Service (LAN)

Bu repo, minimum bagimlilik ile C# kullanarak LAN uzerinden uzaktan masaustu akisinin temelini kurar.

## Proje Yapisı

- `RemoteDesktop.Core`: Basit paket protokolu (frame gonder/al)
- `RemoteDesktop.Host`: Ekrani yakalayan ve TCP ile gonderen host
- `RemoteDesktop.Client.Cross`: Cross-platform Avalonia client (Windows/macOS, goruntu + mouse/klavye)

## Calistirma

1. Host:
   - `dotnet run --project .\RemoteDesktop.Host\RemoteDesktop.Host.csproj -- 5050 12 55`
2. Client:
   - `dotnet run --project .\RemoteDesktop.Client.Cross\RemoteDesktop.Client.Cross.csproj`
   - Host IP ve port gir, `Connect` tikla

Argumanlar:
- `port` varsayilan: `5050`
- `fps` varsayilan: `12`
- `jpegQuality` varsayilan: `55` (25-90 arasi)

Gecikme azaltma oneri (ozellikle hotspot/LAN sunumu):
- `dotnet run --project .\RemoteDesktop.Host\RemoteDesktop.Host.csproj -- 5050 8 40`
- Daha dusuk FPS + daha dusuk JPEG kalite = daha az gecikme

## Input Kontrolu (Mouse + Klavye)

- Client penceresinde once `Connect` yap.
- Goruntu alani uzerinde:
  - Mouse hareketi ve tiklari host'a gider.
  - Uygulama odaklandiginda klavye tuslari host'a gider.
- Bu adimda input enjeksiyonu Windows host icin aktiftir.

## Self-contained Publish (Tum bagimliliklar uygulama icinde)

Windows host + cross client:

- Host:
  - `dotnet publish .\RemoteDesktop.Host\RemoteDesktop.Host.csproj -c Release -r win-x64`
- Cross Client (Windows):
  - `dotnet publish .\RemoteDesktop.Client.Cross\RemoteDesktop.Client.Cross.csproj -c Release -r win-x64`
- Cross Client (macOS Apple Silicon):
  - `dotnet publish .\RemoteDesktop.Client.Cross\RemoteDesktop.Client.Cross.csproj -c Release -r osx-arm64`
- Cross Client (macOS Intel):
  - `dotnet publish .\RemoteDesktop.Client.Cross\RemoteDesktop.Client.Cross.csproj -c Release -r osx-x64`

Bu publish ciktilari .NET kurulumu olmadan da calisabilir.

## macOS Notu

Host uygulamasi macOS uzerinde de calisir. macOS tarafinda ekran yakalama icin sistemin `screencapture` araci kullanilir.

Ilk calistirmada macOS izinleri gerekir:
- Screen Recording izni
- Gerekirse Terminal/uygulama icin Accessibility izni (input adiminda)

Basit senaryo:
- Host: Windows
- Client: macOS (`RemoteDesktop.Client.Cross`)