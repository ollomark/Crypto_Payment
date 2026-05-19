# Heroku Deployment - Crypto Payment v2

## 📋 Deployment Özeti

**Tarih:** 12 Şubat 2026  
**App Adı:** `cryptopayment-v2`  
**URL:** https://cryptopayment-v2-607c8f5c6c2d.herokuapp.com/  
**Region:** US  
**Stack:** Heroku-24  
**Buildpack:** heroku/dotnet

---

## 🎯 Yapılan İşlemler

### 1. Yeni Heroku App Oluşturuldu
```bash
heroku apps:create cryptopayment-v2 --region us
```

**Neden yeni app?**  
Eski `cryptopayment` app'i korundu, yeni fix branch ayrı bir app olarak deploy edildi.

---

### 2. PostgreSQL Database Eklendi
```bash
heroku addons:create heroku-postgresql:essential-0 -a cryptopayment-v2
```

**Plan:** Essential-0 ($5/month)  
**Database URL:** Otomatik olarak `DATABASE_URL` environment variable'ına eklendi

---

### 3. .NET Buildpack Ayarlandı
```bash
heroku buildpacks:set heroku/dotnet -a cryptopayment-v2
```

**SDK Version:** .NET 8.0.418 (otomatik algılandı)

---

### 4. Environment Variables Eklendi

```bash
# Production ortamı
heroku config:set ASPNETCORE_ENVIRONMENT=Production -a cryptopayment-v2

# Plisio API Key (eski sistemden kopyalandı)
heroku config:set Plisio__ApiKey=YOUR_PLISIO_API_KEY -a cryptopayment-v2

# CORS ayarları
heroku config:set ALLOWED_ORIGINS=https://cryptopayment-v2-607c8f5c6c2d.herokuapp.com -a cryptopayment-v2

# SMTP ayarları (GitHub main branch'ten alındı)
heroku config:set SMTP_USER=ridvanakyil16@gmail.com -a cryptopayment-v2
heroku config:set SMTP_FROM=ridvanakyil16@gmail.com -a cryptopayment-v2
heroku config:set SMTP_PASS=YOUR_SMTP_APP_PASSWORD -a cryptopayment-v2

# Project file path (buildpack için)
heroku config:set PROJECT_FILE=Crypto_Payment/Crypto_Payment.csproj -a cryptopayment-v2
```

---

### 5. Git Remote Eklendi ve Deploy Edildi

```bash
# Heroku git remote eklendi
git remote add heroku https://git.heroku.com/cryptopayment-v2.git

# Fix branch'i main olarak push edildi
git push heroku fix:main
```

---

### 6. Yapılan Kod Değişiklikleri

#### a) Solution Dosyası Eklendi
**Dosya:** `Crypto_Payment.sln`  
**Neden:** Heroku buildpack root'ta .sln veya .csproj dosyası arıyor

```bash
dotnet new sln -n Crypto_Payment -o Crypto_Payment
dotnet sln Crypto_Payment/Crypto_Payment.sln add Crypto_Payment/Crypto_Payment/Crypto_Payment.csproj
```

#### b) Procfile Düzeltildi
**Dosya:** `Procfile`  
**Eski:** `web: cd Crypto_Payment && dotnet Crypto_Payment.dll`  
**Yeni:** `web: cd Crypto_Payment/bin/publish && dotnet Crypto_Payment.dll`

**Neden:** Build sonrası DLL dosyası `bin/publish/` klasöründe oluşuyor

---

## 📊 Mevcut Konfigürasyon

```bash
ALLOWED_ORIGINS:        https://cryptopayment-v2-607c8f5c6c2d.herokuapp.com
ASPNETCORE_ENVIRONMENT: Production
DATABASE_URL:           (Heroku tarafından otomatik set edilir)
Plisio__ApiKey:         (gizli)
PROJECT_FILE:           Crypto_Payment/Crypto_Payment.csproj
SMTP_FROM:              ridvanakyil16@gmail.com
SMTP_PASS:              (gizli)
SMTP_USER:              ridvanakyil16@gmail.com
```

---

## ✅ Deployment Sonuçları

### Database Migration
- ✅ PostgreSQL database oluşturuldu
- ✅ Tüm tablolar başarıyla oluşturuldu (EnsureCreated kullanıldı)
- ✅ Identity tabloları (AspNetUsers, AspNetRoles, vb.)
- ✅ Uygulama tabloları (Invoices, Customers, InvoiceItems, Permissions)

