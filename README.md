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

Host argumanlari:

```text
<port> <fps> <jpegKalite> [gray|gri|1]
```

| Arguman | Ornek | Aciklama |
|---------|--------|----------|
| port | `5050` | TCP dinleme portu |
| fps | `8` | Saniyede yaklasik kare sayisi (ic hesap: min ~15 ms kare araligi) |
| jpegKalite | `30` | JPEG encoder kalitesi **25–90** arasi (dusuk = daha kucuk dosya, daha cok artefakt) |
| (istege bagli) | `gray` | Gri tonlama — asagida |

Varsayilan renkli ornek: `5050 8 30`.

## Gri tonlama (istege bagli) — mantik

**Ne zaman:** Dorduncu arguman **`gray`**, **`gri`** veya **`1`** verildiginde (buyuk/kucuk harf `gray` / `gri` icin fark etmez).

**Nerede:** Sadece **Windows host** (`RemoteDesktop.Host`). Client degisikligi gerekmez; agdan gelen kare zaten gri tonlu JPEG olarak cozulur.

**Akis (her kare):**

1. Ekran **24 bpp RGB** bitmap olarak yakalanir (`CopyFromScreen`) — henuz renkli.
2. Gri mod aciksa, bu bitmap **JPEG’e yazilmadan once** `LockBits` ile piksel piksel islenir. `Format24bppRgb` bellekte **BGR** sirasidir (once Mavi, Yesil, Kirmizi bayt); her pikselde **Y ≈ 0,299·R + 0,587·G + 0,114·B** hesaplanir ve **uc bayta da ayni Y** yazilir (gri).  
   *(Eski `ColorMatrix` + `DrawImage` yolu bazi surumlerde kanal sirasi yuzunden sari sapma verebiliyordu; dogrudan BGR okuma bunu giderir.)*
3. Sonra her zamanki gibi **JPEG** sikistirilir ve TCP ile `Frame` paketi olarak gonderilir.

**Neden:** Renk bilgisi (uc kanal arasindaki fark) kalktigi icin cogu sahnede **JPEG dosyasi bir miktar kuculebilir**; dar bant (hotspot, kalabalik Wi-Fi) icin **deneysel** bir secenek. Kazanc **icerige baglidir**; garanti sabit yuzde yok.

**Konsol:** Host, JPEG kalite / PSNR satirinda modu **`[GRI]`** veya **`[RENK]`** ile gosterir.

**Ornek komut:**

```powershell
dotnet run --project .\RemoteDesktop.Host\RemoteDesktop.Host.csproj -- 5050 8 30 gray
```

## Notlar

- Uzaktan klavye/fare enjeksiyonu **Windows host** ile hedeflenir.
- macOS’ta host calistirilirsa ekran icin `screencapture` ve sistem izinleri gerekebilir.
