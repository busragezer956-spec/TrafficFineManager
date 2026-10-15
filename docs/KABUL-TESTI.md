# Yerel kabul testi

Önce `dotnet build` ve `dotnet test` komutlarını çalıştırın. Ardından SQL Server ile uygulamayı açın. Her satırı gerçekten deneyip sonucu kaydedin.

| Adım | İşlem | Beklenen sonuç |
| --- | --- | --- |
| 1 | Giriş yapmadan `/Fines` aç | Login ekranına yönlendirme |
| 2 | Operasyonla giriş yap | Genel bakış ve örnek veriler |
| 3 | Araç ekle: `34 XY 987`, Binek, Ford, Focus | Araç listede `34XY987` görünür |
| 4 | Aynı plakayı `34-XY-987` ile yeniden ekle | Tekrarlı plaka hatası |
| 5 | Yeni araç için benzersiz numara ve `1500,25` tutarla ceza oluştur | Yeni durum; geçmişte oluşturma işlemi |
| 6 | Tarihi yarın, tutarı negatif yapmayı dene | Kayıt engellenir; form girdileri korunur |
| 7 | Onaya gönder | Yönetici Onayı; ikinci geçmiş satırı |
| 8 | Operasyonla doğrudan `/Fines/Edit/{id}` aç | 403; düzenleme engellenir |
| 9 | Finansla bu kaydı aç | Yönetici onayı veremez |
| 10 | Yöneticiyle boş ret nedeni gönder | Ret engellenir |
| 11 | Yöneticiyle gerekçeli ret | Reddedildi; gerekçe geçmişte |
| 12 | Operasyonla kaydı düzelt ve tekrar gönder | Yeniden Yönetici Onayı; eski ret silinmez |
| 13 | Yöneticiyle onayla | Finans Onayı |
| 14 | Finansla onayla | Tamamlandı; kayıt artık düzenlenemez |
| 15 | İki sekmede aynı onayı aç; ilkinde onay, ikincisinde ret dene | İkinci işlem stale version uyarısı; fazladan geçmiş yok |
| 16 | Ceza listesinde plaka, tutanak, durum filtrelerini dene | İlgili sonuçlar; temizle tüm kayıtları gösterir |
| 17 | Beş hatalı şifre dene | Hesap beş dakika kilitlenir |
| 18 | Uygulamayı kapatıp tekrar aç | Veriler kalıcı; örnekler tekrar eklenmez |

Geliştirici araçları veya doğrudan HTTP isteğiyle buton saklamanın yetki sağlamadığı da kontrol edilebilir. POST isteğinde CSRF token kaldırıldığında 400 beklenir. Test amacıyla kullanılan veritabanını gerçek şirket verilerinden ayrı tutun.
