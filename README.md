# Remote Desktop (LAN)

C# ile ayni ag uzerinden ekran paylasimi ve uzaktan giris.

| Proje | Rol |
|--------|-----|
| `RemoteDesktop.Core` | TCP paket protokolu |
| `RemoteDesktop.Host` | Ekran + giris (Windows’ta tam destek) |
| `RemoteDesktop.Client.Cross` | Istemci (Avalonia, Windows / macOS) |

## Kurulum

1. [.NET SDK](https://dotnet.microsoft.com/download) yukle (10.x uyumlu).
2. Repoyu klonla / indir, **proje kokunde** (`Remote-Desktop-Access-and-Management-Service`):

```bash
dotnet restore RemoteDesktopLan.slnx
```

*(Host acikken `dotnet build` DLL kilidi verebilir; host’u kapatip tekrar dene.)*

## Windows host + Mac client

Her iki makinede de repoyu ayni yol mantigiyla ac; komutlari **proje kokunden** calistir.

**Windows — Host (PowerShell veya CMD):** once repoyu indir, **klasor adi ne olursa olsun** icinde `RemoteDesktop.Host` klasorunun oldugu kok dizine `cd` yap:

```powershell
cd C:\Yol\Remote-Desktop-Access-and-Management-Service
dotnet run --project .\RemoteDesktop.Host\RemoteDesktop.Host.csproj -- 5050 8 30
```

Ikinci satir host’u baslatir; konsolda `Host starting on port 5050...` ve `Waiting for client...` gorunmeli.

Wifi / telefon hotspot gibi dar kanallarda JPEG **25–35** araligini dene (dusuk = daha kucuk kare, daha az kuyruk gecikmesi). Client tarafinda sokette biriken eski kareler atilir, **en son kare** gosterilir.

**macOS — Client (Terminal):**

```bash
dotnet run --project ./RemoteDesktop.Client.Cross/RemoteDesktop.Client.Cross.csproj
```

## Baglanti

1. Windows’ta `ipconfig` ile **IPv4** adresini al.
2. Mac client’ta bu IP + port **5050** yaz, **Connect**.

Host argumanlari: `<port> <fps> <jpegKalite>` — varsayilan ornek: `5050 8 30`.

## Notlar

- Uzaktan klavye/fare enjeksiyonu **Windows host** ile hedeflenir.
- macOS’ta host calistirilirsa ekran icin `screencapture` ve sistem izinleri gerekebilir.
