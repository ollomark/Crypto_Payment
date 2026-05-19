# Bilinen Sorunlar ve Analiz Raporu

**Tarih:** 18 Şubat 2026  
**Branch:** fix  
**Heroku App:** cryptopayment-v2  
**URL:** https://cryptopayment-v2-607c8f5c6c2d.herokuapp.com/

---

## SORUN 1: Ödeme Onay Ekranı Görünmüyor (Result Page)

### Açıklama
Kullanıcı ödeme yaptıktan sonra (örn: ORD-1771354954860, txnId: `6994bb4c8635561199057496`), ödeme onaylandığı halde "Payment Confirmed!" ekranı düzgün görünmüyor.

### Heroku Log Analizi

**Backend doğru çalışıyor:**
- Plisio API `status: "completed"` döndü
- DB'de `pending → completed` olarak güncellendi (19:10:07)

**Polling doğru çalışıyor:**
- `/api/invoices/status/{txnId}` endpoint'i her 3 saniyede çağrılıyor
- 19:06:25'ten 19:10:07'ye kadar düzenli polling yapıldı

**Result sayfası yüklendi:**
- 19:10:08'de `/result-invoice/34?txnId=6994bb4c8635561199057496` isteği yapıldı
- HTTP 200 döndü, 2441 bytes

### Olası Kök Nedenler

1. **CSS dosyası yüklenemiyor olabilir:**
   - `ResultInvoice.cshtml` dosyasında `<link href="~/admin/my-css/done.css">` referansı var
   - Heroku'da static file serving düzgün çalışmıyor olabilir
   - `~/` prefix'i Heroku'da farklı resolve olabilir

2. **Status mapping sorunu:**
   - `PaymentController.ApplyPlisioDetails()` metodu Plisio'dan gelen raw status'u direkt viewModel'e yazıyor
   - Plisio `"completed"` dönüyor → `GetStatusText()` bunu `"Payment Confirmed!"` olarak map ediyor
   - Bu kısım doğru çalışıyor gibi görünüyor

3. **Sayfa yönlendirme sorunu:**
   - Polling JS kodu status değiştiğinde `window.location.href = /result-invoice/{id}?txnId=...` yapıyor
   - Redirect çalışıyor (logda 200 görünüyor)
   - Ama kullanıcı sayfayı görememiş olabilir (tarayıcı cache, JS hatası vs.)

### İlgili Dosyalar
- `Controllers/PaymentController.cs` → `ResultInvoice()` action
- `Views/Payment/ResultInvoice.cshtml` → Onay sayfası view
- `Views/Payment/Index.cshtml` → Ödeme sayfası (polling JS burada)
- `wwwroot/admin/my-css/done.css` → Result sayfası CSS
- `Controllers/InvoicesController.cs` → `GetStatus()` polling endpoint
- `Helpers/StatusMapper.cs` → Plisio status → DB status mapping

### Kontrol Edilmesi Gerekenler
- [ ] Heroku'da `done.css` dosyasına erişilebiliyor mu? (Browser'da `/admin/my-css/done.css` aç)
- [ ] Kullanıcı tam olarak ne görüyor? (Boş sayfa mı, ödeme sayfası takılı mı kalıyor?)
- [ ] Browser console'da JS hatası var mı?
- [ ] Tarayıcı cache temizlenip tekrar denensin

---

## SORUN 2: Callback 403 Hatası (HMAC Doğrulama Başarısız)

### Açıklama
Plisio, ödeme durumu değiştiğinde callback URL'ye POST isteği gönderiyor. Ancak bu istek 403 (Forbidden) ile reddediliyor.

### Heroku Log Detayı
```
19:10:04 - POST /api/callback?json=true → 403
```

### Kök Neden Analizi

**Callback URL yapısı:**
- Fatura oluşturulurken callback URL'ye `?json=true` ekleniyor (`PlisioManager.AddJsonTrue()`)
- Plisio bu URL'ye POST isteği gönderiyor

