# Genel mantik tablosu (+ Task ve thread notu)

Projede **`new Thread()` ile adlandirilmis sabit is parcacigi acilmiyor**; **`async`/`Task`** isleri cogu zaman **thread pool** ve tamamlanma geri cagrimlariyla yurur. **“Sabit thread”** asagida **platformun zorunlu** verdigi anlamda kullanilir (ozellikle client UI).

---

## Ana tablo

| Bilesen | Rol (kisaca) | Surekli calisan mantik | Task / paralellik | Thread notu |
|---------|----------------|-------------------------|-------------------|---------------|
| **RemoteDesktop.Core** | Paket formati: tip + uzunluk + payload; `Read`/`Write` | — (kutuphane; kendi dongusu yok) | Cagiranin `Task` zinciri | Kendi thread’i yok |
| **Host `Program.cs`** | Portta dinle; client kabul; video + input | Baglantida **2** sonsuz `async` dongu | `SendFramesLoopAsync` **\|\|** `ReceiveInputLoopAsync` | **Konsol ana akisi:** `while` + `AcceptTcpClientAsync`. Dongulerin `await` devamlari **thread pool**’da calisabilir. **Ayrica acilmis sabit 2 thread yok.** |
| **Client `MainWindow`** | TCP baglan; `Frame` al, goster; fare/klavye gonder | Baglantida **1** ana `ReceiveLoopAsync` dongusu | Uzerine cok sayida **kisa** `SendPacketAsync` `Task`’i | **UI thread (1):** pencere, `ScreenImage`, tus/fare olaylari; `Dispatcher.UIThread.Post` ile goruntu atamasi buraya. Alim/yazim `await` devamlari **thread pool**’da ilerleyebilir. |
| **Client `Program` / `App`** | Avalonia yasam dongusu | Uygulama acikken framework yonetir | Framework `Task`’leri | UI thread + framework thread’leri (.NET/Avalonia yonetir) |

---

## “Sabit thread” ozet (sunum cumlesi)

| Yer | Sabit dedigimiz sey | Aciklama |
|-----|----------------------|----------|
| **Client** | **UI thread** | Kullanici arayuzu tek is parcaciginde calisir; uzun is bloklamamak icin ag okumasi `async` ile thread pool’a kayar, goruntu guncellemesi UI’ya **Post** edilir. |
| **Host** | **Ana / konsol thread** (baslangic) | Ilk satirlar ve `while (true)` accept dongusu burada ilerler; `await` sonrasi devamlar havuzda olabilir. |
| **Her iki taraf** | **Thread pool** (sistem) | Sabit sayida *bizim* thread yok; .NET is yukune gore havuzdan **atanir ve geri verilir**. |

---

## Baglanti anindaki “kim ne yapar” (tek satir)

| Adim | Kim | Ne |
|------|-----|-----|
| 1 | Host | Portta TCP **dinler** (`TcpListener`). |
| 2 | Client | Host **IP + port** ile **baglanir**. |
| 3 | Host | **Task A:** FPS/kalite ile `Frame` **yollar**; **Task B:** client **input** paketlerini **okur** ve OS’e uygular. |
| 4 | Client | **Alim dongusu:** `Frame` **alir**, cozer, **UI thread**’e goruntu verir; **gonderim:** fare/klavye paketlerini yazar. |

---

## Rakamlar (slayt alti)

- **Host, bir oturumda tasarlanmis paralel surekli `Task`:** **2**  
- **Client, bir oturumda tasarlanmis surekli alim:** **1** `ReceiveLoopAsync`  
- **Kodda acikca olusturulan sabit worker thread sayisi:** **0** (UI thread haric; o da framework ile gelir)