### Uygulama Durumu
- ✅ Build başarılı (152.2 MB slug size)
- ✅ Uygulama çalışıyor (state: up)
- ✅ Port binding başarılı (PORT environment variable)
- ✅ HTTPS redirect devre dışı (Heroku proxy için)

### Email Sistemi
- ✅ SMTP ayarları yapılandırıldı
- ✅ Gmail App Password eklendi
- ✅ Email doğrulama aktif

---

## 🔗 Eski vs Yeni Karşılaştırma

| Özellik | Eski App (cryptopayment) | Yeni App (cryptopayment-v2) |
|---------|--------------------------|------------------------------|
| URL | https://cryptopayment-34be79722fc3.herokuapp.com/ | https://cryptopayment-v2-607c8f5c6c2d.herokuapp.com/ |
| Branch | main (eski kod) | fix (bugfix'li kod) |
| Database | PostgreSQL (eski data) | PostgreSQL (yeni, boş) |
| Durum | Çalışıyor (dokunulmadı) | Çalışıyor (yeni deploy) |

---

## 🚀 Gelecek Güncellemeler İçin

### Yeni Kod Deploy Etmek
```bash
# Fix branch'te değişiklik yap
git add .
git commit -m "Açıklama"

# Heroku'ya push et
git push heroku fix:main
```

### Config Değiştirmek
```bash
# Yeni config ekle
heroku config:set KEY=VALUE -a cryptopayment-v2

# Config'i sil
heroku config:unset KEY -a cryptopayment-v2

# Tüm config'leri gör
heroku config -a cryptopayment-v2
```

### Logları İzlemek
```bash
# Canlı log izle
heroku logs --tail -a cryptopayment-v2

# Son 100 satır
heroku logs -n 100 -a cryptopayment-v2
```

### Database Yönetimi
```bash
# PostgreSQL console'a bağlan
heroku pg:psql -a cryptopayment-v2

# Database bilgilerini gör
heroku pg:info -a cryptopayment-v2

# Backup al
heroku pg:backups:capture -a cryptopayment-v2
```

### App'i Yeniden Başlat
```bash
heroku restart -a cryptopayment-v2
```

---

## ⚠️ Önemli Notlar

1. **Database Migration:** SQLite migration'ları PostgreSQL'de çalışmadığı için `EnsureCreated()` kullanıldı. Production'da migration değişikliği yapılırsa dikkatli olunmalı.

2. **SMTP Şifresi:** Gmail App Password kullanılıyor. Normal Gmail şifresi çalışmaz. Şifre değişirse Heroku config'i güncellenmelidir.

3. **CORS:** Sadece Heroku URL'sine izin veriliyor. Farklı domain'den erişim gerekirse `ALLOWED_ORIGINS` güncellenmelidir.

4. **Plisio Callback:** Callback URL dinamik olarak `window.location.origin + '/api/callback'` şeklinde oluşturuluyor, hardcoded IP yok.

5. **Rate Limiting:** Auth endpoint'leri 10 req/min, callback endpoint'leri 30 req/min ile sınırlı.

---

## 📞 Sorun Giderme

### App Crash Olursa
```bash
# Logları kontrol et
heroku logs --tail -a cryptopayment-v2

# App'i yeniden başlat
heroku restart -a cryptopayment-v2
```

### Database Bağlantı Sorunu
```bash
# DATABASE_URL'i kontrol et
heroku config:get DATABASE_URL -a cryptopayment-v2

# PostgreSQL durumunu kontrol et
heroku pg:info -a cryptopayment-v2
```

### Email Gönderilmiyor
```bash
# SMTP ayarlarını kontrol et
heroku config -a cryptopayment-v2 | grep SMTP

# Logları kontrol et (SMTP hataları için)
heroku logs --tail -a cryptopayment-v2
```

---

## 🎉 Sonuç

Crypto Payment v2 başarıyla Heroku'ya deploy edildi ve çalışıyor!

- ✅ Tüm bugfix'ler uygulandı
- ✅ PostgreSQL database hazır
- ✅ Email sistemi çalışıyor
- ✅ Plisio entegrasyonu aktif
- ✅ Güvenlik iyileştirmeleri yapıldı

**Site URL:** https://cryptopayment-v2-607c8f5c6c2d.herokuapp.com/
