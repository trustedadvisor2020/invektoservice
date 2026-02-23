---
name: chat-export
description: Export customer chat data from MSSQL to CSV files for intent/scenario training. Connects to customer SQL Server, extracts conversations, writes structured CSV output.
---

# /chat-export [database] [instanceId]

> Musteri DB'sinden sohbet verilerini cekip CSV dosyasina yazar.
> Cikan CSV'ler intent ve senaryo egitim verisi olarak kullanilir.

## Usage

```
/chat-export                     # DB listesini goster, secim iste
/chat-export MyCustomerDB 5553   # Direkt belirtilen DB ve Instance'dan cek
```

## SECURITY

- **customer-mssql MCP serveri otomatik onaylanmaz** — her tool cagrisi Q onayina tabidir
- Sadece SELECT sorgulari calisir, write BLOCKED
- Q acikca istemediginde bu baglanti KURULMAZ

## Workflow

### Step 1: Database Discovery

customer-mssql MCP `list-databases` ile mevcut DB'leri listele.
Q'ya hangi DB'yi kullanacagini sor (eger argumanda verilmediyse).

### Step 2: Schema Kesfet

`list-tables` ile secilen DB'deki tablolari incele.
Ozellikle su tablolari ara:
- `Chats` — sohbet basliklari
- `ChatMessages` — mesaj icerikler
- `Users` — kullanici bilgileri
- `Instances` — WhatsApp/kanal bilgileri

`table-schema` ile kolon yapilarini dogrula.

### Step 3: Instance Secimi

Eger instanceId verilmediyse, mevcut instance'lari sorgula:

```sql
SELECT DISTINCT c.InstanceID, c.InstancePhoneNumber, c.InstanceAccountID, c.InstanceType
FROM Chats c
GROUP BY c.InstanceID, c.InstancePhoneNumber, c.InstanceAccountID, c.InstanceType
ORDER BY c.InstanceID
```

Q'ya hangi instance(lar)i cekecegini sor.

### Step 4: Chat Extraction Query

Asagidaki sorguyu calistir (InstanceID parametrik):

```sql
SELECT
  CASE WHEN C.InstanceType=1 THEN C.InstancePhoneNumber ELSE C.InstanceAccountID END AS [WHATSAPP_NUMBER],
  CONVERT(date, ISNULL(CM.SentTime, CM.CreateDate)) AS [SENT_DATE],
  CONVERT(time(0), ISNULL(CM.SentTime, CM.CreateDate)) AS [SENT_TIME],
  CASE WHEN C.InstanceType=1 THEN C.CustomerPhoneNumber ELSE C.CustomerAccountID END AS [CUSTOMER_ID],
  REPLACE(REPLACE(REPLACE(REPLACE(CM.Body, CHAR(13), ''), CHAR(10), ''), CHAR(9), ''), CHAR(34), '') AS [MESSAGE],
  CASE WHEN CM.FromMe=0 THEN 'CUSTOMER' ELSE 'ME' END AS [MESSAGE_SOURCE],
  (U.Name + ' ' + U.Surname) AS [USER_NAME]
FROM ChatMessages CM WITH (NOLOCK)
INNER JOIN Chats C ON CM.ChatID = C.ID
LEFT JOIN Users U ON CM.UserID = U.ID
WHERE CM.MessageType = 1
  AND CM.SystemMessageType IS NULL
  AND C.CustomerPhoneNumber IS NOT NULL
  AND C.IsGroup = 0
  AND CM.Body NOT IN ('Dosya İndirilememiştir', 'Media could not be downloaded')
  AND LEN(CM.Body) > 0
  AND C.InstanceID = {instanceId}
ORDER BY C.InstancePhoneNumber, C.CustomerPhoneNumber, ISNULL(CM.SentTime, CM.CreateDate)
```

### Step 5: CSV Export (Streaming)

**KRITIK:** Milyonlarca satir olabilir. `export-csv` tool'unu kullan — bu tool streaming ile dogrudan diske yazar, memory'de tutmaz.

```
customer-mssql MCP > export-csv tool:
  database: {secilen DB}
  sql: {yukaridaki sorgu}
  outputPath: C:/CRMs/InvektoServices/temp/chat-export/{database}_{instanceId}_{tarih}.csv
```

- 10 dakika timeout, backpressure yonetimi var
- Her 100k satirda progress loglar
- UTF-8 BOM + proper CSV escaping otomatik

**Kucuk veri icin (< 1000 satir):** `query` tool'unu kullanabilirsin, ama buyuk veri icin HER ZAMAN `export-csv`.

### Step 6: Ozet Rapor

`export-csv` tamamlaninca Q'ya sun:
- Toplam mesaj sayisi (export-csv ciktisinda var)
- Dosya boyutu (MB)
- CSV dosya yolu
- Gecen sure

Ek bilgi icin kucuk bir `query` calistir:
```sql
SELECT
  COUNT(*) AS total_messages,
  COUNT(DISTINCT C.CustomerPhoneNumber) AS unique_customers,
  MIN(ISNULL(CM.SentTime, CM.CreateDate)) AS first_date,
  MAX(ISNULL(CM.SentTime, CM.CreateDate)) AS last_date
FROM ChatMessages CM WITH (NOLOCK)
INNER JOIN Chats C ON CM.ChatID = C.ID
WHERE CM.MessageType = 1 AND CM.SystemMessageType IS NULL
  AND C.CustomerPhoneNumber IS NOT NULL AND C.IsGroup = 0
  AND CM.Body NOT IN ('Dosya İndirilememiştir', 'Media could not be downloaded')
  AND LEN(CM.Body) > 0
  AND C.InstanceID = {instanceId}
```

## Notes

- `temp/` klasoru gitignore'da, CSV'ler repo'ya eklenmez
- `export-csv` streaming modda calisir: milyonlarca satir icin memory-safe
- WITH (NOLOCK) hint'i production DB'de lock etmemek icin kritik
- `query` tool max 1000 satir — kesfif icin. Bulk export icin `export-csv` kullan.
