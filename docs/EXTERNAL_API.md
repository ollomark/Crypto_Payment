# Harici Site API — Kısa Referans

> **Tam dokümantasyon:** [API_DOKUMANTASYONU.md](./API_DOKUMANTASYONU.md)

## Taban URL

```
https://payment.wanda.to
```

## Kimlik doğrulama

```http
X-Api-Key: cp_live_...
```

## Uç noktalar

| Metot | Yol |
|--------|-----|
| POST | `/api/v1/payments` |
| GET | `/api/v1/payments/{id}` |
| GET | `/api/v1/payments/by-order/{orderNumber}` |

## Panel

API anahtarı: **Ayarlar → API Anahtarları** (`/api-clients`) — MasterAdmin

## Webhook header

- `X-Signature-SHA256` — HMAC-SHA256 (hex), `webhookSecret` ile
- `X-Event-Type` — `payment.completed`, `payment.pending`, …

Detaylar, kod örnekleri ve hata kodları için [API_DOKUMANTASYONU.md](./API_DOKUMANTASYONU.md) dosyasına bakın.
