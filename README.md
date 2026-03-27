# Remote Desktop Access and Management Service (LAN)

Bu repo, minimum bagimlilik ile C# kullanarak LAN uzerinden uzaktan masaustu akisinin temelini kurar.

## Proje Yapisı

- `RemoteDesktop.Core`: Basit paket protokolu (frame gonder/al)
- `RemoteDesktop.Host`: Ekrani yakalayan ve TCP ile gonderen host
- `RemoteDesktop.Client.Windows`: Windows WinForms client (goruntu alir)

## Calistirma

1. Host:
   - `dotnet run --project .\RemoteDesktop.Host\RemoteDesktop.Host.csproj -- 5050 12`
2. Client:
   - `dotnet run --project .\RemoteDesktop.Client.Windows\RemoteDesktop.Client.Windows.csproj`
   - Host IP ve port gir, `Connect` tikla

Argumanlar:
- `port` varsayilan: `5050`
- `fps` varsayilan: `12`

## Self-contained Publish (Tum bagimliliklar uygulama icinde)

Windows host/client:

- Host:
  - `dotnet publish .\RemoteDesktop.Host\RemoteDesktop.Host.csproj -c Release -r win-x64`
- Client:
  - `dotnet publish .\RemoteDesktop.Client.Windows\RemoteDesktop.Client.Windows.csproj -c Release -r win-x64`

Bu publish ciktilari .NET kurulumu olmadan da calisabilir.

## macOS Notu

Host uygulamasi macOS uzerinde de calisir. macOS tarafinda ekran yakalama icin sistemin `screencapture` araci kullanilir.

Ilk calistirmada macOS izinleri gerekir:
- Screen Recording izni
- Gerekirse Terminal/uygulama icin Accessibility izni (input adiminda)

Basit senaryo:
- Host: macOS
- Client: Windows (`RemoteDesktop.Client.Windows`)