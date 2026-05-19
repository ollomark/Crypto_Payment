# Crypto Payment — Kripto Para Ödeme Sistemi

## Proje Hakkında

Crypto Payment, işletmelerin müşterilerine kripto para ile fatura kesip ödeme almasını sağlayan bir web uygulamasıdır. Plisio API entegrasyonu ile Bitcoin, Ethereum, USDT ve diğer kripto paralar üzerinden ödeme kabul eder.

**Teknoloji Yığını:**
- ASP.NET Core 8.0 MVC
- Entity Framework Core 8.0
- SQLite (geliştirme/production) / PostgreSQL (opsiyonel)
- ASP.NET Core Identity (kimlik doğrulama, 2FA desteği)
- Plisio API (kripto ödeme gateway)
- Velzon Admin Template (UI)
- QRCoder (2FA QR kod üretimi)

**Temel Özellikler:**
- Müşteri yönetimi (CRUD)
- Fatura oluşturma ve yönetimi
- Kripto para ile ödeme alma (Plisio API)
- Ödeme durumu takibi (callback ile otomatik güncelleme)
- Rol ve yetki yönetimi
- İki faktörlü kimlik doğrulama (2FA)
- Email doğrulama sistemi
- Soft delete (kayıtlar silinmez, pasif yapılır)

---

## Proje Yapısı

```
Crypto_Payment/
├── Crypto_Payment/                  # Ana uygulama
│   ├── Controllers/                 # 15 controller (API + MVC)
│   │   ├── AuthController.cs        # Kimlik doğrulama (login, register, 2FA)
│   │   ├── CallbackController.cs    # Plisio ödeme callback (HMAC-SHA1 korumalı)
│   │   ├── CustomerController.cs    # Müşteri view controller
│   │   ├── CustomersController.cs   # Müşteri API controller
│   │   ├── HomeController.cs        # Dashboard
│   │   ├── InvoiceController.cs     # Fatura view controller
│   │   ├── InvoicesController.cs    # Fatura API controller
│   │   ├── PaymentController.cs     # Ödeme sayfaları (public)
│   │   ├── PermissionController.cs  # Yetki view controller
│   │   ├── PermissionsController.cs # Yetki API controller
│   │   ├── RoleClaimController.cs   # Rol-yetki eşleme view
│   │   ├── RoleController.cs        # Rol view controller
│   │   ├── RolesController.cs       # Rol API controller
│   │   ├── UserController.cs        # Kullanıcı view controller
│   │   └── UsersController.cs       # Kullanıcı API controller
│   ├── Data/
│   │   └── AppDbContext.cs          # EF Core DbContext
│   ├── DTOS/                        # Veri transfer nesneleri
│   ├── Helpers/                     # Email gönderici
│   ├── Manager/                     # İş mantığı katmanı
│   ├── Migrations/                  # EF Core migration'ları
│   ├── Models/                      # Veritabanı modelleri
│   ├── Services/                    # Servis arayüzleri
│   ├── Views/                       # Razor view'ları
│   ├── wwwroot/                     # Statik dosyalar (CSS, JS, images)
│   ├── Program.cs                   # Uygulama giriş noktası
│   └── Crypto_Payment.csproj        # Proje dosyası
├── Crypto_Payment.Tests/            # Test projesi
│   ├── Helpers/
│   │   └── TestDbContextFactory.cs  # InMemory DB factory
│   └── PropertyTests/               # 10 property-based test
├── deploy/                          # Deployment scriptleri
│   ├── setup-vds.sh                 # VDS kurulum scripti
│   ├── nginx-config.sh              # Nginx yapılandırma
│   ├── register-user.sh             # Kullanıcı oluşturma scripti
│   └── test-all-endpoints.sh        # Kapsamlı API test scripti
├── LOCAL_SETUP.md                   # Yerel kurulum kılavuzu
├── Procfile                         # Heroku deployment dosyası
└── README.md                        # Bu dosya
```

---

## API Endpoint Haritası

### Public Endpoint'ler (Login Gerektirmeyen)

| Method | Route | Açıklama |
|--------|-------|----------|
| GET | `/api/auth/login` | Login sayfası |
| POST | `/api/auth/login` | Login işlemi (CSRF korumalı) |
| GET | `/api/auth/register` | Kayıt sayfası |
| POST | `/api/auth/register` | Kayıt işlemi (CSRF korumalı) |
| GET | `/api/auth/register-success` | Kayıt başarılı sayfası |
| GET | `/api/auth/confirm-email` | Email doğrulama |
| GET | `/api/auth/email-verification` | Email doğrulama bilgi sayfası |
| GET | `/api/auth/twofactor` | 2FA giriş sayfası |
| POST | `/api/auth/twofactor` | 2FA doğrulama |
| GET | `/pay/{id}` | Ödeme sayfası |
| GET | `/result-invoice/{id}` | Ödeme sonuç sayfası |
| GET | `/api/invoices/status/{id}` | Fatura durum sorgulama |
| POST/GET | `/api/callback` | Plisio ödeme callback (HMAC-SHA1) |

