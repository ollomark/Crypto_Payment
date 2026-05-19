# Heroku Deploy Rehberi

**App:** cryptopayment-v2  
**URL:** https://cryptopayment-v2-607c8f5c6c2d.herokuapp.com/  
**Hesap:** ridvanakyil16@gmail.com  

---

## Ön Gereksinimler

- Heroku CLI kurulu olmalı: `C:\Program Files\heroku\bin\heroku.cmd`
- Git kurulu olmalı
- `fix` branch'inde olmalısınız

---

## 1. Ortam Değişkenleri

Heroku'da şu environment variable'lar tanımlı:

| Değişken | Değer | Açıklama |
|----------|-------|----------|
| `ASPNETCORE_ENVIRONMENT` | `Production` | .NET ortam ayarı |
| `DATABASE_URL` | (Heroku PostgreSQL tarafından otomatik set edilir) | PostgreSQL bağlantı string'i |
| `Plisio__ApiKey` | (gizli) | Plisio API anahtarı |
| `SMTP_USER` | ridvanakyil16@gmail.com | E-posta gönderimi için |
| `SMTP_FROM` | ridvanakyil16@gmail.com | Gönderen e-posta adresi |
| `SMTP_PASS` | (gizli) | Gmail app password |
| `ALLOWED_ORIGINS` | https://cryptopayment-v2-607c8f5c6c2d.herokuapp.com | CORS ayarı |
| `PROJECT_FILE` | Crypto_Payment/Crypto_Payment.csproj | Build edilecek proje dosyası |

---

## 2. Deploy Komutu (Tek Satır)

PowerShell'de `Crypto_Payment` klasöründe çalıştırın:

```powershell
$env:HEROKU_API_KEY="YOUR_HEROKU_API_KEY"; git push heroku fix:main
```

### Açıklama:
- `$env:HEROKU_API_KEY=...` → Heroku API key'i set eder (MFA aktif olduğu için şifre yerine bu kullanılır)
- `git push heroku fix:main` → Lokal `fix` branch'ini Heroku'nun `main` branch'ine push eder
- Heroku otomatik olarak build + deploy yapar

---

## 3. Adım Adım Deploy Süreci

### Adım 1: Değişiklikleri commit et
```powershell
# Crypto_Payment klasöründe
git add -A
git commit -m "açıklayıcı commit mesajı"
```

### Adım 2: Heroku'ya push et
```powershell
$env:HEROKU_API_KEY="YOUR_HEROKU_API_KEY"
git push heroku fix:main
```

