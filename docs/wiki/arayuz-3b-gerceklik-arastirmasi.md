# Arayüz 3B Sahne — Gerçekçilik Araştırması

Ana sayfadaki kaydırmalı 3B sahne ([[arayuz]]) "yapay ve çocuksu" bulundu. Bu sayfa nedenleri, web araştırmasından çıkan kaynakları ve önerilen yolu kaydeder. Tarih: 2026-10-29. Karar kaydı: [[kararlar]] K-89 (öneri, kullanıcı onayı bekliyor).

## 0. Uygulama durumu (2026-10-30)

Önerilen yol uygulandı. Sahnede artık dört Sketchfab CC BY 4.0 modeli var (`web/public/models/v2/`, toplam ~2,7 MB; eski modeller ~12 MB'tı): Stethoscope (Phillip Stephens), Hospital Room'dan yalnız yatak + serum askısı (Uğur Yakışık), Microscope (Eugen Vahrushin), Doctor (BrushDip, iskeletli, gömülü poz klibi son karede sabitlenir). Bölüm sırası: steteskop, hasta odası, mikroskop, doktor. Atıf: `web/public/models/CREDITS.md` ve ana sayfa altlığı.

Çıkan dersler:
- **"Hospital Room" bir oda değil, yan yana dizilmiş parça setidir** (duvar, yatak, dolap, kanepe… z ekseninde sıralı). Bu yüzden yalnız `Bed`, `IvPole`, `Serum` parçaları bırakılıyor (`keep` seçeneği, `HeroScene.tsx`). Başlıktan ve beğeniden anlaşılmıyor; indirmeden önce görüntüleyicide bakmak gerekir.
- Dönüştürme: `gltf-transform optimize in.gltf out.glb --compress meshopt --texture-compress webp --texture-size 2048 --palette false --flatten false --join false --simplify false` (66 MB → 1,4 MB oda, 17 MB → 0,5 MB mikroskop). `EXT_texture_webp` three'nin `GLTFLoader`'ında yerleşik; KTX2 gerekmedi.
- Steteskop yatay geldiği için `modelRotation` ile kameraya çevrildi.
- **Kullanılmayanlar:** "Stylized Simply Visualization Of DNA" (Orpind) **CC BY-NC-ND 4.0**: ticari kullanım yok ve değiştirilmiş sürüm dağıtılamaz; web için sıkıştırma/dönüştürme "değişiklik" sayılır, ayrıca ham hali 31 MB. Kullanılmadı. "Simple Hospital Room" (Johnny Rumeliotis): indirme kapalı ve 535k yüzey; kullanılamaz. Kalp modeli indirilmedi (bölüm sayısı dörtte tutuldu).
- Ham zip'ler `web/models-src/` altında (git'e girmez, `.gitignore`).

## 1. Sorunun nedeni

- Mevcut altı model Poly Pizza'dan (Poly by Google, jeremy, GRADD vb.); low-poly/oyuncak stili. Doktor modeli 105 KB. Kaynaklar `web/public/models/CREDITS.md` içinde.
- Farklı sanatçıların farklı stilleri ve ölçekleri tek sahnede birleşiyor; bina + ambulans + saat + doktor minyatür diorama etkisi veriyor.
- Kodda gerçekçiliği düşüren üç şey: (a) `tuneMaterial` ile roughness/metalness/renk ezilerek orijinal PBR dokuları düzleşiyor; (b) `RoomEnvironment` ışığı düz; zemin gölgesi yok; (c) neon halka ve parçacıklar sci-fi/oyuncak hissi veriyor. Hepsi `web/src/three/HeroScene.tsx` içinde.

## 2. Önerilen yol (özet)

1. **Sahneyi sadeleştir:** Bölüm başına tek "kahraman" nesne. Kalabalık diorama yerine tek, yüksek kaliteli model.
2. **İnsan karakteri kullanma.** Gerçekçi insan modelleri "uncanny valley" riski taşır ve ağırdır. Anatomi (kalp) + ekipman (mikroskop, steteskop) + oda daha güvenli.
3. **Modelleri Sketchfab'dan seç** (CC0 / CC-BY, indirilebilir), önce Sketchfab görüntüleyicisinde gözle incele.
4. **Işığı düzelt:** Poly Haven (CC0) studio HDRI'ı kendi sunucudan yükle; `ContactShadows` ekle.
5. **Materyal override'larını kaldır** (`tuneMaterial`); modelin kendi PBR dokularını kullan.
6. **Sahnedeki neon halka/parçacıkları** kaldır ya da çok soluklaştır.
7. **Sıkıştır:** `gltf-transform` ile Meshopt + doku sıkıştırma.
8. (İsteğe bağlı) `@react-three/postprocessing` ile hafif SSAO/bloom/DOF.