### Korumalı Endpoint'ler (Login Gerekli)

| Method | Route | Açıklama |
|--------|-------|----------|
| GET | `/` | Dashboard |
| GET | `/customers` | Müşteri listesi sayfası |
| GET | `/invoices` | Fatura listesi sayfası |
| GET | `/invoices/invoice-add` | Fatura ekleme sayfası |
| GET | `/invoices/invoice-detail/{id}` | Fatura detay sayfası |
| GET | `/roles` | Rol listesi sayfası |
| GET | `/permissions` | Yetki listesi sayfası |
| GET | `/users` | Kullanıcı listesi sayfası |
| GET | `/role-claims` | Rol-yetki eşleme sayfası |
| GET | `/api/auth/2fa/setup` | 2FA kurulum sayfası |
| POST | `/api/auth/2fa/enable` | 2FA etkinleştirme |
| POST | `/api/auth/logout` | Çıkış |

### CRUD API'leri (Login Gerekli)

**Müşteriler (`/api/customers`)**
| Method | Route | Açıklama |
|--------|-------|----------|
| GET | `/api/customers/GetAll` | Tüm müşteriler |
| GET | `/api/customers/GetTotalCustomerCount` | Müşteri sayısı |
| GET | `/api/customers/{id}` | Müşteri detay |
| POST | `/api/customers/create` | Müşteri oluştur |
| PUT | `/api/customers/{id}` | Müşteri güncelle |
| DELETE | `/api/customers/{id}` | Müşteri sil (soft delete) |

**Faturalar (`/api/invoices`)**
| Method | Route | Açıklama |
|--------|-------|----------|
| GET | `/api/invoices/GetAll` | Tüm faturalar |
| GET | `/api/invoices/GetTotalInvoiceCount` | Fatura sayısı |
| POST | `/api/invoices/invoice-add` | Fatura oluştur |
| POST | `/api/invoices/invoice-update-registration-status` | Fatura durumu güncelle |

**Yetkiler (`/api/permissions`)**
| Method | Route | Açıklama |
|--------|-------|----------|
| GET | `/api/permissions/GetAll` | Tüm yetkiler |
| GET | `/api/permissions/{id}` | Yetki detay |
| POST | `/api/permissions/create` | Yetki oluştur |
| PUT | `/api/permissions/{id}` | Yetki güncelle |
| DELETE | `/api/permissions/{id}` | Yetki sil |

**Roller (`/api/roles`)**
| Method | Route | Açıklama |
|--------|-------|----------|
| GET | `/api/roles/GetAll` | Tüm roller |
| GET | `/api/roles/GetTotalRoleCount` | Rol sayısı |
| GET | `/api/roles/{id}` | Rol detay |
| POST | `/api/roles/create` | Rol oluştur |
| PUT | `/api/roles/{id}` | Rol güncelle |
| DELETE | `/api/roles/{id}` | Rol sil |

**Kullanıcılar (`/api/users`)**
| Method | Route | Açıklama |
|--------|-------|----------|
| GET | `/api/users/GetAll` | Tüm kullanıcılar |

---

## Yapılan İyileştirmeler ve Düzeltmeler

Proje analiz edilip toplam 4 tur düzeltme yapılmıştır. Her turda önce detaylı analiz raporu hazırlanmış, onay alındıktan sonra düzeltmeler uygulanmıştır.

### Tur 1 — Veritabanı Düzeltmeleri (database-fixes spec)

**7 görev tamamlandı:**

1. **Customer modeli nullable field düzeltmesi**
   - `Customer.cs` → `CompanyName`, `Email`, `Telegram`, `Skype` alanları `string?` (nullable) yapıldı
   - Zorunlu alanlar (`FirstName`, `LastName`, `Phone`) `[Required]` attribute ile işaretlendi
   - Dosya: `Crypto_Payment/Models/Customer.cs`

2. **InvoiceItem decimal hassasiyet düzeltmesi**
   - `InvoiceItem.Total` alanına `[Column(TypeName = "decimal(18,8)")]` eklendi
   - Kripto para tutarları için 8 ondalık basamak hassasiyeti sağlandı
   - Dosya: `Crypto_Payment/Models/InvoiceItem.cs`

3. **AppDbContext hassasiyet ve ilişki yapılandırması**
   - `OnModelCreating` içinde tüm decimal alanlar için precision(18,8) tanımlandı
   - Invoice → Customer ilişkisi `HasOne/WithMany` ile yapılandırıldı
   - Soft delete için global query filter eklendi: `.HasQueryFilter(x => x.IsActive)`
   - Dosya: `Crypto_Payment/Data/AppDbContext.cs`

4. **CustomerManager soft delete düzeltmesi**
   - `DeleteAsync` metodu fiziksel silme yerine `IsActive = false` yapacak şekilde değiştirildi
   - Dosya: `Crypto_Payment/Manager/CustomerManager.cs`

