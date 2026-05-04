# Remote Desktop (LAN) — 10 dakikalik sunum notu

**Odak:** Paralel isleme (`Task` / `async`) ve eszamanlilik (`Interlocked`, `SemaphoreSlim`, UI thread).  
**Not:** Asagidaki UML ve tablolar ayri dosyalarda (`SUNUM-*.md`); slaytta onlari goster, bu metin **konusma omurgasi**.

---

## 0) Sure dagilimi (yaklasik)

| Boluk | Sure | Icerik |
|--------|------|--------|
| Giris | ~1 dk | Problem, cozum, demo ortami (Windows host + Mac client, LAN) |
| Mimari | ~1,5 dk | 3 proje: Core / Host / Client |
| **Paralel isleme** | **~3,5 dk** | Host 2 Task; Client async + kilitleme; kisa kod |
| Ag + protokol | ~1 dk | TCP, tek soket, paket tipi |
| Kalite / FPS | ~1 dk | JPEG, bant–kalite; konsol PSNR (istege bagli goster) |
| Demo | ~1,5 dk | Baglan, hareket, konsol satiri |
| Kapanis | ~30 sn | Ogrenilenler, sinirlar |

---

## 1) Giris — ne yapiyoruz?

- **Problem:** Ayni yerel agda bir bilgisayarin ekranini uzaktan gormek ve fare/klavye ile kontrol etmek.
- **Cozum:** **Host** ekrani JPEG ile **TCP** uzerinden yollar; **Client** goruntuyu acar ve girdileri geri yollar.
- **Ders baglantisi:** Ayni baglantida **video** ve **kontrol** ayni anda akmali; bu yuzden **paralel `Task`** ve client tarafinda **guvenli paylasim** (`Interlocked`, `SemaphoreSlim`) var.

*(Slayt: basit mimari diyagram — `SUNUM-Mermaid-Gorsel.md` Diyagram 1.)*

---

## 2) Mimari — uc parca

1. **`RemoteDesktop.Core`** — Ortak protokol: paket = **1 bayt tip + 4 bayt uzunluk + payload** (`RemoteProtocol.WritePacketAsync` / `ReadPacketAsync`).
2. **`RemoteDesktop.Host`** — Windows konsol: dinle, yakalama, **iki paralel dongu** (asagida).
3. **`RemoteDesktop.Client.Cross`** — Avalonia arayuz: baglan, **goruntu al**, **input gonder**.

*(Slayt: `SUNUM-RemoteDesktop-Core.md` / Host / Client UML — detay icin.)*

---

## 3) Paralel isleme (ANA BOLUM)

### 3.1 Host — ayni TCP oturumunda iki yon

**Amaç:** Ekran gonderirken **beklemeden** client’tan gelen fare/klavye paketlerini de okuyabilmek.

- **`SendFramesLoopAsync`:** Dongu — ekran yakala → `Frame` paketi yaz → `Task.Delay` (FPS).
- **`ReceiveInputLoopAsync`:** Dongu — `ReadPacketAsync` → `HandleInputPacket` → Windows `SendInput`.

Ikisi **ayni `NetworkStream`** uzerinde; **ayni anda** calisan iki `Task`. Biri bittiginde veya hata olunca `CancellationTokenSource` ile digeri iptal edilir.

**Kisa kod ornegi (Host):**

```csharp
Task senderTask = SendFramesLoopAsync(stream, frameDelayMs, quality, clientCts.Token);
Task receiverTask = ReceiveInputLoopAsync(stream, clientCts.Token);
Task completed = await Task.WhenAny(senderTask, receiverTask);
clientCts.Cancel();
await completed;
```

**Konusma cumlesi:** “`Thread` iki tane acip `Join` demek yerine, **async donguler** ile I/O’yu bloklamadan **ikili paralel akis** kurduk.”

---

### 3.2 Client — alim + gonderim cakismasi

**Amaç:** Bir yandan **surekli Frame oku**; diger yandan **fare/klavye** ile sik paket yaz; **UI** kuralina uy.

- **`ReceiveLoopAsync`:** Tek **async** alim dongusu; `await ReadPacketAsync`.
- **Goruntu:** `Dispatcher.UIThread.Post` ile `ScreenImage` — **UI thread** uzerinde cizim.
- **`Interlocked`:** `_renderBusy` — ayni anda iki kare decode/UI cakismasin; `_lastMouseMoveAt` — fare spam azaltma.
- **`SemaphoreSlim(1,1)`:** `SendPacketAsync` — **await** ile uyumlu **yazma kilidi** (`lock` + `await` kacinilir).

