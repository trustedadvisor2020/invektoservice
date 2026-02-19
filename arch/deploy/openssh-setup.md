# OpenSSH Server Kurulumu (Production Server)

> MCP Server Ops icin gerekli. Tek seferlik kurulum.
> Tarih: 2026-02-18

---

## Adim 1: OpenSSH Server'i Yukle

Sunucuda **Admin PowerShell** ac:

```powershell
# OpenSSH Server feature'ini yukle (Windows Server 2019+ dahili)
Add-WindowsCapability -Online -Name OpenSSH.Server~~~~0.0.1.0

# Dogrula
Get-WindowsCapability -Online | Where-Object Name -like 'OpenSSH*'
```

Cikti: `OpenSSH.Server~~~~0.0.1.0  Installed` olmali.

---

## Adim 2: Servisi Baslat ve Otomatik Yap

```powershell
# SSHD servisini baslat
Start-Service sshd

# Boot'ta otomatik baslasin
Set-Service -Name sshd -StartupType 'Automatic'

# Durum kontrolu
Get-Service sshd
```

Status: **Running** olmali.

---

## Adim 3: Firewall Kurali

```powershell
# SSH icin port 22 ac (eger yoksa)
$rule = Get-NetFirewallRule -Name "OpenSSH-Server-In-TCP" -ErrorAction SilentlyContinue
if (-not $rule) {
    New-NetFirewallRule -Name 'OpenSSH-Server-In-TCP' -DisplayName 'Invekto SSH (TCP 22)' -Enabled True -Direction Inbound -Protocol TCP -Action Allow -LocalPort 22
    Write-Host "Firewall rule created"
} else {
    Write-Host "Firewall rule already exists"
}
```

---

## Adim 4: Test (Dev PC'den)

Dev PC'de PowerShell ac:

```powershell
# SSH baglanti testi
ssh Administrator@services.invekto.com "hostname"
```

Sunucu adini dondururse basarili.

> **Ilk baglantiyi** PowerShell'den yapmak onemli - host key'i kabul etmen gerekecek (yes yaz).

---

## Adim 5: Claude Code MCP Test

Claude Code'u yeniden baslat (`/quit` + tekrar ac) ve MCP server'in yuklendigini dogrula.

Ornek komutlar:
- `"sunucudaki servislerin durumunu goster"` → server-status tool'u cagirilir
- `"Backend loglarini goster"` → server-logs tool'u cagirilir
- `"sunucuda hostname komutunu calistir"` → server-exec tool'u cagirilir

---

## Port Haritasi (Guncellenmis)

| Port | Servis | Erisim |
|------|--------|--------|
| 22   | SSH (OpenSSH) | External (MCP ops) |
| 21   | FTP Control | External (deploy legacy) |
| 5000 | Backend | External (webhooks) |
| 5432 | PostgreSQL | External (dev MCP) |
| 7101-7112 | Mikroservisler | Localhost |

---

## Sorun Giderme

### SSH baglanti reddediliyor
```powershell
# SSHD calisiyor mu?
Get-Service sshd

# Port 22 dinleniyor mu?
netstat -an | findstr ":22"

# Firewall kurali var mi?
Get-NetFirewallRule -Name "OpenSSH-Server-In-TCP"
```

### Sifre kabul edilmiyor
```powershell
# sshd_config'de password auth acik mi?
Get-Content "C:\ProgramData\ssh\sshd_config" | Select-String "PasswordAuthentication"
```

`PasswordAuthentication yes` olmali. Degilse:
```powershell
# sshd_config duzenle
notepad "C:\ProgramData\ssh\sshd_config"
# PasswordAuthentication yes yap, kaydet
Restart-Service sshd
```

### Admin kullanici SSH'ye giremiyorsa
Windows Server'da Administrator kullanicilari icin ozel authorized_keys dosyasi gerekir:
```powershell
# sshd_config'deki bu satirlari yorum satirina al (basa # koy):
# Match Group administrators
#   AuthorizedKeysFile __PROGRAMDATA__/ssh/administrators_authorized_keys
Restart-Service sshd
```