5. **Test projesi oluşturuldu**
   - `Crypto_Payment.Tests` projesi xUnit + FsCheck.Xunit + InMemory DB ile kuruldu
   - `TestDbContextFactory.cs` helper sınıfı oluşturuldu
   - Dosya: `Crypto_Payment.Tests/Crypto_Payment.Tests.csproj`

6. **4 property-based test yazıldı**
   - `CustomerCreationPropertyTests.cs` — Müşteri oluşturma testi
   - `SoftDeletePropertyTests.cs` — Soft delete davranış testi
   - `InvoiceItemTotalPropertyTests.cs` — Decimal hassasiyet testi
   - `NullableFieldsPropertyTests.cs` — Nullable alan testi

7. **Migration oluşturuldu**
   - `20260210142115_DatabaseFixes.cs` — Tüm değişiklikleri içeren migration

---

### Tur 2 — Güvenlik ve Veritabanı İyileştirmeleri (security-and-db-improvements spec)

**11 görev tamamlandı:**

1. **CallbackController HMAC-SHA1 doğrulaması**
   - Plisio callback endpoint'ine HMAC-SHA1 imza doğrulaması eklendi
   - `verify_hash` parametresi ile gelen imza, sunucu tarafında hesaplanan imza ile karşılaştırılıyor
   - Geçersiz imza → 403 Forbidden
   - Development ortamında doğrulama atlanabiliyor (test kolaylığı)
   - Dosya: `Crypto_Payment/Controllers/CallbackController.cs`

2. **InvoicesController yetkilendirme**
   - Controller seviyesinde `[Authorize]` attribute eklendi
   - Tüm fatura API'leri artık login gerektiriyor
   - `status/{id}` endpoint'i `[AllowAnonymous]` olarak bırakıldı (ödeme sayfasından erişim için)
   - Dosya: `Crypto_Payment/Controllers/InvoicesController.cs`

3. **Invoice soft delete**
   - `Invoice.IsActive` alanı eklendi (default: true)
   - Silme işlemi `IsActive = false` yapıyor
   - Global query filter ile silinen faturalar otomatik filtreleniyor
   - Dosya: `Crypto_Payment/Models/Invoice.cs`

4. **InvoiceItem.Total decimal düzeltmesi**
   - `[Column(TypeName = "decimal(18,8)")]` attribute eklendi
   - Dosya: `Crypto_Payment/Models/InvoiceItem.cs`

5. **AppDbContext index ve foreign key iyileştirmeleri**
   - `Invoice.CustomerId` için index eklendi
   - `Permission.TopPermissionId` için index eklendi
   - Tüm decimal alanlar için precision(18,8) yapılandırması
   - Foreign key ilişkileri düzgün tanımlandı
   - Dosya: `Crypto_Payment/Data/AppDbContext.cs`

6. **LINQ sorgu düzeltmesi**
   - `InvoiceManager.GetAllAsync()` → N+1 sorgu problemi çözüldü
   - `.Include(x => x.Customer)` ile eager loading eklendi
   - Dosya: `Crypto_Payment/Manager/InvoiceManager.cs`

7. **Permission navigation property**
   - `Permission.TopPermission` navigation property eklendi
   - Self-referencing ilişki düzgün yapılandırıldı
   - Dosya: `Crypto_Payment/Models/Permission.cs`

8. **Role.cs silindi**
   - Kullanılmayan custom `Role.cs` modeli silindi
   - Identity'nin kendi `IdentityRole` sınıfı kullanılıyor
   - Silinen dosya: `Crypto_Payment/Models/Role.cs`

9. **N+1 sorgu düzeltmesi**
   - `InvoiceManager` ve `PermissionManager` sorgularında eager loading eklendi
   - Dosyalar: `Crypto_Payment/Manager/InvoiceManager.cs`, `Crypto_Payment/Manager/PermissionManager.cs`

10. **Migration oluşturuldu**
    - `20260210144142_SecurityAndDbImprovements.cs`

11. **6 yeni property-based test yazıldı** (toplam 10)
    - `HmacValidationPropertyTests.cs` — HMAC doğrulama testi
    - `InvoiceSoftDeletePropertyTests.cs` — Fatura soft delete testi
    - `PermissionNavigationPropertyTests.cs` — Permission navigation testi
    - `QueryFilterPropertyTests.cs` — Global query filter testi
    - `RecentInvoicesPropertyTests.cs` — Son faturalar sorgu testi
    - `CustomerCreationPropertyTests.cs` — Güncellenmiş müşteri testi

---

### Tur 3 — Ek Düzeltmeler (14 sorun)

Doğrudan uygulandı (spec olmadan):

1. **InvoiceManager — GetTotalInvoiceByStatusAsync null kontrolü**
   - Null status parametresi için güvenli kontrol eklendi

2. **InvoiceManager — GetRecentInvoicesAsync sıralama**
   - `OrderByDescending(x => x.Id)` ile son faturalar önce gelecek şekilde sıralandı

