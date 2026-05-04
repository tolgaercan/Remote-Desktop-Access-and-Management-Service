# Mermaid Live — gorsel sunum

1. Ac: [https://mermaid.live](https://mermaid.live)  
2. Asagidaki kodu **Code** paneline yapistir.  
3. **Actions → PNG / SVG** ile slayta ekle.

---

## Diyagram 1 — Genel mimari (moduller + TCP)

```mermaid
flowchart TB
  subgraph CORE["RemoteDesktop.Core"]
    RP[RemoteProtocol + PacketType]
  end

  subgraph HOST["Host — Windows"]
    L[TcpListener port 5050]
    T1[Task: Frame gonder]
    T2[Task: Input oku]
    W[SendInput — OS]
    L --> T1
    L --> T2
    T2 --> W
  end

  subgraph CLIENT["Client — Avalonia"]
    UI[UI thread — pencere]
    R[Task: ReceiveLoop — Frame al]
    S[Kisa Task: SendPacket — fare/klavye]
    UI <--> R
    S --> UI
  end

  HOST <-->|"TCP ayni LAN"| CLIENT
  HOST --> RP
  CLIENT --> RP
```

---

## Diyagram 2 — Baglanti sonrasi akis (adim adim)

```mermaid
flowchart LR
  A[1 Host dinler] --> B[2 Client baglanir]
  B --> C[3 Iki yonlu TCP]
  C --> D[4 Host: Frame dongusu]
  C --> E[5 Host: Input dongusu]
  C --> F[6 Client: Frame al ve goster]
  C --> G[7 Client: Kontrol gonder]
  D --> H[(Ayni soket)]
  E --> H
  F --> H
  G --> H
```

---

## Diyagram 3 — Host icinde paralel 2 Task

```mermaid
flowchart TB
  NS[NetworkStream]

  subgraph PAR["Ayni anda"]
    direction TB
    LOOP1["SendFramesLoopAsync<br/>yakala - JPEG - Write Frame<br/>Task.Delay FPS"]
    LOOP2["ReceiveInputLoopAsync<br/>ReadPacket - SendInput OS"]
  end

  NS --> LOOP1
  NS --> LOOP2
```

---

## Diyagram 4 — Client: UI thread + alim

```mermaid
flowchart LR
  subgraph POOL["Thread pool async"]
    RX[ReceiveLoopAsync<br/>ReadPacket + decode]
  end

  subgraph UI_TH["UI thread"]
    WIN[MainWindow + ScreenImage]
  end

  RX -->|"Dispatcher.Post"| WIN
  EVT[fare / klavye olayi] --> WIN
  WIN -->|"SendPacketAsync"| POOL
```

---

## Diyagram 5 — Thread / Task ozet kutular

```mermaid
flowchart TB
  subgraph NOT["Sunum notu"]
    N1["Kodda new Thread yok"]
    N2["Host: 2 surekli Task / oturum"]
    N3["Client: 1 surekli alim + cok kisa gonderim"]
    N4["UI thread: sadece clientta sabit anlamda"]
  end
```

---

## Tek diyagramda hepsi (sade slayt — bir sayfa)

Daha kalabalik; slayt buyukse kullan.

```mermaid
flowchart TB
  CORE["CORE: ortak paket formati"]

  subgraph H["HOST"]
    direction TB
    H1[TcpListener]
    H2[Task Frame]
    H3[Task Input]
    H1 --> H2
    H1 --> H3
  end

  subgraph C["CLIENT"]
    direction TB
    C1[UI thread]
    C2[ReceiveLoop]
    C1 <--> C2
    C3[fare klavye]
    C3 --> C1
  end

  H <-->|TCP| C
  H --> CORE
  C --> CORE
```
