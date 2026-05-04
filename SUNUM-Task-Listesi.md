# Task ozeti — Remote Desktop (LAN) projesi

## Task ve thread pool (kisaca)

**`Task`**, isin *her zaman* yeni bir is parcacigi acmak demek degildir. Cogu `async`/`await` isinde kod **thread pool** uzerinde veya I/O tamamlaninca **havuzdaki** bir is parcaciginde kisa sure calisir; beklerken is parcacigi baska ise doner: **havuzda is var al — kullan — birak** mantigina yakin dusunulabilir (ogretim dili; teknik olarak I/O beklemede thread baglanmayabilir).

---

## Surekli calisan (yasam sureleri baglantiya bagli) Task mantigi

| # | Nerede | Ne | Not |
|---|--------|-----|-----|
| 1 | **Host** — `SendFramesLoopAsync` | Baglanti acikken dongu: ekran yakala → `Frame` yaz → `Delay` | Oturum basina **surekli**; biri iptal olana kadar doner. |
| 2 | **Host** — `ReceiveInputLoopAsync` | Baglanti acikken dongu: paket oku → `HandleInputPacket` | Oturum basina **surekli**; gonderici ile **ayni anda** ilerler. |
| 3 | **Client** — `ReceiveLoopAsync` | Baglanti acikken dongu: `ReadPacketAsync` → decode → UI post | **Tek ana alim** hatti; baglanti kopana kadar surer. |

**Host’ta, bir client bagliyken “paralel surekli” sayisi:** **2 Task** (gorsel gonderen + input alan).  
**Client’ta, bagliyken “paralel surekli” sayisi:** **1 ana async dongu** (alim); gorsel gonderim yok (client host’a sadece input yollar).

---

## Anlik / kisa omurlu Task’ler (sayi sabit degil)

| Nerede | Ne | Not |
|--------|-----|-----|
| **Client** | `_ = SendPacketAsync(...)` | Fare hareketi, tik, tus, teker her seferinde **ayri kisa** `Task`; ayni anda birden fazla calisabilir ama `_sendLock` ile **yazma sirasi** korunur. |
| **Host** | `AcceptTcpClientAsync` | Her yeni baglantida `await`; ayri “sonsuz gonderici” degil, ana dongunun bir adimi. |

Bunlari “havuzdan al-kullan-at” ile dusun: **uzun sure bloklayan** bir `Task` yoksa is parcacigi cabuk serbest kalir.

---

## Paralellik — toplamda ayni anda kac Task?

- **Pratik cevap (sunum):** Host’ta bagli oturumda **en az 2 Task ayni anda ilerler** (video dongusu + input dongusu). Client’ta **en az 1 surekli alim** vardir; uzerine **0…cok** kisa gonderim `Task`’i eklenebilir.
- **Minimum:** Hic client yokken host sadece **accept** bekler (tek `await` zinciri; “iki sonsuz dongu” o an yok). Client bagli degilken **ReceiveLoopAsync** calismaz.
- **Maksimum (sabit tavan yok):** Client’ta ayni anda kac `SendPacketAsync` Task’i ucar — olaya bagli; **tavan sabit degil**, ama **surekli tasarlanmis** olanlar host’ta **2**.

---

## Tek cumle (slayt)

“Host’ta bir baglantida **iki surekli `Task`**: biri **Frame** yollar, biri **input** okur; `Task`/`async` isleri cogu zaman **thread pool** ve I/O ile verimli calisir. Client’ta **bir surekli alim dongusu** vardir; kontrol paketleri **kisa omurlu gonderim Task**’leriyle host’a yazilir.”