3. **InvoiceItemManager — decimal hassasiyet**
   - Hesaplamalarda `decimal` tipi kullanımı doğrulandı

4. **CustomerManager — GetAllAsync soft delete filtresi**
   - Global query filter ile zaten filtreleniyor, ek kontrol gereksiz

5. **PermissionManager — navigation property include**
   - `TopPermission` navigation property eager loading eklendi

6. **PermissionDto — TopPermissionName alanı**
   - DTO'ya `TopPermissionName` alanı eklendi

7. **InvoiceDashboardDto — eksik alanlar**
   - Dashboard için gerekli alanlar eklendi

8. **RoleManager — Identity entegrasyonu**
   - `IdentityRole` ile uyumlu hale getirildi

9. **CustomerDto — validation attribute'ları**
   - `[Required]` ve `[EmailAddress]` attribute'ları eklendi

10. **InvoiceDto — validation**
    - Zorunlu alanlar için validation eklendi

11. **PlisioManager — hata yönetimi**
    - API çağrılarında try-catch ve loglama eklendi

12. **AppDbContext — ek index'ler**
    - Sık sorgulanan alanlara index eklendi

13. **Program.cs — PostgreSQL desteği**
    - `DATABASE_URL` environment variable ile PostgreSQL bağlantısı
    - Yoksa SQLite kullanılıyor

14. **Genel kod temizliği**
    - Kullanılmayan using'ler kaldırıldı

---

### Tur 4 — Son Düzeltmeler (9 sorun)

1. **RolesController route düzeltmesi**
   - `{id:int}` → `{id}` olarak değiştirildi (3 endpoint: GetById, Update, Delete)
   - Neden: Role ID'leri GUID string, int değil
   - Dosya: `Crypto_Payment/Controllers/RolesController.cs`

2. **UsersController async düzeltmesi**
   - `GetAll()` metodunda sync `.ToList()` → async `.ToListAsync()` olarak değiştirildi
   - `using Microsoft.EntityFrameworkCore` eklendi
   - Dosya: `Crypto_Payment/Controllers/UsersController.cs`

3. **InvoicesController exception handling**
   - `InvoiceUpdateRegistrationStatus` metodunda `InvalidOperationException` catch → `KeyNotFoundException` catch
   - Dosya: `Crypto_Payment/Controllers/InvoicesController.cs`

4. **GetTotalInvoiceByStatusAsync durum eşleme düzeltmesi**
   - `"pending"` durumu `"paid"` grubundan çıkarıldı
   - Sadece `"completed"` ve `"mismatch"` → `"paid"` olarak eşleniyor
   - Dosya: `Crypto_Payment/Manager/InvoiceManager.cs`

5. **InvoicesController.MapPlisioStatus birleştirme**
   - CallbackController ile aynı durum eşleme mantığı kullanılacak şekilde birleştirildi
   - Dosya: `Crypto_Payment/Controllers/InvoicesController.cs`

6. **Service.cs silindi** — Kullanılmayan ölü kod
7. **IMerchantService.cs silindi** — Implementasyonu olmayan ölü arayüz
8. **IUsdtRateService.cs silindi** — Implementasyonu olmayan ölü arayüz
9. **PermissionController.cs temizlendi** — Var olmayan `_service` referansına ait yorum satırları kaldırıldı

---

### Tur 5 — QR Kod ve Payment Düzeltmeleri

1. **QR kod üretimi Linux uyumlu hale getirildi**
   - `System.Drawing.Bitmap` → `PngByteQRCode` olarak değiştirildi
   - Artık Linux sunucuda da 2FA QR kodu üretiliyor
   - Dosya: `Crypto_Payment/Controllers/AuthController.cs`

2. **PaymentController retry mekanizması**
   - Plisio wallet adresi gecikmeli geldiğinde "preparing" durumu gösteriliyor
   - Ödeme sayfası wallet hazır olana kadar polling yapıyor (2sn aralıkla)
   - Dosya: `Crypto_Payment/Controllers/PaymentController.cs`

3. **InvoiceManager hata yönetimi**
   - `CreateAsync` metodu Plisio hatalarında `InvalidOperationException` fırlatıyor
   - Controller'da düzgün hata mesajı ile 422 dönülüyor
   - Dosya: `Crypto_Payment/Manager/InvoiceManager.cs`

---

### Tur 6 — Frontend Bug Düzeltmeleri (7 sorun)

Tüm frontend view dosyaları detaylıca incelendi ve aşağıdaki buglar tespit edilip düzeltildi:

1. **InvoiceAdd.cshtml — `total` alanı string gönderme hatası**
   - Sorun: Fatura oluşturulurken `total` alanı `"$10.00"` string olarak gönderiliyordu
   - Çözüm: `parseFloat(rawTotal.replace(/[^0-9.\-]/g, ''))` ile decimal'e parse edildi
   - Dosya: `Crypto_Payment/Views/Invoice/InvoiceAdd.cshtml`

