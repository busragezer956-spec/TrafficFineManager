# Teslim ve Live Session hazırlığı

## Repository paylaşımı

Proje anlamlı yerel commit'lerle hazırlanmıştır. Uzak repository oluşturulmamıştır. GitHub hesabınızda boş bir repository oluşturup aşağıdaki komutları proje kökünde çalıştırın. Örnek URL'yi kendi repository adresinizle değiştirin.

```powershell
git status
git log --oneline
git remote add origin https://github.com/KULLANICI/TrafficFineManager.git
git push -u origin main
```

Değerlendiricinin repository'yi açabildiğini doğrulayın. Private repository seçerseniz teknik ekibe erişim sağlamanız gerekir. Teslim etmeden önce README adımlarını temiz bir klasörde deneyin. Şifre veya connection string parolasını Git'e eklemeyin.

## Beş dakikalık demo

1. Operasyon hesabıyla giriş yapın. Genel bakıştaki sayaçları ve araç tiplerini gösterin.
2. Yeni ceza oluşturun. Tutanak, araç, tutar ve tarih doğrulamasını açıklayın.
3. Yöneticiye gönderin; durumun ve geçmişin birlikte değiştiğini gösterin.
4. Yönetici hesabıyla gerekçeli ret verin.
5. Operasyonla düzeltip tekrar gönderin; eski ret gerekçesinin korunduğunu gösterin.
6. Yönetici ve finans onaylarını sırayla verin. Tamamlanan kaydın kilitlendiğini gösterin.
7. Testleri çalıştırın ve iki kullanıcı aynı anda işlem yaparsa version kontrolünü açıklayın.

## Açıklayabilmeniz gereken kararlar

- **Neden MVC?** Case beklentisine uygun; Razor ile ekran ve controller akışını tek .NET uygulamasında tutuyor.
- **Neden servis?** Geçiş, sahiplik ve rol kurallarının her controller'da tekrarını önlüyor.
- **Neden ayrı History tablosu?** Son durum tek başına önceki kararları ve ret gerekçelerini anlatmaz.
- **Neden Guid Version?** Güncellemede EF eski version'ı WHERE koşuluna ekler. Kaydı başka biri değiştirdiyse sıfır satır güncellenir ve concurrency hatası alınır.
- **Geçmiş ayrı kaydedilirse ne olur?** Durum değişip geçmiş yazılamayabilir. Bu nedenle aynı SaveChanges ve transaction kullanılır.
- **Neden reddedilen kayıt yöneticiye döner?** Düzeltilen tutar veya bilgiler önceki onayı geçersiz kılabilir; süreç baştan değerlendirilir.
- **Neden onaydaki kayıt değişmiyor?** Kullanıcıların onayladığı içerik süreç içinde sessizce değişmesin.
- **Neden repository yok?** Bu ölçekte DbContext'in üzerine aynı metotları saran katman ek fayda sağlamıyor.
- **Migration nasıl uygulanıyor?** Development açılışında MigrateAsync başlangıç şemasını oluşturur; üretimde migration script'leri kontrollü uygulanmalıdır. Model snapshot, EF'nin sonraki değişikliği hesaplamasını sağlar.
- **SQLite testleri SQL Server testinin yerini tutar mı?** Hayır. İş kuralları ve ilişkisel concurrency test edilir; SQL Server kurulumu ve sağlayıcı davranışı ayrıca yerel kabul testinden geçer.

Kodu teslim etmeden önce özellikle `FineWorkflowService`, `AppDbContext`, `FinesController` ve testleri okuyup kendi cümlelerinizle anlatın. Live Session'da anlamadığınız bir bölümü biliyormuş gibi sunmayın.
