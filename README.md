# Remote Desktop (LAN)

C# ile ayni ag uzerinden ekran paylasimi ve uzaktan giris.

| Proje | Rol |
|--------|-----|
| `RemoteDesktop.Core` | TCP paket protokolu |
| `RemoteDesktop.Host` | Ekran + giris (Windows’ta tam destek) |
| `RemoteDesktop.Client.Cross` | Istemci (Avalonia, Windows / macOS) |

## Kurulum

1. [.NET SDK](https://dotnet.microsoft.com/download) yukle (10.x uyumlu).
2. Repoyu ac ve bagimliliklari indir:

```powershell
cd C:\Yol\Remote-Desktop-Access-and-Management-Service
dotnet restore RemoteDesktopLan.slnx
dotnet build RemoteDesktopLan.slnx
```

*(Host calisiyorsa `dotnet build` DLL kilidi verebilir; host’u kapatip tekrar dene.)*

## Hizli calistirma (komut satiri)

Proje kokunde (`Remote-Desktop-Access-and-Management-Service`).

### Windows — Host (uzak PC)

PowerShell veya CMD:

```powershell
dotnet run --project .\RemoteDesktop.Host\RemoteDesktop.Host.csproj -- 5050 8 40
```

### Windows — Client

```powershell
dotnet run --project .\RemoteDesktop.Client.Cross\RemoteDesktop.Client.Cross.csproj
```

### macOS / Linux — Client

Terminal:

```bash
cd /path/to/Remote-Desktop-Access-and-Management-Service
dotnet run --project ./RemoteDesktop.Client.Cross/RemoteDesktop.Client.Cross.csproj
```

### Cift tikla (Windows)

- `run-host.bat` — host’u varsayilan ayarlarla baslatir (`5050 8 40`).
- `run-client.bat` — client’i baslatir.

### macOS — script

```bash
chmod +x run-client.sh
./run-client.sh
```

## Baglanti

1. Host’ta `ipconfig` ile **IPv4** adresini al.
2. Client’ta bu IP + port **5050** yaz, **Connect**.

## Argumanlar (Host)

```text
dotnet run --project .\RemoteDesktop.Host\RemoteDesktop.Host.csproj -- <port> <fps> <jpegKalite>
```

Ornek: `5050 8 40` — dusuk gecikme icin iyi baslangic.

## Publish (kurulum gerektirmeyen exe / app)

**Windows (host + client):**

```powershell
dotnet publish .\RemoteDesktop.Host\RemoteDesktop.Host.csproj -c Release -r win-x64
dotnet publish .\RemoteDesktop.Client.Cross\RemoteDesktop.Client.Cross.csproj -c Release -r win-x64
```

Cikti ornekleri:

- Host: `RemoteDesktop.Host\bin\Release\net10.0\win-x64\publish\RemoteDesktop.Host.exe`
- Client: `RemoteDesktop.Client.Cross\bin\Release\net10.0\win-x64\publish\RemoteDesktop.Client.Cross.exe`

**macOS (Apple Silicon):**

```bash
dotnet publish ./RemoteDesktop.Client.Cross/RemoteDesktop.Client.Cross.csproj -c Release -r osx-arm64
```

**macOS (Intel):**

```bash
dotnet publish ./RemoteDesktop.Client.Cross/RemoteDesktop.Client.Cross.csproj -c Release -r osx-x64
```

## Notlar

- Uzaktan klavye/fare enjeksiyonu simdilik **Windows host** ile hedeflenir.
- macOS’ta host kullanilirsa ekran icin `screencapture` ve sistem izinleri gerekebilir.