2. **InvoiceAdd.cshtml — Loading modal takılma sorunu**
   - Sorun: `data-bs-backdrop="static"` ile `hidden.bs.modal` event'i tetiklenmiyordu, modal kapanmıyordu
   - Çözüm: `forceHideLoading()` fonksiyonu eklendi — Bootstrap instance dispose + DOM'dan zorla temizleme (classList, style, backdrop, body overflow)
   - Dosya: `Crypto_Payment/Views/Invoice/InvoiceAdd.cshtml`

3. **InvoiceAdd.cshtml — Hata mesajları görünmüyordu**
   - Sorun: API'den dönen hata mesajları kullanıcıya gösterilmiyordu
   - Çözüm: `error.message`, `error.errors` (ASP.NET validation), `error.title` formatları handle edildi
   - Dosya: `Crypto_Payment/Views/Invoice/InvoiceAdd.cshtml`

4. **InvoiceAdd.cshtml — Callback URL localhost**
   - Sorun: Default callback URL `https://localhost:5001/api/callback` idi
   - Çözüm: `http://185.7.243.141:5000/api/callback` olarak güncellendi
   - Dosya: `Crypto_Payment/Views/Invoice/InvoiceAdd.cshtml`

5. **InvoiceDetail.cshtml — Ölü script bloğu**
   - Sorun: Tanımsız `i.status` değişkenine referans veren JavaScript bloğu vardı
   - Çözüm: Kırık script bloğu tamamen kaldırıldı, sadece `printInvoice()` ve `downloadInvoicePdf()` bırakıldı
   - Dosya: `Crypto_Payment/Views/Invoice/InvoiceDetail.cshtml`

6. **Login.cshtml — Response body çift okuma hatası**
   - Sorun: `res.json()` çağrıldıktan sonra `res.text()` çağrılıyordu → body stream zaten tüketilmiş olduğu için hata
   - Çözüm: Tek seferde `res.json()` ile okunup, hata durumunda `return` ile akış kesildi
   - Dosya: `Crypto_Payment/Views/Auth/Login.cshtml`

7. **CustomerList.cshtml — 3 sorun birden**
   - Sorun 1: `tbody.addEventListener` `DOMContentLoaded` scope dışındaydı → tbody henüz yüklenmeden event listener ekleniyordu
   - Sorun 2: Silme butonu `data-id` attribute'u kullanmıyordu
   - Sorun 3: Müşteri ekleme form handler'ı (`customerAddForm` submit) tamamen eksikti
   - Çözüm: Tüm JS kodu `DOMContentLoaded` içine taşındı, silme event delegation ile düzeltildi, müşteri ekleme handler'ı geri eklendi
   - Dosya: `Crypto_Payment/Views/Customer/CustomerList.cshtml`

---

### Tur 7 — Route Ambiguity Düzeltmesi

1. **HomeController route çakışması**
   - Sorun: `Privacy()` ve `Error()` action'larında route attribute yoktu → `AmbiguousMatchException` hatası → tüm sayfalarda 500 Internal Server Error
   - Çözüm: `[HttpGet("privacy")]` ve `[HttpGet("error")]` explicit route'ları eklendi
   - Dosya: `Crypto_Payment/Controllers/HomeController.cs`

2. **ExceptionHandler path düzeltmesi**
   - Sorun: `app.UseExceptionHandler("/Home/Error")` → bu route artık `/error` olarak tanımlı
   - Çözüm: `app.UseExceptionHandler("/error")` olarak güncellendi
   - Dosya: `Crypto_Payment/Program.cs`

---

## Test Sonuçları

### Property-Based Testler (10/10 PASS)

```
Crypto_Payment.Tests/PropertyTests/
├── CustomerCreationPropertyTests.cs    ✅ Müşteri oluşturma
├── SoftDeletePropertyTests.cs          ✅ Soft delete davranışı
├── InvoiceItemTotalPropertyTests.cs    ✅ Decimal hassasiyet
├── NullableFieldsPropertyTests.cs      ✅ Nullable alan kontrolü
├── HmacValidationPropertyTests.cs      ✅ HMAC doğrulama
├── InvoiceSoftDeletePropertyTests.cs   ✅ Fatura soft delete
├── PermissionNavigationPropertyTests.cs ✅ Permission navigation
├── QueryFilterPropertyTests.cs         ✅ Global query filter
└── RecentInvoicesPropertyTests.cs      ✅ Son faturalar sorgusu
```

Testleri çalıştırmak için:
```bash
cd Crypto_Payment
dotnet test
```

### VDS API Testleri (78 test: 68 PASS, 10 FAIL)

VDS üzerinde kapsamlı API testi yapıldı. Test scripti: `deploy/test-all-endpoints.sh`