## 3. Model kaynakları

Sketchfab genel arama API'sinden alındı (indirilebilir modeller, beğeni sırasıyla). Poligon sayısı ve beğeni stil/kaliteyi kanıtlamaz: **her aday önce görüntüleyicide gözle kontrol edilmeli.** İndirmek için Sketchfab hesabı gerekir. Lisans notu: CC-BY → `CREDITS.md`'ye atıf eklenir; CC-BY-NC modellerden kaçın (ticari kullanım riski); CC-BY-SA paylaşım şartı getirir.

### Kalp / anatomi
| Model | Yazar | Lisans | Yüzey | Not |
|---|---|---|---|---|
| Realistic Human Heart | neshallads | CC-BY | 22,562 | 1681 beğeni, en popüler; hafif |
| Human heart, realistic anatomical model | TinkerLane Studio | CC-BY | 20,370 | alternatif |
| Beating-heart | jalmer | CC-BY | 34,401 | atan kalp (animasyonlu olabilir, doğrula) |
| NIH 3D "human heart" (3DPX-022787) | Sourav Pan / Biology Notes Online | CC0 | ? | NIH 3D lisansı CC0; **indirme formatı (GLB var mı) doğrulanamadı**, sayfada kontrol et |

Dikkat: Z-Anatomy ve E-learning UMCG modelleri milyonlarca yüzey içerir ve ShareAlike/NonCommercial lisanslıdır; web için uygun değil.

### Mikroskop
| Model | Yazar | Lisans | Yüzey |
|---|---|---|---|
| Microscope | Eugen Vahrushin | CC-BY | 11,643 |
| PBR Vintage Microscope | Christina Lauridsen | CC-BY | 14,234 |
| 1974.15 Microscope | Cleveland Museum of Art | CC0 | 99,498 (antika; ağır) |
| Microscope SWIFT SW380B | Ricardo Dias Teixeira | CC-BY-NC | 19,259 (**kullanma**) |

### Steteskop ve ekipman
| Model | Yazar | Lisans | Yüzey |
|---|---|---|---|
| Stethoscope | Phillip Stephens | CC-BY | 42,000 |
| Doctor's stethoscope | Vera4Art | CC-BY | 7,736 |
| Doctor's Office - Assets | Evan Petrov | CC-BY | 32,212 (787 beğeni) |
| Medical Supplies Collection | Miguel Adão | CC-BY | 87,456 |

### Oda
| Model | Yazar | Lisans | Yüzey |
|---|---|---|---|
| Hospital Room | UgurYksk | CC-BY | 25,105 (1162 beğeni) |
| Stretcher | Odlab | CC-BY | 12,228 |

### Başka kaynaklar ve notlar
- **Poly Haven:** Tüm varlıklar CC0, atıf gerekmez. Medikal model içeriği az; asıl değeri HDRI.
- **Fab / CGTrader / TurboSquid / RenderHub:** Çoğu ücretli; lisans ilan başına kontrol edilmeli. Aramada bir mikroskop ilanında GLB seçeneği görüldü.
- **Ready Player Me:** Kamu hizmeti 31 Ocak 2026'da kapandı (Netflix devralmasının ardından); **kullanma.** Alternatifler (Avatar SDK MetaPerson, Avaturn, Genies) satıcı bloglarından geldi, bağımsız doğrulanmadı. Mixamo araştırılmadı.

## 4. Işık ve render

