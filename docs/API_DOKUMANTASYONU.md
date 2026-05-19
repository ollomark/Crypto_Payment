# Crypto Payment — Harici Site API Dokümantasyonu

**Versiyon:** 1.0  
**Son güncelleme:** Mayıs 2026  
**Format:** REST JSON  
**Üretim tabanı:** `https://payment.wanda.to`

Bu API, kendi web sitenizden veya uygulamanızdan Crypto Payment altyapısı ile **kripto para ödemesi** oluşturmanızı, durum takibi yapmanızı ve ödeme sonuçlarını **webhook** ile almanızı sağlar.

---

## İçindekiler

1. [Hızlı başlangıç](#hızlı-başlangıç)
2. [Kimlik doğrulama](#kimlik-doğrulama)
3. [API anahtarı alma](#api-anahtarı-alma)
4. [Uç noktalar](#uç-noktalar)
5. [Ödeme durumları](#ödeme-durumları)
6. [Webhook](#webhook)
7. [Hata yönetimi](#hata-yönetimi)
8. [Limitler ve güvenlik](#limitler-ve-güvenlik)
9. [Entegrasyon akışı](#entegrasyon-akışı)
10. [Kod örnekleri](#kod-örnekleri)

---

## Hızlı başlangıç

```text
1. Panelden API anahtarı oluştur  →  cp_live_...
2. POST /api/v1/payments          →  paymentUrl al
3. Müşteriyi paymentUrl'e yönlendir
4. Webhook veya GET ile durumu kontrol et
```

**Örnek — ödeme oluşturma:**

```bash
curl -X POST "https://payment.wanda.to/api/v1/payments" \
  -H "Content-Type: application/json" \
  -H "X-Api-Key: cp_live_BURAYA_ANAHTARINIZ" \
  -d '{
    "amount": 49.99,
    "sourceCurrency": "USD",
    "cryptoCurrency": "USDT_TRX",
    "email": "musteri@example.com",
    "orderNumber": "ORD-2026-00042",
    "orderName": "Premium Üyelik"
  }'
```

**Örnek yanıt:**

```json
{
  "paymentId": 128,
  "orderNumber": "ORD-2026-00042",
  "externalReference": null,
  "txnId": "65f1a2b3c4d5e6f7",
  "status": "new",
  "paymentUrl": "https://payment.wanda.to/pay/128?txnId=65f1a2b3c4d5e6f7",
  "amount": 49.99,
  "sourceCurrency": "USD",
  "cryptoCurrency": "USDT_TRX",
  "createdAt": "2026-05-19T14:30:00Z"
}
```

Müşteriyi `paymentUrl` adresine yönlendirin. Ödeme tamamlandığında webhook veya durum sorgusu ile bilgilendirilirsiniz.

---

## Kimlik doğrulama

Tüm `/api/v1/payments` istekleri **API anahtarı** gerektirir.

### Yöntem 1 — Header (önerilen)

```http
X-Api-Key: cp_live_xxxxxxxxxxxxxxxxxxxxxxxx
```

### Yöntem 2 — Bearer token

```http
Authorization: Bearer cp_live_xxxxxxxxxxxxxxxxxxxxxxxx
```

### Kurallar

| Kural | Açıklama |
|-------|----------|
| Anahtar formatı | `cp_live_` ile başlamalı |
| Saklama | Sunucu tarafında ortam değişkeni; asla frontend’e koymayın |
| Geçersiz anahtar | `401 Unauthorized` |
| Pasif istemci | `401 Unauthorized` |

---

## API anahtarı alma

API anahtarları yalnızca **MasterAdmin** kullanıcıları panel üzerinden oluşturur.

| Adım | İşlem |
|------|--------|
| 1 | `https://payment.wanda.to` adresine MasterAdmin ile giriş yapın |
| 2 | **Ayarlar → API Anahtarları** menüsüne gidin (`/api-clients`) |
| 3 | **Yeni API Anahtarı** ile site adı ve (isteğe bağlı) webhook URL girin |
| 4 | Açılan pencerede **API Key** ve **Webhook Secret** değerlerini kopyalayın |

> **Uyarı:** API anahtarı yalnızca oluşturulduğu anda gösterilir. Kaybederseniz panelden **Anahtarı yenile** ile yeni anahtar üretin; eski anahtar anında geçersiz olur.

### Programatik oluşturma (MasterAdmin oturumu)

Panel cookie’si ile:

```http
POST /api/admin/api-clients
Content-Type: application/json
Cookie: .AspNetCore.Identity.Application=...

{
  "name": "Benim Mağazam",
  "defaultWebhookUrl": "https://magaza.com/api/payment-webhook"
}
```

**Yanıt (201 / 200):**

```json
{
  "id": 3,
  "name": "Benim Mağazam",
  "apiKey": "cp_live_...",
  "webhookSecret": "a1b2c3d4e5f6...",
  "defaultWebhookUrl": "https://magaza.com/api/payment-webhook",
  "apiKeyPrefix": "cp_live_xxxx…",
  "message": "API anahtarını güvenli bir yerde saklayın; tekrar gösterilmez."
}
```

---

## Uç noktalar

Temel yol: `{BASE_URL}/api/v1/payments`

| Metot | Yol | Açıklama |
|--------|-----|----------|
| `POST` | `/api/v1/payments` | Yeni ödeme oluştur |
| `GET` | `/api/v1/payments/{paymentId}` | ID ile durum sorgula |
| `GET` | `/api/v1/payments/by-order/{orderNumber}` | Sipariş numarası ile sorgula |

---

### POST — Ödeme oluştur

**`POST /api/v1/payments`**

Yeni bir kripto ödeme kaydı açar ve Plisio üzerinden ödeme sayfası URL’i döner.

#### İstek gövdesi

| Alan | Tip | Zorunlu | Açıklama |
|------|-----|---------|----------|
| `amount` | number | Evet | Tahsil edilecek tutar (min `0.01`) |
| `sourceCurrency` | string | Evet | Fiat para birimi: `USD`, `EUR`, `EURO`, `TRY`, `TL` |
| `cryptoCurrency` | string | Evet | Plisio kripto kodu (aşağıya bakın) |
| `email` | string | Evet | Müşteri e-postası |
| `orderNumber` | string | Evet | **Sizin** benzersiz sipariş numaranız (aynı API istemcisinde tekrarlanamaz) |
| `orderName` | string | Hayır | Görünen sipariş adı; boşsa `orderNumber` kullanılır |
| `externalReference` | string | Hayır | Kendi sisteminizdeki referans ID (sepet, kullanıcı vb.) |
| `webhookUrl` | string | Hayır | Bu ödeme için özel webhook; boşsa istemci varsayılanı |
| `customerId` | integer | Hayır | Panelde kayıtlı müşteri ID (varsa) |
| `items` | array | Hayır | Fatura kalemleri (aşağıya bakın) |

**`items[]` elemanı:**

| Alan | Tip | Zorunlu | Açıklama |
|------|-----|---------|----------|
| `description` | string | Evet | Kalem açıklaması |
| `quantity` | integer | Hayır | Adet (varsayılan: `1`) |
| `unitPrice` | number | Evet | Birim fiyat |

> `items` gönderirseniz `quantity × unitPrice` toplamı `amount` ile uyumlu olmalıdır (±0.01 tolerans).

#### Desteklenen `cryptoCurrency` değerleri (örnek)

| Kod | Açıklama |
|-----|----------|
| `USDT_TRX` | USDT (TRC20) — önerilen |
| `TRX` | Tron |
| `BTC` | Bitcoin |
| `ETH` | Ethereum |

> Tam liste Plisio hesabınıza bağlıdır. Panelde fatura oluştururken görünen kodları kullanın.

#### Başarılı yanıt — `200 OK`

```json
{
  "paymentId": 128,
  "orderNumber": "ORD-2026-00042",
  "externalReference": "cart-9f3a2b",
  "txnId": "65f1a2b3c4d5e6f7",
  "status": "new",
  "paymentUrl": "https://payment.wanda.to/pay/128?txnId=65f1a2b3c4d5e6f7",
  "amount": 49.99,
  "sourceCurrency": "USD",
  "cryptoCurrency": "USDT_TRX",
  "createdAt": "2026-05-19T14:30:00Z"
}
```

| Alan | Açıklama |
|------|----------|
| `paymentId` | Sistemdeki fatura/ödeme ID |
| `txnId` | Plisio işlem ID (ödeme sayfası ve sorgu için) |
| `paymentUrl` | Müşterinin yönlendirileceği ödeme sayfası |
| `status` | İlk durum genelde `new` |

#### Hata yanıtları

| HTTP | Gövde örneği | Sebep |
|------|----------------|-------|
| `400` | ASP.NET validation | Eksik/hatalı alan |
| `401` | — | API anahtarı yok/geçersiz |
| `422` | `{ "message": "Bu order_number zaten kayıtlı: ..." }` | Yinelenen sipariş no. |
| `422` | `{ "message": "Plisio ..." }` | Ödeme sağlayıcı hatası |
| `429` | — | Rate limit aşıldı |

---

### GET — Ödeme durumu (ID)

**`GET /api/v1/payments/{paymentId}`**

Yalnızca **sizin API istemcinizle** oluşturulmuş ödemeleri döner.

#### Başarılı yanıt — `200 OK`

```json
{
  "paymentId": 128,
  "orderNumber": "ORD-2026-00042",
  "externalReference": "cart-9f3a2b",
  "txnId": "65f1a2b3c4d5e6f7",
  "status": "completed",
  "transactionId": "9abc...def blockchain tx hash",
  "amount": 49.99,
  "sourceCurrency": "USD",
  "cryptoCurrency": "USDT_TRX",
  "createdAt": "2026-05-19T14:30:00Z",
  "updatedAt": null
}
```

#### Hata

| HTTP | Açıklama |
|------|----------|
| `404` | `{ "message": "Ödeme bulunamadı." }` |

---

### GET — Ödeme durumu (sipariş numarası)

**`GET /api/v1/payments/by-order/{orderNumber}`**

`orderNumber` URL-encoded olmalıdır (ör. `ORD%2F001`).

Yanıt formatı `GET /api/v1/payments/{id}` ile aynıdır.

---

## Ödeme durumları

| `status` | Anlam | Son durum? |
|----------|--------|------------|
| `new` | Ödeme kaydı oluşturuldu | Hayır |
| `pending` | Müşteri ödeme bekleniyor / işlemde | Hayır |
| `preparing` | Cüzdan/QR hazırlanıyor | Hayır |
| `completed` | Ödeme başarılı | **Evet** |
| `mismatch` | Tutar uyuşmazlığı (kısmi/fazla) — yine de tahsil sayılabilir | **Evet** |
| `expired` | Süre doldu | **Evet** |
| `cancelled` | İptal | **Evet** |
| `error` | Hata | **Evet** |

**Entegrasyon önerisi:**

- Siparişi “ödendi” saymak için: `completed` veya `mismatch`
- `cancelled` ve `expired` için siparişi iptal / yeniden ödeme sun
- Webhook + periyodik `GET` sorgusu birlikte kullanılabilir (webhook birincil, polling yedek)

---

## Webhook

Ödeme **durumu değiştiğinde** sisteminizdeki URL’e `POST` isteği gönderilir.

### Webhook URL önceliği

1. Ödeme oluştururken gönderilen `webhookUrl`
2. API istemcisinin `defaultWebhookUrl` (panelden tanımlı)
3. Tanımlı değilse webhook gönderilmez

### İstek formatı

```http
POST https://siteniz.com/webhooks/payment
Content-Type: application/json
X-Signature-SHA256: 8f3c2a1b...
X-Event-Type: payment.completed
User-Agent: CryptoPayment-Webhook/1.0
```

**Gövde:**

```json
{
  "event": "payment.completed",
  "payment_id": 128,
  "order_number": "ORD-2026-00042",
  "external_reference": "cart-9f3a2b",
  "txn_id": "65f1a2b3c4d5e6f7",
  "previous_status": "pending",
  "status": "completed",
  "amount": 49.99,
  "source_currency": "USD",
  "crypto_currency": "USDT_TRX",
  "transaction_id": "9abc...def",
  "timestamp": "2026-05-19T14:35:22.1234567Z"
}
```

### `event` değerleri

| `event` | Tetikleyen `status` |
|---------|---------------------|
| `payment.completed` | `completed`, `mismatch` |
| `payment.pending` | `new`, `pending` |
| `payment.expired` | `expired` |
| `payment.cancelled` | `cancelled` |
| `payment.updated` | Diğer geçişler |

### İmza doğrulama (zorunlu)

Ham JSON gövdesi üzerinden **HMAC-SHA256** hesaplanır; sonuç **küçük harf hex** olarak `X-Signature-SHA256` header’ında gelir.

**Anahtar:** Panelde oluşturulan `webhookSecret`

#### Node.js

```javascript
const crypto = require("crypto");

function verifyWebhook(rawBody, signatureHeader, webhookSecret) {
  const expected = crypto
    .createHmac("sha256", webhookSecret)
    .update(rawBody, "utf8")
    .digest("hex");

  const a = Buffer.from(expected, "utf8");
  const b = Buffer.from((signatureHeader || "").toLowerCase(), "utf8");
  if (a.length !== b.length) return false;
  return crypto.timingSafeEqual(a, b);
}

// Express örneği — raw body gerekir
app.post("/webhooks/payment", express.raw({ type: "application/json" }), (req, res) => {
  const sig = req.headers["x-signature-sha256"];
  if (!verifyWebhook(req.body.toString("utf8"), sig, process.env.WEBHOOK_SECRET)) {
    return res.status(401).send("Invalid signature");
  }
  const payload = JSON.parse(req.body);
  // payload.order_number ile siparişinizi güncelleyin
  res.status(200).json({ received: true });
});
```

#### PHP

```php
<?php
$rawBody = file_get_contents('php://input');
$signature = $_SERVER['HTTP_X_SIGNATURE_SHA256'] ?? '';
$secret = getenv('WEBHOOK_SECRET');

$expected = hash_hmac('sha256', $rawBody, $secret);
if (!hash_equals($expected, strtolower($signature))) {
    http_response_code(401);
    exit('Invalid signature');
}

$payload = json_decode($rawBody, true);
// $payload['order_number'] ile sipariş güncelle
http_response_code(200);
echo json_encode(['received' => true]);
```

#### Python

```python
import hmac
import hashlib

def verify_webhook(raw_body: bytes, signature: str, secret: str) -> bool:
    expected = hmac.new(secret.encode(), raw_body, hashlib.sha256).hexdigest()
    return hmac.compare_digest(expected, (signature or "").lower())

# Flask
@app.post("/webhooks/payment")
def payment_webhook():
    raw = request.get_data()
    sig = request.headers.get("X-Signature-SHA256", "")
    if not verify_webhook(raw, sig, os.environ["WEBHOOK_SECRET"]):
        return "", 401
    payload = request.get_json()
    return {"received": True}, 200
```

### Webhook yanıtı

| Sizin yanıtınız | Davranış |
|-----------------|----------|
| `2xx` | Başarılı kabul |
| Diğer | Sunucu loglarına yazılır; otomatik yeniden deneme garantisi yok — yedek olarak `GET` sorgusu kullanın |

---

## Hata yönetimi

### HTTP özet tablosu

| Kod | Anlam |
|-----|--------|
| `200` | Başarılı |
| `400` | Geçersiz istek gövdesi |
| `401` | Kimlik doğrulama hatası |
| `404` | Kayıt bulunamadı |
| `422` | İş kuralı ihlali |
| `429` | Çok fazla istek |
| `500` | Sunucu hatası |

### Hata gövdesi formatı

```json
{
  "message": "İnsan tarafından okunabilir açıklama"
}
```

ASP.NET validation hatalarında ek alanlar gelebilir:

```json
{
  "type": "https://tools.ietf.org/html/rfc9110#section-15.5.1",
  "title": "One or more validation errors occurred.",
  "status": 400,
  "errors": {
    "Email": ["Geçerli bir e-posta adresi giriniz."]
  }
}
```

---

## Limitler ve güvenlik

| Konu | Değer |
|------|--------|
| Rate limit | **60 istek / dakika** / API anahtarı (`external-api`) |
| HTTPS | Üretimde zorunlu |
| Idempotency | Aynı `orderNumber` ile ikinci `POST` → `422` |
| Veri izolasyonu | Bir API anahtarı yalnızca kendi ödemelerini görür |

**Güvenlik kontrol listesi:**

- [ ] API anahtarını yalnızca backend’de saklayın  
- [ ] Webhook imzasını her istekte doğrulayın  
- [ ] `order_number` + `payment_id` eşlemesini kendi veritabanınızda tutun  
- [ ] `completed` webhook’unu idempotent işleyin (aynı ödeme iki kez işlenmesin)  

---

## Entegrasyon akışı

```text
┌─────────────┐     POST /api/v1/payments      ┌──────────────────┐
│  Sizin site │ ─────────────────────────────► │ Crypto Payment   │
│  (backend)  │ ◄───────────────────────────── │ API              │
└─────────────┘     paymentUrl, paymentId      └────────┬─────────┘
       │                                                  │
       │ redirect                                         │ Plisio
       ▼                                                  ▼
┌─────────────┐                              ┌──────────────────┐
│  Müşteri    │ ◄── paymentUrl ──────────────│ Ödeme sayfası    │
│  tarayıcı   │ ─── kripto öder ────────────►│ /pay/{id}        │
└─────────────┘                              └────────┬─────────┘
       ▲                                              │
       │         POST webhook (signed)                │ status change
       └──────────────────────────────────────────────┘
                 Sizin webhook endpoint’iniz
```

**Önerilen sipariş yaşam döngüsü:**

1. Sipariş oluştur → durum: `awaiting_payment`  
2. `POST /api/v1/payments` → `paymentId` ve `paymentUrl` kaydet  
3. Müşteriyi `paymentUrl`’e yönlendir  
4. Webhook `payment.completed` → sipariş: `paid`  
5. (Yedek) Her 5–10 sn `GET /api/v1/payments/{id}` ile polling — webhook gelmezse  

---

## Kod örnekleri

### PHP — ödeme oluştur ve yönlendir

```php
<?php
$apiKey = getenv('CRYPTO_PAYMENT_API_KEY');
$baseUrl = 'https://payment.wanda.to';

$payload = [
    'amount' => 99.99,
    'sourceCurrency' => 'USD',
    'cryptoCurrency' => 'USDT_TRX',
    'email' => $customerEmail,
    'orderNumber' => 'ORD-' . $orderId,
    'orderName' => 'Sipariş #' . $orderId,
    'externalReference' => (string) $orderId,
    'webhookUrl' => 'https://example.com/webhooks/crypto-payment',
];

$ch = curl_init($baseUrl . '/api/v1/payments');
curl_setopt_array($ch, [
    CURLOPT_POST => true,
    CURLOPT_RETURNTRANSFER => true,
    CURLOPT_HTTPHEADER => [
        'Content-Type: application/json',
        'X-Api-Key: ' . $apiKey,
    ],
    CURLOPT_POSTFIELDS => json_encode($payload),
]);

$response = curl_exec($ch);
$httpCode = curl_getinfo($ch, CURLINFO_HTTP_CODE);
curl_close($ch);

if ($httpCode !== 200) {
    throw new Exception('Payment API error: ' . $response);
}

$data = json_decode($response, true);
header('Location: ' . $data['paymentUrl']);
exit;
```

### JavaScript (Node.js) — durum sorgula

```javascript
async function getPaymentStatus(paymentId) {
  const res = await fetch(
    `https://payment.wanda.to/api/v1/payments/${paymentId}`,
    { headers: { "X-Api-Key": process.env.CRYPTO_PAYMENT_API_KEY } }
  );
  if (!res.ok) throw new Error(await res.text());
  return res.json();
}
```

### cURL — sipariş numarası ile sorgu

```bash
curl -s "https://payment.wanda.to/api/v1/payments/by-order/ORD-2026-00042" \
  -H "X-Api-Key: cp_live_BURAYA_ANAHTARINIZ"
```

---

## Sık sorulan sorular

**API anahtarını frontend’de kullanabilir miyim?**  
Hayır. Anahtar sunucu tarafında kalmalıdır; ödeme oluşturma işlemi kendi backend’inizden yapılmalıdır.

**Aynı sipariş için tekrar ödeme linki alabilir miyim?**  
Aynı `orderNumber` ile ikinci `POST` reddedilir (`422`). Yeni deneme için farklı `orderNumber` veya panelden işlem gerekir.

**Webhook gelmezse ne yapmalıyım?**  
`GET /api/v1/payments/{paymentId}` ile periyodik sorgulayın. Ödeme sayfası da arka planda durumu günceller.

**Test ortamı var mı?**  
Şu an ayrı sandbox anahtarı yok; düşük tutarlı gerçek işlem veya staging Heroku uygulaması kullanılabilir.

---

## Destek ve değişiklikler

| Kaynak | Konum |
|--------|--------|
| Panel — API anahtarları | `/api-clients` |
| Bu doküman | `docs/API_DOKUMANTASYONU.md` |
| Kısa referans | `docs/EXTERNAL_API.md` |

API değişiklikleri `feature/external-payment-api` branch’i ve GitHub `ollomark/Crypto_Payment` reposu üzerinden yayınlanır.

---

*Crypto Payment © Wandaplay — Harici Site API v1.0*