**Çalışan her şey (68/78):**
- ✅ Login/Register/Logout akışı
- ✅ CSRF token koruması
- ✅ Tüm korumalı endpoint'lerde auth redirect (302)
- ✅ Dashboard, Customer, Invoice, Role, User, RoleClaim sayfaları
- ✅ Customer CRUD (Create/Read/Update/Delete + soft delete)
- ✅ Permission CRUD (Create/Read/Update/Delete)
- ✅ Role CRUD (Create/Read/Update/Delete)
- ✅ Tüm GetAll API'leri (valid JSON)
- ✅ Tüm Count API'leri
- ✅ 13 static file (Velzon CSS/JS/images, Bootstrap, jQuery)
- ✅ SQLite veritabanı ve migration'lar
- ✅ Callback HMAC doğrulaması (geçersiz → 403)
- ✅ Callback test endpoint Production'da kapalı (404)

---

## Bilinen Sorunlar ve Kalan İşler

### Düzeltilmemiş Sorunlar

#### 1. Eksik View Dosyaları (3 adet)

| View | Controller Metodu | Hata |
|------|-------------------|------|
| `Views/Auth/TwoFactor.cshtml` | `AuthController.TwoFactor()` | 2FA giriş sayfası view'ı hiç oluşturulmamış |
| `Views/Auth/TwoFactorEnabled.cshtml` | `AuthController.TwoFactorEnabled()` | 2FA aktif bilgi sayfası view'ı yok |
| `Views/Permission/PermissionList.cshtml` | `PermissionController.PermissionList()` | Yetki listesi view'ı yok |

**Etki:** Bu sayfalara gidildiğinde 500 Internal Server Error döner.
**Çözüm:** İlgili `.cshtml` dosyalarının oluşturulması gerekiyor.

#### 2. Olmayan Kayıt İçin Exception Fırlatma

- **Endpoint'ler:** `GET /pay/{id}`, `GET /result-invoice/{id}`, `GET /api/invoices/status/{id}`
- **Hata:** Olmayan fatura ID'si verildiğinde `KeyNotFoundException` fırlatılıyor
- **Beklenen:** 404 Not Found dönmeli
- **Etki:** 500 Internal Server Error döner
- **Çözüm:** Controller'larda null kontrolü yapılıp 404 dönülmeli

#### 3. Hardcoded API Key ve SMTP Şifresi

- Plisio API key ve SMTP şifresi `appsettings.json` içinde hardcoded
- **Not:** Kullanıcı talebiyle bu düzeltme kasıtlı olarak atlandı
- **Çözüm:** Environment variable'lara taşınmalı

### Yapılması Gereken İyileştirmeler

1. **HTTPS/SSL sertifikası** — Şu an HTTP üzerinden çalışıyor, Let's Encrypt ile SSL eklenebilir
2. **Nginx reverse proxy** — Doğrudan Kestrel yerine Nginx arkasında çalıştırılmalı
3. **Veritabanı yedekleme** — Otomatik SQLite backup cron job'ı kurulmalı
4. **Loglama** — Serilog veya benzeri structured logging eklenebilir
5. **Rate limiting** — API endpoint'lerine rate limit eklenebilir
6. **Fatura düzenleme** — Mevcut fatura düzenleme UI'ı yok
7. **Dashboard toplam tutar** — Dashboard'daki "Toplam Tutar" kartı henüz 0 gösteriyor, hesaplama eklenmeli
8. **Fatura PDF indirme** — InvoiceDetail'daki PDF indirme `html2canvas` + `jsPDF` kullanıyor ama bu kütüphaneler layout'a dahil edilmemiş olabilir

---

## VDS Deployment (Ubuntu 22.04)

### Sunucu Bilgileri

| Özellik | Değer |
|---------|-------|
| İşletim Sistemi | Ubuntu 22.04 LTS |
| CPU | 2 vCPU |
| RAM | 2 GB |
| Disk | 30 GB |
| Uygulama Portu | 5000 |
| Veritabanı | SQLite |
| .NET Runtime | ASP.NET Core 8.0 |
| Systemd Servisi | `cryptopayment` |

### Sunucudaki Diğer Servisler

| Port | Servis | Not |
|------|--------|-----|
| 22 | SSH | Uzak erişim |
| 5000 | Crypto Payment | Bu uygulama |
| 8080 | Python Bot | Dokunulmadı |
| 8899 | Python Bot | Dokunulmadı |

### Hızlı Deploy (Windows → VDS)

```powershell
# 1. Publish
cd Crypto_Payment/Crypto_Payment
dotnet publish -c Release -o ./publish --no-self-contained -r linux-x64

# 2. Tar oluştur
cd ../..
tar -czf publish.tar.gz -C Crypto_Payment/Crypto_Payment/publish .

# 3. VDS'e gönder
pscp -batch publish.tar.gz root@<VDS_IP>:/tmp/publish.tar.gz

# 4. VDS'de deploy (servis durdur, db yedekle, temizle, aç, db geri yükle, başlat)
plink -ssh -l root -batch <VDS_IP> "systemctl stop cryptopayment && cp /var/www/cryptopayment/invoice.db /tmp/invoice.db.bak 2>/dev/null; rm -rf /var/www/cryptopayment/* && tar -xzf /tmp/publish.tar.gz -C /var/www/cryptopayment/ && cp /tmp/invoice.db.bak /var/www/cryptopayment/invoice.db 2>/dev/null; chmod +x /var/www/cryptopayment/Crypto_Payment && systemctl start cryptopayment"
```