**HMAC doğrulama süreci (`CallbackController.VerifyPlisioCallback()`):**
1. Tüm Query parametreleri alınıyor (`verify_hash` hariç)
2. Form parametreleri alınıyor (`verify_hash` hariç)
3. Alfabetik sıralanıp JSON serialize ediliyor
4. HMAC-SHA1 ile hash hesaplanıyor
5. Plisio'nun gönderdiği `verify_hash` ile karşılaştırılıyor

**Sorun:**
- Callback URL'de `?json=true` var → Bu Query parametresi olarak geliyor
- Plisio kendi parametrelerini (txn_id, status, verify_hash vs.) de Query veya Form olarak gönderiyor
- `json=true` parametresi HMAC hesaplamasına dahil ediliyor ama Plisio'nun hash hesaplamasında bu parametre yok
- Bu yüzden hash'ler uyuşmuyor → 403

### Çözüm Önerisi
`VerifyPlisioCallback()` metodunda `json` parametresini de `verify_hash` gibi hariç tutmak:

```csharp
// Query parametrelerini ekle (verify_hash ve json hariç)
foreach (var param in Request.Query)
{
    if (!string.Equals(param.Key, "verify_hash", StringComparison.OrdinalIgnoreCase)
        && !string.Equals(param.Key, "json", StringComparison.OrdinalIgnoreCase))
    {
        parameters[param.Key] = param.Value.ToString();
    }
}
```

### Etki
- Callback 403 olsa bile polling mekanizması çalışıyor, yani ödeme durumu yine güncelleniyor
- Ama callback daha hızlı ve güvenilir (Plisio anında bildirim gönderiyor)
- Polling ise her 3 saniyede Plisio API'ye istek atıyor (gereksiz yük)

### İlgili Dosyalar
- `Controllers/CallbackController.cs` → `VerifyPlisioCallback()` metodu
- `Manager/PlisioManager.cs` → `AddJsonTrue()` metodu

---

## SORUN 3: (Potansiyel) Static File Serving - Heroku

### Açıklama
Heroku'da .NET uygulamaları static dosyaları (CSS, JS, images) serve ederken sorun yaşayabilir.

### Detay
- `ResultInvoice.cshtml` dosyası `~/admin/my-css/done.css` referansı kullanıyor
- `InvoiceList.cshtml` dosyası `admin/velzon-dist/assets/images/svg/crypto-icons/` altındaki SVG'leri kullanıyor
- Bu dosyaların Heroku'da erişilebilir olduğundan emin olunmalı

### Kontrol
- `Program.cs`'de `app.UseStaticFiles()` çağrılıyor mu?
- `wwwroot` klasörü deploy'a dahil mi?
- `.gitignore`'da `wwwroot` hariç tutulmuş mu?

### İlgili Dosyalar
- `Program.cs`
- `.gitignore`
- `wwwroot/` klasörü

---

## Genel Durum Özeti

| Bileşen | Durum | Not |
|---------|-------|-----|
| Build | ✅ 0 hata, 6 uyarı | 4 migration + 2 cshtml uyarısı |
| Testler | ✅ 40/40 geçiyor | Property testler 20 iterasyon |
| Ödeme oluşturma | ✅ Çalışıyor | Plisio API entegrasyonu OK |
| Ödeme sayfası | ✅ Çalışıyor | QR kod, wallet adresi, countdown |
| Polling | ✅ Çalışıyor | Her 3 saniyede status kontrolü |
| Callback | ❌ 403 hatası | HMAC doğrulama başarısız |
| Result sayfası | ⚠️ Belirsiz | Yükleniyor ama kullanıcı görememiş |
| Crypto ikonlar | ✅ Düzeltildi | getCryptoIcon() fonksiyonu |
| Ödeme linkleri | ✅ Düzeltildi | Kendi sayfamıza yönlendiriyor |
