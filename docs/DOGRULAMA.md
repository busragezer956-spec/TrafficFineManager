# Doğrulama kaydı

Tarih: 15.10.2026. Ortam: Ubuntu 24.04, .NET SDK 9.0.315, .NET runtime 9.0.17.

| Kontrol | Sonuç | Kapsam |
| --- | --- | --- |
| Release derlemesi | Başarılı; 0 hata, 0 uyarı | Web + test projeleri, Razor derlemesi |
| İş kuralı testleri | 23/23 başarılı | SQLite ilişkisel veritabanı; rol, geçiş, ret, yeniden gönderim, concurrency, doğrulama |
| HTTP testleri | 9/9 başarılı | EF InMemory; gerçek MVC pipeline, Identity login cookie, CSRF, erişim kontrolü, ekranlar ve Türkçe tutar girişi |
| EF başlangıç migration'ı | Üretildi | SQL Server migration ve model snapshot |
| Windows / SQL Server LocalDB ilk çalıştırma | Kullanıcının ekranlarıyla doğrulandı | Migration/seed, Identity giriş, genel bakış, yeni ceza ve onaya gönderim çalıştı. Tam SQL Server otomatik test koşusu henüz doğrulanmadı. |
| Tarayıcı görsel kontrolü | İlk sürüm kullanıcı ekranlarında incelendi | Genel bakış, ceza detayı ve geçmiş; sonraki süreç göstergesi güncellemesi için yerel görsel kontrol gerekiyor |
| GitHub uzak repository | Oluşturulmadı | Yerel Git geçmişi hazır; kullanıcı hesabına push gerekiyor |

Komutlar:

```powershell
dotnet build TrafficFineManager.sln -c Release
dotnet test TrafficFineManager.sln -c Release
```

SQL Server HTTP testlerini açmak için README'deki `TFM_TEST_SQLSERVER` ayarını kullanın. 32 testin geçmesi SQL Server üzerinde test yapıldığı anlamına gelmez. Son teslimden önce Windows/SQL Server'da `KABUL-TESTI.md` adımlarını tamamlayın.

15.10.2026 yerel kontrol notu: Kullanıcı, boş ret gerekçesinin engellendiğini doğruladı. TEST-2026-001 kaydının 1.500,25 TL tutarla oluşturulup yönetici onayına gönderildiği ekranda görüldü. Tamamlandı durumu ve ret-düzelt-yeniden gönder senaryosunun tamamı son ekranla henüz doğrulanmadı.
