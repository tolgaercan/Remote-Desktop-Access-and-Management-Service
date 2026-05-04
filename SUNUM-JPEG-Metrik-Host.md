# Host konsolunda JPEG metrikleri (PSNR / MSE)

## Ne yapiyor?

Windows host her karede:

1. Ekrani **24 bpp RGB** bitmap olarak alir.
2. Ayni bitmap’i secilen **JPEG kalite** ile kodlar.
3. JPEG’i **tekrar cozer** (agdan gitmeden once, olcum icin).
4. Ham piksel ile decode piksel arasinda **MSE** ve **PSNR** (dB) hesaplar.

**PSNR:** Yuksek dB = daha az fark (genelde daha iyi). Dusuk dB = daha cok fark.

## Nerede gorunur?

**Host konsol penceresinin en altinda** tek satir; her yeni karede `\r` ile **uzerine yazilir** (dinamik guncelleme).

Ornek satir anlami:

- `JPEG Q=30` — encoder kalite parametresi  
- `(30% olcek)` — `quality/100`  
- `PSNR=... dB` — olculen bildirim benzerligi  
- `MSE=...` — ortalama kare hata  
- `JPEG x KB / ham y KB` — dosya boyutu vs ham RGB  
- `sikistirma ~Nx` — `ham / JPEG` orani  
- `agresiflik gostergesi` — `(100-Q)/100` (sunumda acikladigimiz turev gosterge)

## Sinirlar

- Olcum **encode-decode dongusu** icindir; agda baska bozulma yok varsayilir.
- **CPU:** Her karede ek decode + karsilastirma; dusuk FPS’te genelde kabul edilir.

## macOS host

Bu metrikler **yalnizca Windows** host yolunda calisir (`CaptureWindowsFrameWithMetrics`).