- **HDRI:** Poly Haven `studio` kategorisinde `ferndale_studio_*`, `cyclorama_hard_light`, `blocky_photo_studio`, `brown_photostudio_*` gibi seçenekler var. Hangisinin en iyi durduğu kodda denenerek seçilmeli (2–3 tanesini karşılaştır). `.hdr` dosyasını `web/public/` altından `<Environment files="..." />` ile yükle.
- drei dokümanı: `Environment preset="..."` üretim için önerilmiyor (CDN'den yükler); kendi sunucudan yükle. Gainmap formatları en küçük boyutu verir.
- **ContactShadows:** drei dokümanı "pahalı efekt" diyor; statik sahnede `frames={1}` kullan, `resolution` düşük tut. (Sahnede nesneler hareket ediyorsa `frames` sınırlaması gölgeyi dondurur; denemeli.)
- **Postprocessing:** `npm install @react-three/postprocessing`; `EffectComposer` içinde Bloom, DepthOfField vb. Efektler tek pasta birleşir. SSAO/N8AO ayrıntısı README'de yok; `docs.pmnd.rs/react-postprocessing` bakılmalı. Mobilde pahalıdır: sade modda ve dar ekranda kapat.
- Tone mapping zaten `ACESFilmicToneMapping`; HDRI ile birlikte pozlama (`toneMappingExposure`) yeniden ayarlanmalı.

## 5. Dosya boyutu ve sıkıştırma

Mevcut dosyalar toplam ~12 MB (`exam_room.glb` 8 MB tek başına).

- Önce `gltf-transform inspect model.glb` ile sorunu gör.
- `gltf-transform optimize in.glb out.glb --compress ... --texture-compress webp` tek adımlık yoldur. Dokümana göre KTX2, `etc1s`/`uastc` komutlarıyla ayrıca yapılır. Üçüncü taraf bir rehber `--compress meshopt --texture-compress ktx2 --texture-size 1024` kullanıyor; **bu bayrakları `gltf-transform optimize -h` ile doğrula.**
- Meshopt genelde Draco'dan hızlı çözülür; Draco daha küçük dosya verir. Koddaki `GLTFLoader` zaten `MeshoptDecoder` kullanıyor.
- KTX2 dokular için `GLTFLoader`'a `KTX2Loader` eklemek gerekir (şu an yok). Ya da WebP ile başlayıp KTX2'yi sonra ekle.
- Hedef: sayfa başına toplam birkaç MB; modeller kaydırma ilerledikçe tembel yüklenmeli.

## 6. Alternatif: 3B yerine fotoğraf/video

Kurumsal sağlık sitelerinde gerçek fotoğraf veya kısa video hero, düşük riskli ve güvenilir görünür. 3B sahnenin bakım ve performans maliyeti yok. Ödevde 3B zorunlu değilse düşünülmeli ([[odev-gereksinimleri]]). Not: "sade mod" zaten var ([[arayuz]]).

## 7. Uygulama sırası (öneri)

1. Kod tarafı (modelden bağımsız, mevcut modellerle bile fark yaratır): HDRI + ContactShadows + `tuneMaterial` temizliği + neon halka/parçacık kaldırma.
2. Yeni GLB'leri seç/indir (kullanıcı hesabıyla), `public/models/` altına koy, `CREDITS.md`'yi güncelle.
3. Sahneyi bölüm başına tek kahraman nesneye göre yeniden kur (kalp, mikroskop, steteskop, oda).
4. Sıkıştırma + tembel yükleme + isteğe bağlı postprocessing.
5. Eski Poly Pizza modellerini sil, `CREDITS.md`'den çıkar.

## 8. Doğrulanamayanlar

- NIH 3D modellerinde GLB indirme formatı.
- Sketchfab adaylarının gerçek görsel kalitesi (yalnız meta veri görüldü).
- `gltf-transform optimize` ile KTX2 bayrağı.
- Mixamo durumu ve lisansı.

## Kaynaklar

- Sketchfab arama API'si: https://api.sketchfab.com/v3/search
- NIH 3D: https://3d.nih.gov/entries/3DPX-022787
- Poly Haven SSS (CC0): https://polyhaven.com/faq, HDRI API: https://api.polyhaven.com/assets?type=hdris&category=studio
- drei Environment: https://drei.docs.pmnd.rs/staging/environment, ContactShadows: https://drei.docs.pmnd.rs/staging/contact-shadows
- react-postprocessing: https://github.com/pmndrs/react-postprocessing
- gltf-transform CLI: https://gltf-transform.dev/cli
- Ready Player Me kapanışı: https://genies.com/blog/ready-player-me-shutdown (satıcı bloğu; Wikipedia: https://en.wikipedia.org/wiki/Ready_Player_Me)