**Kisa kod ornegi (Client — atomik bayrak):**

```csharp
if (Interlocked.Exchange(ref _renderBusy, 1) == 1)
    continue; // baska kare isleniyor, bunu atla
// ... decode ...
Dispatcher.UIThread.Post(() => {
    try { ScreenImage.Source = bitmap; }
    finally { Interlocked.Exchange(ref _renderBusy, 0); }
});
```

**Konusma cumlesi:** “**`Interlocked`** kucuk paylasilan sayaclar icin; **`SemaphoreSlim`** async gonderim sirasi icin.”

---

### 3.3 Ozet rakamlar (slayt tablosu ile uyumlu)

- Host (bagli oturumda): **2 surekli paralel `Task`** (video + input).
- Client: **1 surekli alim** + cok sayida **kisa** gonderim `Task`’i.
- Kodda **`new Thread()`** yok; isler **`Task` / thread pool** ve **UI thread** ile yurur.

*(Slayt: `SUNUM-Task-Listesi.md`, `SUNUM-Genel-Mantik-Tablo.md`.)*

---

## 4) Ag ve protokol (kisaca)

- **TCP**, ayni LAN (or. telefon **hotspot**); **5050** varsayilan port.
- **Tek akis:** Uzerinde hem **JPEG Frame** hem **MouseMove / KeyDown / …** paketleri (`PacketType` ile ayrilir).
- **Core** rol: Host ve client **aynı dil**i paylasir — tekrar yazilmaz.

*(Istersen 1 slayt: wire format — daha once hazirladigin hex ornek tablo.)*

---

## 5) JPEG, FPS, bant (kisaca)

- **Kalite 25–90:** Dusuk = kucuk dosya, daha cok artefakt; yuksek = tersi.
- **FPS:** `frameDelayMs = max(15, 1000/fps)` → pratik **ust ~66 FPS** tavan; sunumda genelde **8–15** yeterli.
- **Bant:** Kabaca **FPS × kare boyutu**; hotspot’ta **Q=30** gibi dusuk kalite **guvenli taraf**.
- **Istege bagli:** Host konsolunda **PSNR/MSE** satiri — “JPEG kaybini sayisal gosterdik” de.

*(Slayt: `SUNUM-JPEG-Metrik-Host.md` — agresiflik gostergesi ≠ gercek % kayip; PSNR/MSE ana olcum.)*

---

## 6) Demo (1–2 dk)

1. Windows: `dotnet run --project .\RemoteDesktop.Host\RemoteDesktop.Host.csproj -- 5050 8 30`
2. Mac: `dotnet run --project ./RemoteDesktop.Client.Cross/RemoteDesktop.Client.Cross.csproj`
3. `ipconfig` → IP + **5050** → Connect.
4. Pencere ac/kapat, fare; host konsolda **PSNR satirinin** degistigini goster (istege bagli).

---

## 7) Kapanis

- **Basari:** LAN uzaktan masaustu + **paralel** host donguleri + client’ta **guvenli paylasim**.
- **Sinir:** Guvenlik (sifreleme yok), tek istemci odagi, JPEG + TCP kuyruk davranisi; gelistirme: TLS, UDP video, coklu client.

---

## Hoca sorarsa — tek cumlelik cevaplar

- **Neden TCP?** Sirali, guvenilir; basit prototip.  
- **Neden iki Task host’ta?** Video dongusu input okumasini bloklamasin.  
- **Interlocked neden lock degil?** Tek int bayrak icin atomik ve ucuz.  
- **5050 ne?** Port; JPEG ile ilgisi yok.

---

## Dosya referanslari (slayt malzemesi)

| Dosya | Icerik |
|--------|--------|
| `SUNUM-Mermaid-Gorsel.md` | Genel akis diyagramlari |
| `SUNUM-Genel-Mantik-Tablo.md` | Modul + thread/Task tablosu |
| `SUNUM-Task-Listesi.md` | Task listesi + thread pool notu |
| `SUNUM-RemoteDesktop-Core.md` | Core UML + metod listesi |
| `SUNUM-RemoteDesktop-Host.md` | Host UML + notlar |
| `SUNUM-RemoteDesktop-Client.md` | Client UML + notlar |
| `SUNUM-JPEG-Metrik-Host.md` | Konsol PSNR aciklamasi |
| `README.md` | Calistirma komutlari |

Bu notu **yazdir / PDF** veya ikinci monitorden oku; slaytlar **gorsel**, bu metin **zaman ve cumle** rehberi olsun.
