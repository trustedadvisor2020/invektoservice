# QNB Finansbank Sanal POS Entegrasyon Referansı

## Genel Bilgi

- **Banka:** QNB Finansbank
- **Yöntem:** 3DPay (3D Secure ödeme)
- **Gateway URL:** `https://vpos.qnbfinansbank.com/Gateway/Default.aspx`
- **Dokümantasyon:** https://vpos.qnbfinansbank.com/Help/home
- **Test Kartları:** https://vpos.qnbfinansbank.com/Help/testCards
- **Test Bilgileri:** https://vpos.qnbfinansbank.com/Help/testInformation

## Mevcut Hesap Bilgileri (Regex Danışmanlık)

> **DİKKAT:** Bu bilgiler `appsettings.Production.json`'da saklanır, koda gömülmez!

| Parametre | Değer | Açıklama |
|-----------|-------|----------|
| MbrId | 5 | Kurum kodu |
| MerchantId | 092000000016949 | Üye işyeri numarası |
| UserCode | regeksapiuser | API kullanıcı adı |
| UserPass | o0cCf | API kullanıcı şifresi |
| MerchantPass | 11257943 | 3D üye işyeri anahtarı |

## Test Kartları

### MasterCard / Visa

| # | MasterCard | Visa | Expiry | CVV |
|---|------------|------|--------|-----|
| 1 | 5209882483498019 | 4155650100416111 | 12/25 | 656 |
| 2 | 5456165456165454 | 4282405990002166 | 12/25 | 656 |

### Troy

| # | Kart Numarası | Expiry | CVV |
|---|---------------|--------|-----|
| 1 | 36577312700094 | 08/20 | 483 |
| 2 | 9792350046201275 | 07/27 | 993 |
| 3 | 6501700194147183 | 03/23 | 136 |
| 4 | 9792023757123604 | 01/26 | 861 |
| 5 | 9792072000017956 | 01/20 | 843 |
| 6 | 6500528865390837 | 01/21 | 686 |

> **Not:** Çoğu Troy kartı expire olmuş. Test için Visa `4155650100416111` (12/25, CVV 656) kullan.

## 3DPay Akışı

```
1. Kullanıcı kart bilgilerini girer
2. Backend, form parametrelerini + SHA1 Hash hesaplar
3. Hidden form ile Gateway'e POST (auto-submit HTML)
4. Banka 3D doğrulama sayfasını gösterir
5. Doğrulama sonrası OkUrl/FailUrl'e POST ile döner
6. Backend, callback'teki parametreleri okur ve sonucu belirler
```

## Hash Hesaplama

```
hashstr = MbrId + OrderId + Amount + OkUrl + FailUrl + TxnType + InstallmentCount + Rnd + MerchantPass
hash = Base64(SHA1(UTF8(hashstr)))
```

## İstek Parametreleri

| Parametre | Açıklama | Örnek |
|-----------|----------|-------|
| MbrId | Kurum kodu | 5 |
| MerchantId | Üye işyeri no | 092000000016949 |
| UserCode | Kullanıcı adı | regeksapiuser |
| UserPass | Kullanıcı şifre | *** |
| SecureType | Güvenlik tipi | 3DPay |
| TxnType | İşlem tipi | Auth |
| InstallmentCount | Taksit sayısı | 0 (peşin) |
| Currency | Para birimi kodu | 949 (TRY) |
| OkUrl | Başarılı dönüş URL | /api/payment/callback |
| FailUrl | Başarısız dönüş URL | /api/payment/callback |
| OrderId | Sipariş numarası | INV-20260302... |
| PurchAmount | Tutar (kuruş formatı) | 100.00 |
| Lang | Dil | TR |
| Rnd | Random değer (timestamp) | 20260302... |
| Hash | SHA1 hash (Base64) | computed |
| Pan | Kart numarası | 4155650100416111 |
| Expiry | Son kullanma (MMYY) | 0125 |
| Cvv2 | CVV kodu | 123 |

## Callback (Dönüş) Parametreleri

| Parametre | Açıklama |
|-----------|----------|
| 3DStatus | 3D doğrulama sonucu. `1` = başarılı |
| ProcReturnCode | İşlem sonucu. `00` = başarılı |
| TxnResult | İşlem sonucu text |
| ErrMsg | Hata mesajı |
| OrderId | Sipariş numarası |
| AuthCode | Onay kodu |
| HostRefNum | Banka referans no |
| TransId | İşlem ID |
| PurchAmount | Tutar |
| TxnType | İşlem tipi |
| TxnStatus | İşlem durumu |

## Başarı Kontrolü

```csharp
// 3D doğrulama başarılı VE banka işlemi onayladı
if (response.ThreeDStatus == "1" && response.ProcReturnCode == "00")
{
    // BAŞARILI
}
```

## InvektoServices'teki Kullanım Planı

Lisans ödeme sistemi için kullanılacak:
- Tenant lisans yenileme/yükseltme ödemeleri
- Backend servisi üzerinden ödeme başlatma
- Callback endpoint ile sonuç alma
- `tenant_payments` tablosu ile ödeme geçmişi

## Dosya Konumları

| Dosya | Açıklama |
|-------|----------|
| `Invekto.Shared/DTOs/Payment/PaymentDtos.cs` | DTO'lar |
| `Invekto.Shared/Services/QnbVPosService.cs` | Servis kodu |
| `appsettings.json` → `QnbVPos` section | Konfigürasyon |