### Adım 3: Deploy'u takip et
Push komutu çalışırken terminalde build logları görünür:
- .NET SDK indirilir (ilk deploy'da)
- `dotnet publish` çalışır
- Uygulama başlatılır

### Adım 4: Kontrol et
```powershell
# Uygulamanın çalıştığını kontrol et
$env:HEROKU_API_KEY="YOUR_HEROKU_API_KEY"
& "C:\Program Files\heroku\bin\heroku.cmd" logs --tail --app cryptopayment-v2
```

---

## 4. Heroku CLI Komutları

Heroku CLI'ı kullanırken her zaman önce API key set edilmeli:

```powershell
$env:HEROKU_API_KEY="YOUR_HEROKU_API_KEY"
```

### Sık Kullanılan Komutlar

```powershell
# Logları görüntüle (son 100 satır)
& "C:\Program Files\heroku\bin\heroku.cmd" logs -n 100 --app cryptopayment-v2

# Canlı log takibi
& "C:\Program Files\heroku\bin\heroku.cmd" logs --tail --app cryptopayment-v2

# Uygulamayı yeniden başlat
& "C:\Program Files\heroku\bin\heroku.cmd" restart --app cryptopayment-v2

# Environment variable ekle/güncelle
& "C:\Program Files\heroku\bin\heroku.cmd" config:set DEGISKEN_ADI="deger" --app cryptopayment-v2

# Environment variable'ları listele
& "C:\Program Files\heroku\bin\heroku.cmd" config --app cryptopayment-v2

# Uygulama bilgisi
& "C:\Program Files\heroku\bin\heroku.cmd" info --app cryptopayment-v2
```

---

## 5. Proje Yapısı (Deploy İçin Önemli Dosyalar)

```
Crypto_Payment/                          ← Git repo kök dizini
├── .deployment                          ← Heroku'ya hangi projeyi build edeceğini söyler
├── Procfile                             ← Heroku'ya uygulamayı nasıl çalıştıracağını söyler
├── Crypto_Payment.sln                   ← Solution dosyası
├── Crypto_Payment/                      ← Ana proje klasörü
│   ├── Crypto_Payment.csproj            ← Proje dosyası
│   ├── Program.cs                       ← Uygulama giriş noktası
│   ├── appsettings.json                 ← Ayarlar (production'da env var kullanılır)
│   ├── wwwroot/                         ← Static dosyalar (CSS, JS, images)
│   ├── Controllers/                     ← API ve MVC controller'lar
│   ├── Views/                           ← Razor view'lar
│   ├── Models/                          ← Entity model'ler
│   ├── DTOS/                            ← Data transfer object'ler
│   ├── Manager/                         ← Business logic
│   └── Helpers/                         ← Yardımcı sınıflar
└── Crypto_Payment.Tests/                ← Test projesi (deploy'a dahil değil)
```

### Procfile İçeriği
```
web: dotnet Crypto_Payment.dll --urls http://+:$PORT
```

### .deployment İçeriği
```
[config]
project = Crypto_Payment/Crypto_Payment.csproj
```

---

## 6. Veritabanı (PostgreSQL)

- Heroku PostgreSQL Essential-0 planı ($5/ay)
- `DATABASE_URL` environment variable'ı Heroku tarafından otomatik set edilir
- `Program.cs`'de `DATABASE_URL` parse edilip Npgsql connection string'e çevrilir
- Migration'lar uygulama başlatılırken otomatik çalışır (`context.Database.Migrate()`)

---

## 7. Git Remote Yapısı

```powershell
# Mevcut remote'ları görmek için (Crypto_Payment klasöründe):
git remote -v

# Çıktı:
# heroku  https://git.heroku.com/cryptopayment-v2.git (fetch)
# heroku  https://git.heroku.com/cryptopayment-v2.git (push)
# origin  https://github.com/ridvanakyil16/Crypto_Payment.git (fetch)
# origin  https://github.com/ridvanakyil16/Crypto_Payment.git (push)
```

- `heroku` remote → Heroku'ya deploy için
- `origin` remote → GitHub repo (push yetkisi `kundurani` kullanıcısında yok, 403 alıyor)

---

## 8. Sorun Giderme

### Build hatası alıyorsam?
```powershell
# Lokalde build test et
dotnet build Crypto_Payment/Crypto_Payment.csproj --verbosity minimal
```

### Uygulama açılmıyorsa?
```powershell
# Logları kontrol et
$env:HEROKU_API_KEY="YOUR_HEROKU_API_KEY"
& "C:\Program Files\heroku\bin\heroku.cmd" logs -n 200 --app cryptopayment-v2
```

### Veritabanı sorunu varsa?
```powershell
# Heroku PostgreSQL bilgisi
& "C:\Program Files\heroku\bin\heroku.cmd" pg:info --app cryptopayment-v2
```

### Deploy geri almak istiyorsam?
```powershell
# Son deploy'ları listele
& "C:\Program Files\heroku\bin\heroku.cmd" releases --app cryptopayment-v2

# Belirli bir release'e geri dön
& "C:\Program Files\heroku\bin\heroku.cmd" rollback v{NUMARA} --app cryptopayment-v2
```

---

## 9. Önemli Notlar

1. **Eski app'e dokunma:** `cryptopayment` (eski) app'i ayrı, ona müdahale etme
2. **API Key:** MFA aktif olduğu için her Heroku CLI komutundan önce `$env:HEROKU_API_KEY` set edilmeli
3. **Branch:** Heroku'ya her zaman `fix` branch'inden deploy ediyoruz (`fix:main`)
4. **Static dosyalar:** `wwwroot/` altındaki dosyalar deploy'a dahil — `.gitignore`'da hariç tutulmamalı
5. **Testler:** Deploy öncesi testleri çalıştır: `dotnet test Crypto_Payment/Crypto_Payment.Tests/Crypto_Payment.Tests.csproj --verbosity minimal`

---

## 10. Yapılan Deploy Geçmişi

| Tarih | Commit | Açıklama |
|-------|--------|----------|
| - | `db0fddd` | Comprehensive bugfix (10 task) |
| - | `c6b316f` | Solution dosyası eklendi |
| - | `7711e87` | Procfile düzeltmesi |
| - | `f21a97f` | Crypto ikon düzeltmesi |
| - | `629e166` | İkon hizalama düzeltmesi |
| - | `3c3b6aa` | Ödeme linki yönlendirme düzeltmesi |