### Deployment Adımları (İlk Kurulum)

#### 1. .NET 8 Runtime Kurulumu
```bash
wget https://packages.microsoft.com/config/ubuntu/22.04/packages-microsoft-prod.deb -O packages-microsoft-prod.deb
dpkg -i packages-microsoft-prod.deb
apt update
apt install -y aspnetcore-runtime-8.0
```

#### 2. Uygulama Publish (Windows'ta)
```powershell
cd Crypto_Payment/Crypto_Payment
dotnet publish -c Release -o ./publish --no-self-contained -r linux-x64
```

#### 3. Dosya Transferi (Windows → VDS)
```powershell
# Ana dosyalar
& "C:\Program Files\PuTTY\pscp.exe" -pw "SIFRE" -batch -r "publish/*" "root@185.7.243.141:/var/www/cryptopayment/"

# wwwroot alt klasörleri ayrı ayrı gönderildi (pscp timeout sorunu nedeniyle):
# - wwwroot/css/
# - wwwroot/js/
# - wwwroot/lib/ (bootstrap, jquery, jquery-validation, jquery-validation-unobtrusive)
# - wwwroot/Identity/
# - wwwroot/admin/velzon-dist/ (241 MB, 2153+ dosya)
# - wwwroot/favicon.ico
# - wwwroot/Crypto_Payment.styles.css
```

#### 4. Systemd Servisi
```ini
# /etc/systemd/system/cryptopayment.service
[Unit]
Description=Crypto Payment Web App
After=network.target

[Service]
WorkingDirectory=/var/www/cryptopayment
ExecStart=/usr/bin/dotnet /var/www/cryptopayment/Crypto_Payment.dll
Restart=always
RestartSec=10
SyslogIdentifier=cryptopayment
User=root
Environment=ASPNETCORE_ENVIRONMENT=Production
Environment=ASPNETCORE_URLS=http://0.0.0.0:5000

[Install]
WantedBy=multi-user.target
```

```bash
systemctl daemon-reload
systemctl enable cryptopayment
systemctl start cryptopayment
```

#### 5. Kullanıcı Oluşturma
```bash
# register-user.sh scripti ile:
# 1. Register sayfasından CSRF token alınır
# 2. POST ile kullanıcı kaydedilir
# 3. SQLite'ta EmailConfirmed = 1 yapılır
bash /tmp/register-user.sh
```

**Test Kullanıcısı:**
| Alan | Değer |
|------|-------|
| Email | `admin@crypto.com` |
| Şifre | `Admin123!` |
| Ad | Admin User |

### Deployment Sırasında Karşılaşılan Sorunlar

1. **pscp timeout:** Velzon admin teması 241 MB, 3850+ dosya. İlk transferde timeout oldu. Parça parça gönderildi.
2. **pscp klasör oluşturma:** pscp -r hedef klasörü otomatik oluşturmuyor. `mkdir -p` ile önceden oluşturuldu.
3. **wwwroot/lib eksik:** İlk transferde lib alt klasörleri gitmedi. Her alt klasör (bootstrap/dist/css, bootstrap/dist/js, jquery/dist vb.) ayrı ayrı gönderildi.
4. **CSRF token:** Register ve Login endpoint'leri `[ValidateAntiForgeryToken]` kullanıyor. curl ile doğrudan POST yapılamıyor. Önce GET ile token alınıp, cookie ile birlikte POST yapıldı.
5. **System.Drawing Linux:** QR kod üretimi Windows-only `System.Drawing.Common` kullanıyor. Linux'ta çalışmıyor.

### Sunucu Kaynak Kullanımı

```
Uygulama RAM: ~131 MB
Toplam Kullanılan RAM: ~610 MB
Kullanılabilir RAM: ~1.1 GB
Swap Kullanımı: 31 MB / 2 GB
```

### Faydalı Komutlar

```bash
# Servis durumu
systemctl status cryptopayment

# Logları görüntüle
journalctl -u cryptopayment --no-pager -n 50

# Servisi yeniden başlat
systemctl restart cryptopayment

# Servisi durdur
systemctl stop cryptopayment

# Veritabanı kontrol
sqlite3 /var/www/cryptopayment/invoice.db ".tables"
sqlite3 /var/www/cryptopayment/invoice.db "SELECT * FROM AspNetUsers;"

# Disk kullanımı
du -sh /var/www/cryptopayment/
```

---

## Yerel Geliştirme

### Gereksinimler
- .NET 8.0 SDK
- SQLite (otomatik oluşturulur)

