# Crypto Payment Sistemi — Tahsilat Mantığı ve Çalışma Özeti

Bu doküman, sistemin tahsilat (ödeme toplama) mantığını ve genel çalışma prensiplerini özetler. Sistemin tutarlı kalması için tüm kararlar bu mantığa dayanır.

---

## 1. Fatura Tipleri ve Kaynaklar

| Tip | Açıklama | Ödeme Kaynağı |
|-----|----------|---------------|
| **Tek seferlik** | Tek bir ödeme için oluşturulur | `Invoice.Status` veya tek bir `PaymentLink` |
| **Yinelenen (recurring)** | Aylık tekrarlayan ödemeler | Her ay için `PaymentLink` veya `Invoice` (ilk ay) |

---

## 2. Tahsilat Mantığı — "Ödendi" Kriteri

**Bir fatura/ay için "ödendi" sayılma koşulu:**

- `PaymentLink.Status` = `"completed"` veya `"mismatch"` olmalı
- `PaymentLink.Status` = `"cancelled"` ise **asla** ödenmiş sayılmaz

### Yinelenen faturalarda ay eşleştirme

Bir yinelenen fatura için X ayının ödenmiş sayılması:

1. **Manuel ödeme veya PaidForMonth/Year set edilmişse**
   - `PaidForMonth == X` ve `PaidForYear == hedef yıl` olan bir `PaymentLink` (completed/mismatch) olmalı

2. **Plisio/crypto ödeme (PaymentLink.PaidForMonth null)**
   - `CreatedDate` X ayına denk gelen bir `PaymentLink` (completed/mismatch) olmalı  
   - Yani `CreatedDate >= ay_başı` ve `CreatedDate < ay_sonu`

Bu mantık aşağıdaki yerlerde **aynı şekilde** uygulanır:

- `ManualPayment` — "Bu ay zaten ödenmiş" kontrolü
- `GetMonthlyHistory` — Aylık ödeme durumu zaman çizelgesi
- `OverdueInvoices` — Gecikmiş faturalar listesi
- `GetMonthlyStatsAsync` — Dashboard aylık istatistikleri (PaidForMonth/Year dahil)

---

## 3. Ödeme İptali Tutarlılığı

| Olay | Tek seferlik fatura | Yinelenen fatura |
|------|---------------------|------------------|
| Son/tek ödeme iptal edilirse | `Invoice.Status` → `"pending"` | Değişiklik yok (ay bazlı durum PaymentLink'lerden hesaplanır) |
| Başka tamamlanmış PaymentLink varsa | `Invoice.Status` değişmez | — |

Aylık ödeme durumunda:
- İlgili ay için **iptal edilmiş** (`cancelled`) PaymentLink varsa
- O ay **ödenmiş** gösterilmez (Invoice.Status completed olsa bile)

---

## 4. Genel Çalışma Akışı Özeti

```
┌─────────────────────────────────────────────────────────────────────┐
│ Fatura oluşturma (Plisio / Manuel)                                  │
│ → Tek seferlik veya Yinelenen                                         │
└─────────────────────────────────────────────────────────────────────┘
                                    │
                                    ▼
┌─────────────────────────────────────────────────────────────────────┐
│ Ödeme yolları                                                        │
│ • Plisio link → callback → PaymentLink.Status = completed             │
│ • Manuel ödeme → PaymentLink (IsManual, PaidForMonth/Year)           │
│ • Fatura direkt ödeme → Invoice.Status = completed (tek seferlik)     │
└─────────────────────────────────────────────────────────────────────┘
                                    │
                                    ▼
┌─────────────────────────────────────────────────────────────────────┐
│ Durum hesaplama (her yerde aynı)                                     │
│ • completed/mismatch → ödendi (cancelled hariç)                       │
│ • PaidForMonth/Year varsa → o ay için eşleştir                       │
│ • Yoksa → CreatedDate ile ay eşleştir                                │
└─────────────────────────────────────────────────────────────────────┘
```

---

## 5. Önemli Bileşenler ve Rolleri

| Bileşen | Rol |
|---------|-----|
| **InvoiceManager.GetMonthlyStatsAsync** | Dashboard aylık istatistikleri (ödenen/gecikmiş/bekleyen) |
| **InvoicesController.GetMonthlyHistory** | Fatura detay sayfasında aylık zaman çizelgesi |
| **InvoicesController.CancelPaymentLink** | Ödeme iptali (MasterAdmin direkt, diğerleri onay) |
| **StatsController.OverdueInvoices** | Gecikmiş yinelenen faturalar listesi |
| **FinanceController.ReportData** | Gelir raporu (Invoices + manuel PaymentLinks) |
| **ApprovalController** | ManualPayment, PaymentCancel, InvoiceDelete onayları |

---

## 6. Tutarlı Olması Gereken Kurallar

1. **Ödendi** = sadece `completed` veya `mismatch`, `cancelled` asla sayılmaz.
2. **Ay eşleştirme** = `PaidForMonth/PaidForYear` varsa onlar, yoksa `CreatedDate`.
3. **Tek seferlik revert** = Son ödeme iptal edilirse `Invoice.Status` → `pending`.
4. **Yinelenen** = Her ay ayrı hesaplanır; `Invoice.Status` ay bazlı mantığı etkilemez.

Bu kurallara uyulduğu sürece sistem tahsilat mantığının dışına çıkmaz.
