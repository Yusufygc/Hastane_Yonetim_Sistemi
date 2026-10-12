# Git ve Ekip İş Akışı

Kaynak: `Fazlar.md` (Git ve İş Birliği Kuralları). Ödev şartı: [[odev-gereksinimleri]]. Fazlar: [[faz-plani]].

- **Çalışma modeli:** İki kişi ortak sorumlulukla çalışır; kişiye özel sahiplik yok. İş feature'lara göre sıralanır.
- **Branch:** `main` korunur. Dallar: `feature/<ad>`, `fix/<ad>`; PR ile birleşir, diğer kişi inceler.
- **Commit:** Conventional Commits (`feat(appointments): slot üretimi`). Küçük ve anlamlı; ödev için farklı günlerde en az 10 commit, hedef çok daha fazlası.
- **Çakışmayı azaltma:** handler/endpoint/EF konfigürasyonları taramayla kaydolur ([[mimari]]); her feature kendi DTO'larını taşır; entity konfigürasyonu sınıf başına dosya.
- **Migration çakışması:** İki kişi aynı anda migration üretirse model snapshot çakışır. Üreten kişi PR'ını hızla birleştirir; diğeri `main`'i çekip kendi migration'ını yeniden üretir.
- **Senkronizasyon:** Günde en az bir kez `main`'i çalışma dalına al.
- **CI:** GitHub Actions `dotnet build` + `dotnet test` (Docker gerekli, [[docker-ortam]]).
- **Asistan kuralı:** Kullanıcı istemedikçe commit/push yapılmaz ([[kararlar]]).