### Kurulum
```bash
git clone https://github.com/ridvanakyil16/Crypto_Payment.git
cd Crypto_Payment/Crypto_Payment
dotnet restore
dotnet run
```

Uygulama `http://localhost:5156` adresinde çalışır.

### Veritabanı

- Uygulama başlatıldığında migration'lar otomatik uygulanır (`db.Database.Migrate()`)
- `DATABASE_URL` environment variable tanımlıysa PostgreSQL kullanılır
- Tanımlı değilse SQLite (`invoice.db`) kullanılır

### Test

```bash
cd Crypto_Payment
dotnet test
```

---

## Veritabanı Şeması

### Tablolar

| Tablo | Açıklama |
|-------|----------|
| `AspNetUsers` | Kullanıcılar (Identity) |
| `AspNetRoles` | Roller (Identity) |
| `AspNetUserRoles` | Kullanıcı-Rol eşleme |
| `AspNetRoleClaims` | Rol yetkileri |
| `AspNetUserClaims` | Kullanıcı yetkileri |
| `AspNetUserLogins` | Harici login'ler |
| `AspNetUserTokens` | Kullanıcı token'ları |
| `Customers` | Müşteriler |
| `Invoices` | Faturalar |
| `InvoiceItems` | Fatura kalemleri |
| `Permissions` | Yetkiler (self-referencing) |

### Önemli Alanlar

**Customers:**
- `FirstName`, `LastName`, `Phone` — zorunlu
- `Email`, `CompanyName`, `Telegram`, `Skype` — opsiyonel
- `IsActive` — soft delete (false = silinmiş)

**Invoices:**
- `CustomerId` — müşteri ilişkisi (indexed)
- `TxnId` — Plisio transaction ID
- `Status` — ödeme durumu (new, pending, completed, expired, cancelled vb.)
- `IsActive` — soft delete
- `CryptoAmount` — kripto tutar (decimal 18,8)

**InvoiceItems:**
- `InvoiceId` — fatura ilişkisi
- `Total` — kalem tutarı (decimal 18,8)

**Permissions:**
- `TopPermissionId` — üst yetki (self-referencing, indexed)
- `TopPermission` — navigation property

---

## Güvenlik Özellikleri

1. **Kimlik Doğrulama:** ASP.NET Core Identity ile login/register
2. **CSRF Koruması:** Tüm form POST'larında `[ValidateAntiForgeryToken]`
3. **Yetkilendirme:** Korumalı endpoint'lerde `[Authorize]` attribute
4. **HMAC-SHA1:** Plisio callback endpoint'inde imza doğrulaması
5. **Email Doğrulama:** Kayıt sonrası email onayı gerekli
6. **Hesap Kilitleme:** 5 başarısız giriş → 5 dakika kilitleme
7. **2FA Desteği:** TOTP tabanlı iki faktörlü kimlik doğrulama
8. **Soft Delete:** Kayıtlar fiziksel olarak silinmez, `IsActive = false` yapılır
9. **Global Query Filter:** Silinen kayıtlar otomatik filtrelenir

---

## Proje Geçmişi (Kronolojik)

| Adım | İşlem | Sonuç |
|------|-------|-------|
| 1 | Repo klonlama ve analiz | Mimari, güvenlik, kod kalitesi analizi tamamlandı |
| 2 | Localhost'ta çalıştırma | .NET 8 SDK kuruldu, uygulama localhost:5156'da çalıştı |
| 3 | Admin kullanıcı oluşturma | `admin@crypto.com` / `Admin123!` ile kayıt + email onayı |
| 4 | Sistem analizi | Ödeme akışı, mimari, bileşenler dokümante edildi |
| 5 | Self-hosted API araştırması | Plisio yerine kendi API kurma seçenekleri araştırıldı |
| 6 | Tur 1 düzeltmeler | 7 görev: nullable fields, decimal, soft delete, testler |
| 7 | Detaylı veritabanı analizi | 20 sorun tespit edildi |
| 8 | Tur 2 düzeltmeler | 11 görev: HMAC, auth, indexes, N+1, migration |
| 9 | Tur 3 düzeltmeler | 14 ek sorun düzeltildi |
| 10 | Tur 4 düzeltmeler | 9 son sorun: route fix, async, dead code temizliği |
| 11 | VDS deployment | Ubuntu 22.04'e deploy: .NET runtime, publish, transfer, systemd |
| 12 | VDS test | 78 endpoint testi: 68 PASS, 10 FAIL |
| 13 | Tur 5 düzeltmeler | QR kod Linux uyumluluğu, Payment retry, hata yönetimi |
| 14 | Tur 6 düzeltmeler | 7 frontend bug: modal, total parse, login, customer form |
| 15 | Tur 7 düzeltmeler | Route ambiguity, ExceptionHandler path düzeltmesi |
| 16 | Son deploy ve doğrulama | Tüm endpoint'ler 200 OK, sistem çalışır durumda |

---

## Lisans

Bu proje özel kullanım içindir.
