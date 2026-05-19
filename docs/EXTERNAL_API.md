# Harici Site Ödeme API'si

Başka web sitelerinin Crypto Payment altyapısını kullanarak kripto ödeme alması için REST API.

## Kimlik Doğrulama

Her istekte API anahtarı gönderin:

```http
X-Api-Key: cp_live_xxxxxxxxxxxxxxxx
```

veya

```http
Authorization: Bearer cp_live_xxxxxxxxxxxxxxxx
```

API anahtarı yalnızca **MasterAdmin** panel kullanıcısı tarafından oluşturulur.

## API anahtarı oluşturma (MasterAdmin)

Giriş yapmış MasterAdmin oturumu ile:

```http
POST /api/admin/api-clients
Content-Type: application/json
Cookie: (Identity cookie)

{
  "name": "Benim E-Ticaret Sitem",
  "defaultWebhookUrl": "https://example.com/webhooks/payment"
}
```

Yanıt (tek seferlik — anahtarı kaydedin):

```json
{
  "id": 1,
  "name": "Benim E-Ticaret Sitem",
  "apiKey": "cp_live_...",
  "webhookSecret": "abc123...",
  "defaultWebhookUrl": "https://example.com/webhooks/payment",
  "apiKeyPrefix": "cp_live_xxxx…",
  "message": "API anahtarını güvenli bir yerde saklayın; tekrar gösterilmez."
}
```

## Ödeme oluşturma

```http
POST /api/v1/payments
X-Api-Key: cp_live_...
Content-Type: application/json

{
  "amount": 99.99,
  "sourceCurrency": "USD",
  "cryptoCurrency": "USDT_TRX",
  "email": "musteri@example.com",
  "orderNumber": "ORD-2026-001",
  "orderName": "Premium Paket",
  "externalReference": "cart-uuid-123",
  "webhookUrl": "https://example.com/webhooks/payment",
  "items": [
    { "description": "Premium Paket", "quantity": 1, "unitPrice": 99.99 }
  ]
}
```

Yanıt:

```json
{
  "paymentId": 42,
  "orderNumber": "ORD-2026-001",
  "externalReference": "cart-uuid-123",
  "txnId": "plisio-txn-id",
  "status": "new",
  "paymentUrl": "https://payment.wanda.to/pay/42?txnId=...",
  "amount": 99.99,
  "sourceCurrency": "USD",
  "cryptoCurrency": "USDT_TRX",
  "createdAt": "2026-05-19T12:00:00Z"
}
```

Müşteriyi `paymentUrl` adresine yönlendirin.

## Durum sorgulama

```http
GET /api/v1/payments/42
X-Api-Key: cp_live_...
```

```http
GET /api/v1/payments/by-order/ORD-2026-001
X-Api-Key: cp_live_...
```

## Webhook (sizin sitenize)

Ödeme durumu değişince `webhookUrl` veya ApiClient `defaultWebhookUrl` adresine **POST** atılır.

Header:

- `X-Signature-SHA256`: HMAC-SHA256 (hex) — body + `webhookSecret`
- `X-Event-Type`: `payment.completed`, `payment.pending`, `payment.expired`, …

Body örneği:

```json
{
  "event": "payment.completed",
  "payment_id": 42,
  "order_number": "ORD-2026-001",
  "external_reference": "cart-uuid-123",
  "txn_id": "...",
  "previous_status": "pending",
  "status": "completed",
  "amount": 99.99,
  "source_currency": "USD",
  "crypto_currency": "USDT_TRX",
  "transaction_id": "blockchain-tx",
  "timestamp": "2026-05-19T12:05:00Z"
}
```

Doğrulama (Node.js örneği):

```javascript
const crypto = require("crypto");

function verifyWebhook(rawBody, signatureHeader, webhookSecret) {
  const expected = crypto
    .createHmac("sha256", webhookSecret)
    .update(rawBody, "utf8")
    .digest("hex");
  return crypto.timingSafeEqual(
    Buffer.from(expected),
    Buffer.from(signatureHeader.toLowerCase())
  );
}
```

## Hata kodları

| HTTP | Açıklama |
|------|----------|
| 401 | API anahtarı eksik veya geçersiz |
| 404 | Ödeme bulunamadı |
| 422 | İş kuralı (ör. duplicate order_number) |
| 429 | Rate limit (60 istek/dk) |

## Ortam değişkenleri

| Değişken | Açıklama |
|----------|----------|
| `APP_BASE_URL` | `paymentUrl` üretimi (örn. `https://payment.wanda.to`) |
