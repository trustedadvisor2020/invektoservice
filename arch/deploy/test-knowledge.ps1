# ============================================================
#  Invekto.Knowledge Service - Test Suite v1.0
#  Tum endpoint'leri + gercekci senaryolari test eder
#  JWT token otomatik uretilir (sunucudaki secret'tan)
# ============================================================

param(
    [string]$Base = "http://localhost:7104",
    [int]$TenantId = 1,
    [string]$ConfigPath = "E:\Invekto\Knowledge\current\appsettings.Production.json"
)

$ErrorActionPreference = "Stop"
[Console]::OutputEncoding = [System.Text.Encoding]::UTF8

$pass = 0
$fail = 0
$total = 0
$lastBody = ""
$lastOk = $false
$faqIds = @{}

# ============================================================
#  YARDIMCI FONKSIYONLAR
# ============================================================

function B64Url([byte[]]$bytes) {
    $b = [Convert]::ToBase64String($bytes)
    $b = $b.Replace('+','-').Replace('/','_').TrimEnd('=')
    return $b
}

function Generate-JWT([string]$secret, [int]$tenantId) {
    $header = '{"alg":"HS256","typ":"JWT"}'
    $epoch = New-Object DateTime 1970,1,1,0,0,0,([DateTimeKind]::Utc)
    $exp = [int][Math]::Floor(([DateTime]::UtcNow.AddHours(8) - $epoch).TotalSeconds)
    $payload = "{`"tenant_id`":`"$tenantId`",`"user_id`":`"0`",`"role`":`"admin`",`"exp`":$exp}"

    $hdrB64 = B64Url([System.Text.Encoding]::UTF8.GetBytes($header))
    $payB64 = B64Url([System.Text.Encoding]::UTF8.GetBytes($payload))
    $sigInput = "$hdrB64.$payB64"

    $hmac = New-Object System.Security.Cryptography.HMACSHA256
    $hmac.Key = [System.Text.Encoding]::UTF8.GetBytes($secret)
    $sigBytes = $hmac.ComputeHash([System.Text.Encoding]::UTF8.GetBytes($sigInput))
    $sigB64 = B64Url($sigBytes)

    return "$sigInput.$sigB64"
}

function Test-Endpoint {
    param(
        [string]$Name,
        [string]$Method = "GET",
        [string]$Url,
        [string]$Body = $null,
        [string]$Token = $null,
        [int]$ExpectStatus = 200,
        [switch]$Silent
    )

    $script:total++
    $params = @{
        Uri = $Url
        Method = $Method
        UseBasicParsing = $true
        TimeoutSec = 15
    }

    if ($Token) {
        $params.Headers = @{ Authorization = "Bearer $Token" }
    }

    if ($Body) {
        $params.ContentType = "application/json"
        $params.Body = [System.Text.Encoding]::UTF8.GetBytes($Body)
    }

    try {
        $r = Invoke-WebRequest @params
        $status = [int]$r.StatusCode
        $content = $r.Content
    }
    catch {
        if ($_.Exception.Response) {
            $status = [int]$_.Exception.Response.StatusCode
            try {
                $sr = New-Object System.IO.StreamReader($_.Exception.Response.GetResponseStream())
                $content = $sr.ReadToEnd()
                $sr.Close()
            } catch {
                $content = "ERROR"
            }
        } else {
            $status = 0
            $content = "CONNECTION_FAILED: $($_.Exception.Message)"
        }
    }

    $script:lastBody = $content

    if ($status -eq $ExpectStatus) {
        $script:pass++
        $script:lastOk = $true
        if (-not $Silent) {
            Write-Host "  [PASS] $Name  (HTTP $status)" -ForegroundColor Green
        }
    } else {
        $script:fail++
        $script:lastOk = $false
        Write-Host "  [FAIL] $Name  (Beklenen: $ExpectStatus, Gelen: $status)" -ForegroundColor Red
        $preview = if ($content.Length -gt 200) { $content.Substring(0,200) + "..." } else { $content }
        Write-Host "         Yanit: $preview" -ForegroundColor DarkGray
    }
}

function Get-LastId {
    try {
        $obj = $script:lastBody | ConvertFrom-Json
        return $obj.id
    } catch {
        return $null
    }
}

# ============================================================
#  BANNER
# ============================================================
Write-Host ""
Write-Host "  =====================================================" -ForegroundColor Cyan
Write-Host "   Invekto.Knowledge Service - Test Suite v1.0" -ForegroundColor Cyan
Write-Host "   Port: 7104  |  Target: $Base" -ForegroundColor Cyan
Write-Host "   Tenant: $TenantId  |  Config: $ConfigPath" -ForegroundColor Cyan
Write-Host "  =====================================================" -ForegroundColor Cyan
Write-Host ""

# ============================================================
#  PHASE 0: JWT Token Uret
# ============================================================
Write-Host "[PHASE 0] JWT Token uretiliyor..." -ForegroundColor Yellow

if (-not (Test-Path $ConfigPath)) {
    Write-Host "[HATA] Config dosyasi bulunamadi: $ConfigPath" -ForegroundColor Red
    exit 1
}

$cfg = Get-Content $ConfigPath -Raw | ConvertFrom-Json
$secret = $cfg.Jwt.SecretKey

if (-not $secret -or $secret -like "REPLACE*") {
    Write-Host "[HATA] Jwt:SecretKey ayarlanmamis! $ConfigPath kontrol edin." -ForegroundColor Red
    exit 1
}

$token = Generate-JWT -secret $secret -tenantId $TenantId
Write-Host "[OK] JWT token uretildi (8 saat gecerli)" -ForegroundColor Green
Write-Host ""

# ============================================================
#  PHASE 1: Saglik Kontrolleri
# ============================================================
Write-Host "  === PHASE 1: Saglik Kontrolleri ===" -ForegroundColor Cyan

Test-Endpoint -Name "Health Check" -Url "$Base/health" -ExpectStatus 200
Test-Endpoint -Name "Ready Check (pgvector)" -Url "$Base/ready" -ExpectStatus 200
Test-Endpoint -Name "Endpoint Discovery" -Url "$Base/api/ops/endpoints" -ExpectStatus 200

Write-Host ""

# ============================================================
#  PHASE 2: FAQ CRUD Senaryolari
# ============================================================
Write-Host "  === PHASE 2: FAQ CRUD Senaryolari ===" -ForegroundColor Cyan

# 2.1: Kargo FAQ
$body = @{
    question = "Kargo ne kadar surede gelir?"
    answer = "Siparisleriniz 2-3 is gunu icerisinde kargoya verilir. Istanbul ici teslimat genellikle 1 is gunu icerisinde tamamlanir."
    category = "shipping"
    lang = "tr"
    keywords = @("kargo","teslimat","sure")
} | ConvertTo-Json -Compress
Test-Endpoint -Name "FAQ Olustur: Kargo suresi" -Method POST -Url "$Base/api/v1/knowledge/$TenantId/faqs" -Body $body -Token $token -ExpectStatus 201
if ($lastOk) {
    $faqIds["kargo"] = Get-LastId
    Write-Host "         > ID: $($faqIds['kargo'])" -ForegroundColor DarkYellow
}

# 2.2: Iade FAQ
$body = @{
    question = "Iade kosullari nelerdir?"
    answer = "Urunlerinizi teslim aldiginiz tarihten itibaren 14 gun icerisinde, kullanilmamis ve orijinal ambalajinda olmak kaydiyla iade edebilirsiniz. Iade kargo ucreti tarafimiza aittir."
    category = "returns"
    lang = "tr"
    keywords = @("iade","geri gonderme","kosul")
} | ConvertTo-Json -Compress
Test-Endpoint -Name "FAQ Olustur: Iade politikasi" -Method POST -Url "$Base/api/v1/knowledge/$TenantId/faqs" -Body $body -Token $token -ExpectStatus 201
if ($lastOk) {
    $faqIds["iade"] = Get-LastId
    Write-Host "         > ID: $($faqIds['iade'])" -ForegroundColor DarkYellow
}

# 2.3: Odeme FAQ
$body = @{
    question = "Hangi odeme yontemlerini kabul ediyorsunuz?"
    answer = "Kredi karti, banka karti, havale/EFT ve kapida odeme seceneklerini kabul ediyoruz. Kredi kartina 3-6 taksit imkani mevcuttur."
    category = "payment"
    lang = "tr"
    keywords = @("odeme","kredi karti","taksit","havale")
} | ConvertTo-Json -Compress
Test-Endpoint -Name "FAQ Olustur: Odeme yontemleri" -Method POST -Url "$Base/api/v1/knowledge/$TenantId/faqs" -Body $body -Token $token -ExpectStatus 201
if ($lastOk) {
    $faqIds["odeme"] = Get-LastId
    Write-Host "         > ID: $($faqIds['odeme'])" -ForegroundColor DarkYellow
}

# 2.4: Stok FAQ
$body = @{
    question = "Urun stokta yoksa ne yapmaliyim?"
    answer = "Stokta olmayan urunler icin Gelince Haber Ver butonuna tiklayarak bildirim alabilirsiniz. Urun tekrar stoklara girdiginde size otomatik e-posta gonderilir."
    category = "stock"
    lang = "tr"
    keywords = @("stok","urun yok","bildirim")
} | ConvertTo-Json -Compress
Test-Endpoint -Name "FAQ Olustur: Stok durumu" -Method POST -Url "$Base/api/v1/knowledge/$TenantId/faqs" -Body $body -Token $token -ExpectStatus 201
if ($lastOk) {
    $faqIds["stok"] = Get-LastId
    Write-Host "         > ID: $($faqIds['stok'])" -ForegroundColor DarkYellow
}

# 2.5: Siparis Takip FAQ
$body = @{
    question = "Siparisimi nasil takip edebilirim?"
    answer = "Hesabim sayfasindan veya size gonderilen kargo takip numarasi ile kargo firmasinin web sitesinden takip edebilirsiniz. Ayrica WhatsApp uzerinden siparis numaranizi gonderirseniz anlik bilgi verebiliriz."
    category = "order"
    lang = "tr"
    keywords = @("siparis","takip","kargo takip")
} | ConvertTo-Json -Compress
Test-Endpoint -Name "FAQ Olustur: Siparis takibi" -Method POST -Url "$Base/api/v1/knowledge/$TenantId/faqs" -Body $body -Token $token -ExpectStatus 201
if ($lastOk) {
    $faqIds["siparis"] = Get-LastId
    Write-Host "         > ID: $($faqIds['siparis'])" -ForegroundColor DarkYellow
}

Write-Host ""

# 2.6: Listele
Test-Endpoint -Name "FAQ Listele (tumu)" -Url "$Base/api/v1/knowledge/$TenantId/faqs" -Token $token -ExpectStatus 200

# 2.7: Filtreli listele
Test-Endpoint -Name "FAQ Listele (shipping)" -Url "$Base/api/v1/knowledge/$TenantId/faqs?category=shipping" -Token $token -ExpectStatus 200

# 2.8: Guncelle
if ($faqIds["kargo"]) {
    $fid = $faqIds["kargo"]
    $body = @{
        question = "Kargo ne kadar surede gelir?"
        answer = "Siparisleriniz 1-2 is gunu icerisinde kargoya verilir. Ayni gun kargo secenegi de mevcuttur (ek ucretli). Istanbul ici teslimat genellikle ayni gun tamamlanir."
        category = "shipping"
        lang = "tr"
        keywords = @("kargo","teslimat","sure","ayni gun")
    } | ConvertTo-Json -Compress
    Test-Endpoint -Name "FAQ Guncelle: Kargo suresi" -Method PUT -Url "$Base/api/v1/knowledge/$TenantId/faqs/$fid" -Body $body -Token $token -ExpectStatus 200
}

Write-Host ""

# ============================================================
#  PHASE 3: Arama Senaryolari
# ============================================================
Write-Host "  === PHASE 3: Arama Senaryolari ===" -ForegroundColor Cyan

$searches = @(
    @{ Name = "Arama: kargo ne zaman gelir";         Query = "kargo ne zaman gelir"; TopK = 3 },
    @{ Name = "Arama: urun iade etmek istiyorum";    Query = "urun iade etmek istiyorum"; TopK = 3 },
    @{ Name = "Arama: taksitle odeyebilir miyim";    Query = "taksitle odeyebilir miyim"; TopK = 3 },
    @{ Name = "Arama: bu urun stokta var mi";        Query = "bu urun stokta var mi"; TopK = 3 },
    @{ Name = "Arama: siparisim nerede";             Query = "siparisim nerede kargom gelmedi"; TopK = 5 }
)

foreach ($s in $searches) {
    $body = @{ query = $s.Query; topK = $s.TopK } | ConvertTo-Json -Compress
    Test-Endpoint -Name $s.Name -Method POST -Url "$Base/api/v1/knowledge/$TenantId/search" -Body $body -Token $token -ExpectStatus 200
}

# Filtreli aramalar
$body = @{ query = "ne kadar surede gelir"; topK = 3; category = "shipping" } | ConvertTo-Json -Compress
Test-Endpoint -Name "Arama: filtreli (shipping)" -Method POST -Url "$Base/api/v1/knowledge/$TenantId/search" -Body $body -Token $token -ExpectStatus 200

$body = @{ query = "kargo"; topK = 3; lang = "tr" } | ConvertTo-Json -Compress
Test-Endpoint -Name "Arama: filtreli (lang=tr)" -Method POST -Url "$Base/api/v1/knowledge/$TenantId/search" -Body $body -Token $token -ExpectStatus 200

Write-Host ""

# ============================================================
#  PHASE 4: Embedding Uretimi
# ============================================================
Write-Host "  === PHASE 4: Embedding Uretimi ===" -ForegroundColor Cyan

Test-Endpoint -Name "Embedding Uret" -Method POST -Url "$Base/api/v1/knowledge/$TenantId/generate-embeddings" -Body "{}" -Token $token -ExpectStatus 200

Write-Host ""

# ============================================================
#  PHASE 5: Hata Senaryolari (Negatif Testler)
# ============================================================
Write-Host "  === PHASE 5: Hata Senaryolari (Negatif Testler) ===" -ForegroundColor Cyan

# Auth olmadan
Test-Endpoint -Name "Auth olmadan FAQ listele (401 beklenir)" -Url "$Base/api/v1/knowledge/$TenantId/faqs" -ExpectStatus 401

# Bos soru
$body = @{ question = ""; answer = "Test cevap"; category = "general"; lang = "tr" } | ConvertTo-Json -Compress
Test-Endpoint -Name "Bos soru ile FAQ (400 beklenir)" -Method POST -Url "$Base/api/v1/knowledge/$TenantId/faqs" -Body $body -Token $token -ExpectStatus 400

# Olmayan FAQ
$body = @{ question = "Test?"; answer = "Test"; category = "general"; lang = "tr" } | ConvertTo-Json -Compress
Test-Endpoint -Name "Olmayan FAQ guncelle (404 beklenir)" -Method PUT -Url "$Base/api/v1/knowledge/$TenantId/faqs/999999" -Body $body -Token $token -ExpectStatus 404

# Bos query
$body = @{ query = ""; topK = 5 } | ConvertTo-Json -Compress
Test-Endpoint -Name "Bos query ile arama (400 beklenir)" -Method POST -Url "$Base/api/v1/knowledge/$TenantId/search" -Body $body -Token $token -ExpectStatus 400

# Gecersiz topK
$body = @{ query = "test"; topK = 100 } | ConvertTo-Json -Compress
Test-Endpoint -Name "topK=100 ile arama (400 beklenir)" -Method POST -Url "$Base/api/v1/knowledge/$TenantId/search" -Body $body -Token $token -ExpectStatus 400

Write-Host ""

# ============================================================
#  PHASE 6: Temizlik
# ============================================================
Write-Host "  === PHASE 6: Temizlik - Test FAQ'larini Sil ===" -ForegroundColor Cyan

foreach ($key in @("siparis","stok","odeme","iade","kargo")) {
    if ($faqIds[$key]) {
        $fid = $faqIds[$key]
        Test-Endpoint -Name "FAQ Sil: $key" -Method DELETE -Url "$Base/api/v1/knowledge/$TenantId/faqs/$fid" -Token $token -ExpectStatus 200
    }
}

Write-Host ""

# ============================================================
#  SONUC
# ============================================================
Write-Host "  =====================================================" -ForegroundColor Cyan
Write-Host "   TEST SONUCLARI" -ForegroundColor Cyan
Write-Host "   Toplam : $total" -ForegroundColor White
Write-Host "   Gecen  : $pass" -ForegroundColor Green
Write-Host "   Kalan  : $fail" -ForegroundColor $(if ($fail -gt 0) { "Red" } else { "Green" })
Write-Host "  =====================================================" -ForegroundColor Cyan

if ($fail -gt 0) {
    Write-Host ""
    Write-Host "  [!] $fail test basarisiz. Loglari kontrol edin." -ForegroundColor Red
    exit 1
} else {
    Write-Host ""
    Write-Host "  [OK] Tum testler basarili!" -ForegroundColor Green
    exit 0
}
