# Hızlı Kripto — Benzersiz Cüzdan Çözümü Analizi

## Mevcut Sorun (Çözüldü — Kısa Vadeli Düzeltme)

1. **Ortak cüzdan:** ExtraConfig'deki cüzdanlar rastgele atanıyor → aynı cüzdan birden fazla faturaya verilebiliyor
2. **Gevşek tutar eşleşmesi:** `value >= expected - 0.5%` → 100 USDT transferi 50 USDT bekleyen faturaya da eşleşiyordu
3. **TxHash tekrar kullanımı:** Aynı blockchain transferi iki farklı PaymentLink'e atanabiliyordu

## Uygulanan Kısa Vadeli Düzeltme

- **Tutar aralığı:** `expected - 0.5% <= value <= expected + 15%` (yanlış fatura eşleşmesi engellendi)
- **Kullanılmış TxHash hariç:** Aynı cüzdana atanmış tamamlanmış ödemelerin TxHash'leri eşleşmeden çıkarılıyor
- **Aynı batch çakışması:** Worker içinde tamamlanan linklerin TxHash'i exclude listesine ekleniyor

---

## Benzersiz Cüzdan Çözümü (Önerilen Uzun Vadeli)

### Avantajlar

| Avantaj | Açıklama |
|---------|----------|
| **%100 ayırma** | Her fatura kendi adresine ödeme yapar, karışma imkansız |
| **TxHash kontrolü gereksiz** | Tek transfer = tek fatura |
| **Tutarlı deneyim** | Plisio vb. sistemlerle aynı mantık |
| **Ölçeklenebilirlik** | Cüzdan havuzu limiti yok |

### Gereksinimler

1. **Adres üretimi**
   - TRON adresleri oluşturmak için:
     - **TronWeb** (Node.js): `TronWeb.createAccount()` veya HD wallet
     - **TronNet** (.NET): `TronKey.Generate()` benzeri
     - **Mnemonic + BIP44:** TRON path `m/44'/195'/0'/0/0` vb.

2. **Private key saklama**
   - Her adres için private key güvenli tutulmalı (şifreli DB veya HSM)
   - Ödeme alındıktan sonra fonlar ana cüzdana toplanabilir (opsiyonel)

3. **Altyapı seçenekleri**

   | Seçenek | Zorluk | Maliyet | Not |
   |---------|--------|---------|------|
   | **TronGrid + HD Wallet** | Orta | Ücretsiz | Mnemonic ile adres türetme |
   | **TronLink / TronWeb** | Orta | Ücretsiz | Node.js script ile adres üretimi |
   | **Üçüncü parti API** | Düşük | Ücretli | NOWPayments, Guardarian vb. |
   | **Kendi full node** | Yüksek | Sunucu maliyeti | Tam kontrol |

### Örnek Uygulama Akışı

```
1. PaymentLink oluşturulur (fast_crypto)
2. Yeni TRON adresi üretilir (HD wallet'ten child key)
3. CryptoWalletAddress = yeni adres
4. Müşteri sadece bu adrese ödeme yapar
5. Worker sadece bu adresi tarar → tek transfer = tek fatura
```

### .NET ile Tron Adres Üretimi

- **TronNet** paketi: `TronNet` veya `Tron.Net` (NuGet)
- Örnek:
  ```csharp
  // TronNet veya benzeri ile
  var key = TronKey.Generate();
  string address = key.GetPublicAddress();
  string privateKey = key.PrivateKey; // Güvenli saklanmalı
  ```

### Önerilen Adımlar

1. `TronNet` veya benzeri bir paketi projeye eklemek
2. `IWalletGenerator` servisi: `GenerateAddressAsync() -> (Address, PrivateKeyEncrypted)`
3. PaymentLink oluşturulurken yeni adres üretmek
4. Private key’i şifreli saklamak (gelecekte fon toplama için)
5. CryptoPaymentWorker’da sadece `CryptoWalletAddress` ile tek adres taranacak (mevcut mantık yeterli)

---

## Sonuç

- **Kısa vadede:** Tutar aralığı + TxHash hariç tutma ile karışma riski büyük ölçüde azaltıldı.
- **Uzun vadede:** Benzersiz cüzdan yaklaşımı daha sağlam ve ölçeklenebilir; TronNet entegrasyonu ile uygulanabilir.
