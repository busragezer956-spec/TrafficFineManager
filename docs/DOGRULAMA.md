# Doğrulama kaydı

Tarih: 15.10.2026. Ortam: Ubuntu 24.04, .NET SDK 9.0.315, .NET runtime 9.0.17.

| Kontrol | Sonuç | Kapsam |
| --- | --- | --- |
| Release derlemesi | Başarılı; 0 hata, 0 uyarı | Web + test projeleri, Razor derlemesi |
| İş kuralı testleri | 21/21 başarılı | SQLite ilişkisel veritabanı; rol, geçiş, ret, yeniden gönderim, concurrency, doğrulama |
| HTTP testleri | 6/6 başarılı | EF InMemory; gerçek MVC pipeline, Identity login cookie, CSRF, erişim kontrolü, ekranlar ve Türkçe tutar girişi |
| EF başlangıç migration'ı | Üretildi | SQL Server migration ve model snapshot |
| Gerçek SQL Server çalıştırma | Bu ortamda doğrulanamadı | SQL Server işlemi ortamda başlatılamadı; yerelde kabul testi gerekiyor |
| Tarayıcı görsel kontrolü | Bu ortamda doğrulanamadı | Tarayıcı işlemi ortamda başlatılamadı; ekranlar HTTP üzerinden render edildi |
| GitHub uzak repository | Oluşturulmadı | Yerel Git geçmişi hazır; kullanıcı hesabına push gerekiyor |

Komutlar:

```powershell
dotnet build TrafficFineManager.sln -c Release
dotnet test TrafficFineManager.sln -c Release
```

SQL Server HTTP testlerini açmak için README'deki `TFM_TEST_SQLSERVER` ayarını kullanın. 27 testin geçmesi SQL Server üzerinde test yapıldığı anlamına gelmez. Son teslimden önce Windows/SQL Server'da `KABUL-TESTI.md` adımlarını tamamlayın.
