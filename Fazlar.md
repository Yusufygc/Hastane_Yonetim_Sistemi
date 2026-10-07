İki kişilik bir ekiple, özellikle Clean Architecture ve Modüler Monolitik yapı kullanan bir projede çalışırken en büyük risk **aynı dosyalarda (özellikle DbContext, Route yapıları veya Dependency Injection kayıt dosyalarında) çakışma (merge conflict)** yaşamaktır.

Bunu önlemenin en etkili yolu, projeyi yatay katmanlara (bir kişi sadece API yazsın, diğeri veritabanı yazsın) göre değil; **Dikey Dilimlere (Vertical Slicing) / Modüllere göre** bölmektir. Böylece herkes kendi iş alanında (katmandan bağımsız olarak) uçtan uca geliştirme yapar.

Projeyi birbirinizi engellemeden, 4 temel faza ve iş paketlerine (Developer A ve Developer B) böldüğümüz plan aşağıdadır:

---

### FAZ 1: Altyapı ve Proje İskeleti (1. Hafta)

Bu aşamada aynı projede çalışacağınız için temel kuralları ve altyapıyı ayrı ayrı inşa edersiniz.

* **Developer A (Proje İskeleti & Veritabanı Temeli):**
* Clean Architecture klasör/katman yapısının oluşturulması (Domain, Application, Infrastructure, API).
* Veritabanı bağlantısının kurulması ve `USER_ACCOUNT`, `PATIENT`, `DOCTOR`, `ADMIN` tablolarının Entity'lerinin (Table Per Type) oluşturulması.
* **Repository ve Unit of Work** arayüzlerinin (Interfaces) ve temel sınıflarının yazılması.


* **Developer B (Uygulama Temelleri & Güvenlik):**
* CQRS (MediatR) kütüphanelerinin projeye entegre edilmesi.
* Global Exception Handling (Hata yakalama) mekanizmasının kurulması.
* JWT (JSON Web Token) tabanlı Login/Register altyapısının `USER_ACCOUNT` için hazırlanması.



### FAZ 2: Temel Modüllerin Paralel Geliştirilmesi (Tamamen Bağımsız)

Altyapı hazırlandıktan sonra birbirinizin koduna hiç dokunmadan çalışabileceğiniz aşamadır.

* **Developer A (Kurum ve Tesis Modülü):**
* `HOSPITAL`, `DEPARTMENT`, `SERVICE` entity'lerinin ve veritabanı konfigürasyonlarının yapılması.
* Bu varlıklar için Repository pattern kullanılarak standart CRUD (Oluşturma, Güncelleme, Listeleme, Silme) API uç noktalarının yazılması.
* Admin panelinden hastane ve hizmet ekleme süreçleri.


* **Developer B (Kullanıcı ve Profil Modülü):**
* `PATIENT` (Hasta) detay kayıt ve güncelleme işlemleri.
* `DOCTOR` (Doktor) profil ve uzmanlık alanı yönetimi.
* Role dayalı yetkilendirme (Authorization) kontrollerinin API seviyesinde (örn: `[Authorize(Role="Admin")]`) eklenmesi.



### FAZ 3: Kalp Noktası - Randevu Modülü (CQRS Odaklı Görev Ayrımı)

Randevu modülü (Appointment) projenin en karmaşık yeridir. Aynı tablo üzerinde çalışacağınız için CQRS kalıbı imdadınıza yetişir.

* **Developer A (Write/Command Tarafı ve State Pattern):**
* Randevu durumları için **State Pattern** (Bekliyor, Onaylandı, İptal vb.) sınıflarının yazılması.
* Randevu oluşturma (`CreateAppointmentCommand`) ve İptal etme komutlarının yazılması.
* **Çakışma Kontrol Algoritması:** Yeni randevu kaydedilmeden önce doktorun o saatteki uygunluğunu doğrulayan algoritmanın kodlanması.


* **Developer B (Read/Query Tarafı ve Takvim):**
* Doktor çalışma saatleri ve izin günleri altyapısının (`DOCTOR_AVAILABILITY`) kodlanması.
* Doktorların uygun randevu slotlarının listelenmesi (`GetAvailableSlotsQuery`).
* Hasta ve doktorların kendi randevu geçmişlerini görebileceği listeleme API'lerinin yazılması.



### FAZ 4: Yan Etkiler ve Arayüz (Observer Pattern & UI)

* **Developer A (UI - Yönetici ve Doktor Tarafı):**
* Doktorun randevularını görebileceği takvim arayüzü ve onay/iptal butonlarının API'ye bağlanması.
* Admin dashboard (Kurum ve çalışan listesi).


* **Developer B (Observer Pattern & Hasta UI):**
* Randevu statüsü değiştiğinde (örn: Onaylandığında) sistemin tetiklemesi için **Observer Pattern (Domain Events)** kullanılarak E-Posta / SMS bildirim altyapısının yazılması.
* Hastaların klinik seçip, doktor saatlerini listelediği ve randevu aldığı ekranların bağlanması.



---

## 🚀 Merge Conflict (Kod Çakışması) Önleme Reçetesi

Ekip çalışmasında baş ağrısı çekmemek için uymanız gereken katı Git ve mimari kuralları:

1. **Git Feature Branch Workflow Kullanın:**
* Asla doğrudan `main` veya `develop` branch'ine kod yazmayın.
* Örnek: Developer A kurum ekleme işini yaparken `feature/add-hospital-crud` branch'i açar. İş bitince `develop` branch'ine Pull Request (PR) açar. Developer B kodu inceler (Code Review) ve onaylar.


2. **Bağımlılık Enjeksiyonu (DI) Dosyası Merkezi Olmasın:**
* C# kullanıyorsanız tek bir `Program.cs` veya `Startup.cs` içine bütün servislerin (Services.AddScoped...) kayıtlarını yığmayın.
* Herkes kendi çalıştığı modül için bir "Extension Method" yazsın (Örn: `services.AddFacilityModule()`, `services.AddAppointmentModule()`). Ana dosyada sadece bu modüller çağrılsın. Böylece aynı dosyayı değiştirmezsiniz.


3. **Kendi DTO ve Request Sınıflarınızı Kullanın:**
* Modüller arası ortak sınıf kullanmaktan kaçının. Örneğin `Service` bilgisini listelerken Developer A kendi `ServiceDto` sınıfını yaratsın. Developer B kendi ekranında hastane bilgisini gösterecekse Developer A'nın DTO'sunu kullanmak yerine kendi `AppointmentFacilityDto` sınıfını yazsın. Bu, birbirinizin işini bozmanızı engeller.


4. **Sık Sık Pull Yapın:**
* Günde en az 2 kez `develop` branch'indeki son güncel hali kendi çalıştığınız branch'e çekin (`git pull origin develop`). Farkları kodlar küçükken çözmek, günlerce bekleyip tek seferde çözmekten bin kat daha kolaydır.