# GAFFER
### Game Design Document
**Roguelike Futbol Menajerliği · Mobil (iOS / Android) · Unity**

> *Gaffer* — İngiliz futbol kültüründe oyuncuların menajere verdiği isim. Oyunun fantezisini tek kelimede taşıyor: sen patronsun, saha kenarındaki adam.

---

## 1. Özet

**High-concept:** Football Manager'ın derinliği değil, *fantezisi*. Bir alt lig kulübünde işe başlarsın, bir sezonun vardır — ya yönetimin koyduğu hedefi tutturur yükselirsin, ya da kovulursun. Her sezon bir "run"; başarısız olsan bile menajer kariyerin (itibar, unlock'lar, scout ağı) kalıcı olarak büyür. Görsel 3D maç motoru yok; oyunun kalbi istatistiksel olarak *inandırıcı* bir maç simülasyonu.

**Tek cümle:** "Bir hiçten efsane menajer ol — her sezon bir kumar, her kovulma bir sonraki denemenin yakıtı."

**Tasarım sütunları:**
1. **Hikaye, tablo değil.** İnsanlar sistemi değil hikayeyi hatırlar: bulup büyütüp sattıkları oyuncuyu, kaybettikleri derbiyi, verdikleri kararı. Oyunun ürettiği asıl şey duygu, sayı değil. → *Formül: Hikaye = Simülasyon + Karakter + Hafıza.*
2. **Oyuncular karakterdir.** Kadrodaki her isim bir attribute yığını değil, bir kişilik: derbi canavarı, soyunma odası lideri, antrenman kaçkını, basın adamı. Trait'ler mekanik olarak gerçektir — maçı ve olayları *gerçekten* değiştirir.
3. **Dram sonuçludur ve bir karardır.** Oyuncu kaçar, başkan ölür, taraftar ayaklanır — ama her olay state'i değiştirir ve bir seçim dayatır. Oyuncu seçince sahiplenir; sahiplenince hatırlar.
4. **Her karar tartılır.** Dar kadro, kısıtlı bütçe; her transfer ve her taktik bir bahis.
5. **İnandırıcı, gösterişli değil.** Maç bir animasyon değil, bir anlatı. Favoriler genelde kazanır ama sürprizler gerçek hisseder.
6. **Kaybetmek ilerlemektir.** Kovulmak run'ı bitirir, kariyeri değil.
7. **Bir oturumda tatmin.** 3–6 dakikada anlamlı bir ilerleme; bırakınca içi rahat, geri dönünce bir kanca.

> **Tasarım aksiyomu:** Simülasyon dramın rakibi değil, *toprağı*. Ali Yılmaz hikayesi çünkü sim onu inandırıcı biçimde büyüttüğü için işler. Sim olmadan dram scripted bir cutscene'dir. O yüzden bu doküman sim'i zayıflatmaz — üstüne **karakter** ve **hafıza** katmanlarını ekler.

---

## 2. Çekirdek Fantezi ve Yapı

**Fantezi:** Underdog menajerliği — dipten zirveye, hiç kimseyken efsane olmak.

**Yapı:** Roguelike run. Oyunu FM'den ayıran ve mobile oturtan şey bu.

- **Bir sezon = bir run.** Bir kulüpte işe başlarsın, yönetim bir hedef koyar (ör. "ilk 10", "play-off", "küme düşmeme"). Sezon sonunda hedefi tutturursan itibar kazanır, unlock açar, daha büyük kulüplerden teklif alırsın. Tutturamazsan kovulursun; run biter.
- **Meta-progression kalıcı.** Menajer itibarı, kalıcı perk'ler (ör. gençlik gözlemciliği +1, transfer pazarlığı indirimi), keşfedilmiş scout ağı ve oyun-tarzı arketipleri run'lar arası taşınır. Kovulmak sıfırlama değil, bir sonraki run'ın yakıtıdır.
- **Run içi rastgelelik.** Her run'da mevcut serbest oyuncular, kulüp mali durumu, olay kartları ve rakip güç dağılımı tohum (seed) bazlı üretilir. Aynı strateji her run'da işlemez — adaptasyon gerekir.

Bu yapı üç şeyi aynı anda çözer: mobil oturum uzunluğu (run kısa), retention (meta-progression) ve farklılaşma (hiçbir FM klonuna benzemez).

---

## 3. Core Loop

**Oturum içi (~3–6 dk):**

```
Kadro & taktik seç  →  Maçı simüle et (dakika dakika anlatı)
        ↑                          ↓
Antrenman / moral   ←   Sonuç + kilit anlar + itibar/puan
        ↑                          ↓
   Transfer / scout  ←————  Hafta ilerle
```

**Run içi (bir sezon):** ~30–40 maçlık lig + basit kupa; yönetim hedefi; sezon sonu değerlendirmesi (yüksel / kal / kovul).

**Meta (run'lar arası):** İtibar artışı → yeni kulüpler, perk'ler, arketipler unlock → yeni run daha yüksek tavanla başlar.

Her üç döngünün de kendi "bir sonraki adım" kancası olmalı: maçtan sonra transfer merakı, sezon sonunda "bu sefer daha ileri gidebilirim", run bitince "şu perk'i açsam neler olurdu".

---

## 4. Oyun Sistemleri

### 4.1 Maç Simülasyon Modeli — *oyunun kalbi*

Görsel motor yok. Maç, attribute'lardan olasılık üreten, dakika dakika çözülen istatistiksel bir simülasyon. Hedef **inandırıcılık**: favoriler genelde kazanmalı ama sürprizler gerçek hissettirmeli.

**Katmanlı model:**

1. **Takım gücü türetme.** Pozisyonel attribute'lardan taktik uyumu, form ve moralle modüle edilmiş bir *etkin güç* profili çıkar (hücum, orta saha kontrolü, savunma, kaleci). Tek skaler değil — faz bazlı.
2. **Şans üretimi.** Maç dakika dakika (veya olay bazlı) ilerler. Top hakimiyeti/tempo, iki takımın orta saha gücü oranından türetilir; hücum fazları Poisson benzeri bir süreçle "net şans" üretir. Güçlü takım daha çok ve daha kaliteli şans üretir — ama garanti değil.
3. **Şans çözümü.** Her şansın bir *kalite* değeri var (xG benzeri). Gol olasılığı = şans kalitesi × bitiricilik/kaleci etkileşimi. Böylece "az şansla kazanan takım" veya "çok şans harcayan takım" gibi inandırıcı hikayeler doğal olarak çıkar.
4. **Bağlam duyarlılığı.** Sim maçın *önemini* bilir: derbi mi, final mi, küme düşme maçı mı, seyirci sayısı, sezonun kaderini belirleyen maç mı. Bu bağlam, oyuncu trait'lerini tetikler (bkz. 4.7) — "derbi canavarı" etkin attribute'larını yükseltir, "büyük maçta kaybolan" düşürür. Sim'e bir `MatchContext` girdisi eklenir; bu, karakteri maça bağlayan köprüdür.
5. **Olay katmanı.** Sakatlık, kart, yorgunluk, maç içi taktik değişikliği modifier olarak işler ve kilit anları besler.
6. **Anlatı çıktısı = karakter anı, skor değil.** Dakika dakika olay akışı isimlerle ve bağlamla anlatılır: "68' — 19 yaşındaki Ali Yılmaz, kariyerinin ilk derbisinde soğukkanlılıkla ağları buldu." Çıktı sadece gol değil, *kimin* attığı ve *ne anlama geldiği*. Bu beat'ler oyuncunun yolculuk günlüğüne (bkz. 4.8) yazılır. Anlatı, simülasyonun ham maddesini hikayeye çeviren ilk yerdir.

**Determinizm:** Simülasyon seed'li RNG ile deterministik. Aynı seed → aynı sonuç. Bu hem hata ayıklamayı, hem testi, hem "aynı maçı tekrar izleme" özelliğini mümkün kılar.

**Blackbox quirk'leri — sürpriz bir özelliktir, bug değil (CM 01/02 dersi).** Simülasyon *tam öngörülebilir olmamalı*: ara sıra beklenmedik bir yükseliş (alt-lig bir gencin gerçek yıldızları geçmesi) ya da bir favorinin çöküşü sistemik olarak mümkün olmalı. CM 01/02'nin efsane wonderkid'i Tsigalko tam da bu quirk'ten doğdu; keşif fantezisinin yarısı bu öngörülemezliktir. Yukarıdaki "inandırıcı ama sürpriz gerçek hisseder" hedefi bunu kapsar — nadir overperformer'ları törpüleme.

Oyunun dengesi burada yaşar; en çok emek isteyen yer burasıdır.

### 4.2 Oyuncu Modeli — attribute değil, karakter

Bir oyuncu dört katmandan oluşur: **rakamlar + kişilik + ilişkiler + seninle geçmiş.** Sadece ilki bir istatistik; dördü birlikte bir karakter.

**Katman 1 — Attribute (rakamlar):**
- Kurgusal isimler ve kulüpler (lisans gerektirmez).
- **Attribute seti (0–100, FM-benzeri, gruplu ~18–20).** Gruplar: **Teknik** (finishing, technique, first touch, dribbling, passing, crossing, heading, long shots, marking, tackling), **Set-piece** (penalties, free kicks, corners, long throws), **Fiziksel & Hareket** (pace, acceleration, stamina, strength, agility, jumping, balance, positioning) ve yalnız kaleciler için **Kalecilik** (reflexes, handling, aerial reach, command of area, one-on-ones, kicking + GK positioning). Sayılar **tabular**, değere göre vurgulanır (bkz. ART_STYLE §4.1: 85+ accent'te yanar).
- **Derinlik attribute *sayısında* değil, katmanlarda.** FM'in "mental" ekseni (composure, vision, flair, work rate, off-the-ball, anticipation, decisions, leadership, aggression, bravery) GAFFER'da **attribute değil** — trait + kişilik (Katman 2) *soğurur*. Yani teknik/fiziksel granülerlik zengin, ama "sezgi/karakter" çift-sayılmaz; bu hem GAFFER kimliği hem solo dengeleme için sağlıklı.
- **Pozisyona göre önemli attribute'lar vurgulanır** (rol-anahtarı): bir striker'da finishing/pace/positioning, bir stoperde tackling/marking/heading, bir kalecide Kalecilik grubu öne çıkar. Bu, *önemi* işaretler (değeri değil) — ART_STYLE §4.1'de görsel kural (tint + accent kenar).
- **Gizli potansiyel** ve gelişim eğrisi (scout ne kadar doğru okur, bir belirsizlik ekseni).
- **Scout maskesi:** az gözlemlenmiş oyuncuda görünür attribute'lar bile kesin sayı yerine **aralık** gösterilir ("Finishing 72–86"); scout kalitesi arttıkça netleşir (CM 01/02 "hidden attributes" hissinin dereceli hâli).
- Yaş, form, sakatlık geçmişi runtime state.

**Katman 2 — Kişilik ve Trait'ler (karakteri yaratan şey):**
- **Trait'ler mekanik olarak gerçek**, flavor text değil. Her biri hem sim'i hem olay olasılığını değiştirir. Örnekler:
  - *Derbi canavarı / Büyük maçta kaybolan* → `MatchContext`'e göre etkin attribute modülasyonu.
  - *Soyunma odası lideri* → takım arkadaşlarına pasif moral aurası.
  - *Antrenman kaçkını* → gelişim penaltısı, belki yüksek ham yetenek (risk/ödül).
  - *Cam adam* → sakatlık eğilimi.
  - *Basın adamı* → medya olayları üretir (bkz. 4.7).
  - *Sadık* → transfer talep etme olasılığı düşük, sen tuttukça moral bonusu.
- **Kişilik eksenleri** (hırs, sadakat, ego, profesyonellik, mizaç) oyuncunun *kendi kararlarını* ve hangi dram olaylarına aday olduğunu sürükler.

**Katman 3 — İlişkiler:**
- Oyuncu–oyuncu (mentor/çırak, rekabet, klik) ve oyuncu–menajer bağları. Kaptanın emekli olurken bir halefi işaret etmesi, genç oyuncunun sana bağlanması — hepsi bu katmandan çıkar.

**Katman 4 — Seninle Geçmiş (Ali Yılmaz'ı yaratan katman):**
- Oyuncu *seninle* yaşadığını hatırlar: ona ilk şansı sen verdin, onu finalde yedeğe çektin, derbi golünü senin takımında attı.
- Bu kişisel yay, oyuncunun **yolculuk günlüğünde** (bkz. 4.8) tutulur ve yüzeye çıkarılır. Hikaye burada oluşur — kadrodaki 20 isimden biri, *senin hikayen* olur.

### 4.3 Taktik Sistemi

- Birkaç dizilim + birkaç oyun-tarzı ekseni (tempo, pres yüksekliği, risk). Az sayıda ama sonuç doğuran seçenek — her biri sim modelinde ölçülebilir etki yaratmalı, kozmetik olmamalı.
- Taktik–kadro uyumu ve rakibe göre adaptasyon, oyuncunun tekrar tekrar verdiği asıl karar.

### 4.4 Transfer, Scout ve Ekonomi

- Kısıtlı bütçe; her transfer bir bahis. Serbest oyuncu havuzu run başına seed'li üretilir.
- **Scout belirsizliği** temel mekanik: oyuncunun gerçek potansiyeli maskeli; scout kalitesi (meta-perk) maskeyi inceltir. "Bilgi satın alma" bir para birimine dönüşür.
- Rakip menajer AI'ı da transfer yapar. MVP'de basit kural tabanlı, sonra derinleştirilir.
- **Düşük sürtünme (CM 01/02 dersi).** Transfer akıcı olmalı: basit teklif → kabul/karşı-teklif, hızlı sonuçlanır. **Ajan bürokrasisi, klause labirenti, basın sirki yok** (bkz. §9 "asla eklenmeyecekler"). Düzenli oynayıp performans gösteren bir oyuncuya teklifler doğal gelir — sahne süresi + form, satılabilirliği besler.
- **"Keşfet-büyüt-sat" flip'i çekirdek bir ödüldür.** Bedava/ucuz bulup büyüttüğün cevheri büyük paraya satmak, oyunun Ali Yılmaz payoff'udur (bkz. 4.9 legend card). Bu *an* tatmin edici ve lucrative olmalı.
- **Ama roguelike ekonomisi gergin kalır (kritik denge).** CM 01/02'de AI'nın yanlış-değerlemesi transferi bir para-basma makinesine çeviriyordu; roguelike'ta bu run'ın riskini öldürür. GAFFER'da satmak bir *bedel* taşır: oyuncuyu kaybedersin (kadro + duygusal), board hedefi baskısı seni her cevheri satamaz tutar. Yani flip lucrative bir *an*'dır, spam'lenebilir bir döngü değil — para basma değil, tuzlu bir karar.

### 4.5 Antrenman ve Gelişim

- Basit: haftalık odak seçimi (genç geliştir / form koru / fitness). Derinlikten çok tradeoff sunar.
- Genç geliştirme meta ile bağlanır: bir run'da yetiştirdiğin cevher, itibar ve unlock besler.

### 4.7 Dram Motoru — *retention burada yaşar*

Oyunun ürettiği asıl bağımlılık maçtan değil, aralardaki hikayeden gelir. Dram motoru, oyun state'ini + oyuncu trait/kişiliklerini izleyen, koşullar tetiklenince **karar dayatan olaylar** üreten bir sistemdir.

**Üç tasarım kuralı (aksi retention'ı öldürür):**
1. **Sonuçlu olmalı.** Olay state'i değiştirir — gece kulübü skandalı gerçekten moral ve fitness düşürür. Sadece bildirim değil.
2. **Bir karar olmalı.** "Oyuncu kaçtı" değil → "yıldızın gitmek istiyor: zorla tut (moral riski), sat (kadro kaybı), ikna et (para/söz ver)". Oyuncu seçer, sonucu sahiplenir, hatırlar. Retention *senin verdiğin karardan* doğar.
3. **Nadir olmalı.** Her hafta başkan ölürse dram gürültü olur. Kıtlık, dramı değerli kılar. Olay bütçesi/cooldown ile frekans sıkı kontrol edilir.

**Emergent > scripted.** Olaylar çoğunlukla trait + state + olasılıkla *kendini üretir* (ölçeklenir). Küçük bir set el yapımı "set-piece" olay (başkanın ölümü, kulübün satılması) dramatik çıpalar koyar; gerisini sistem doğurur. El yazması hikaye kampanyası bir içerik değirmenidir — ondan kaçınılır.

**Olay kategorileri:**
- **Kişisel:** transfer talebi, kontrat krizi, saha dışı skandal, sıla hasreti, oynamak isteyen wonderkid.
- **Kurumsal:** kulüp satılıyor, başkan ölüyor, bütçe kesiliyor, yönetim değişiyor.
- **Taraftar / medya:** derbi yenilgisi sonrası protesto, basın savaşı, taraftar favorisi doğuyor.
- **İlişki:** kaptan emekli olurken halef gösteriyor, genç oyuncu sana baba gibi bağlanıyor, soyunma odası ikiye bölünüyor.
- **Rekabet:** derbi — tekrar eden, yüksek bahisli anlatı çıpası.

### 4.8 Hafıza ve Anlatı Katmanı — *"sistemi değil hikayeyi hatırlıyor"*

Oyun hikayeyi pasif bırakmaz; **aktif olarak inşa edip yüzeye çıkarır.**

- **Oyuncu yolculuk günlüğü:** debüt, "geldiğini" ilan eden maç, derbi golü, sana bağlandığı an, transferi — her oyuncunun seninle yayı kaydedilir.
- **Satış anı duygusal işaretlenir:** "Bedava buldun. 5 sezon oynattın. Manchester'a 40M'a sattın." Payoff burasıdır — transfer bir tablo satırı değil, hikayenin doruğudur.
- **Sezon sonu anlatı recap'i:** sim'in ürettiği yayları hikaye olarak geri veren özet ("Ali Yılmaz'ın Yükselişi", "Düşüşün Eşiğinden Dönüş").

### 4.9 Roguelike Meta-Progression

- **Menajer itibarı:** Ana meta para birimi. Run performansıyla artar, hangi kulüplerin seni işe alacağını belirler.
- **Perk ağacı:** Kalıcı, run'lar arası taşınan pasif avantajlar (scout, pazarlık, gençlik, moral yönetimi).
- **Arketipler:** Zamanla açılan menajer oyun-tarzı kimlikleri (ör. "gençlik akademisi ustası", "pres canavarı"). Her run'a farklı başlangıç modifier'ı ve farklı optimal strateji verir.
- **Efsaneler Salonu (Legends / Hall of Fame):** Her run kendi Ali Yılmaz'ını doğurur; keşfedip büyüttüğün oyuncular kalıcı kariyer mirasına yazılır. Dram katmanı ile roguelike katmanı birbirini besler — "3 run önce bedava bulduğum çocuk" senin efsanen olur. Bu, oyunun uzun-vadeli duygusal çıpasıdır.
- **Paylaşılabilir "legend card" *(sonraya — MVP dışı)*:** Oyuncular prosedürel olduğu için CM 01/02'deki "herkesin bildiği Tsigalko" gibi *paylaşılan* bir kültür kendiliğinden oluşmaz; senin efsanen **kişisel**. Bu boşluğu kapatmak için her efsane dışa aktarılabilir/paylaşılabilir bir karta dönüşür ("bedava buldun → 40M'a sattın", broadcast stilinde). Kişisel efsaneyi paylaşılabilir kılar — CM'in Tsigalko t-shirt'ünün modern karşılığı (görsel: ART_STYLE §7). **MVP'de yalnız Hall of Fame *çekirdeği* var** (efsanelerinin kalıcı kaydı/listesi); tasarlanmış paylaşılabilir kart post-MVP paylaşım/retention katmanına aittir (bkz. §9 ve ROADMAP "MVP sonrası ufuk").

### 4.10 Menajer Karakteri — *sen the gaffer'sın*

**İlke: menajeri önceden-yazma, projeksiyon alanı bırak.** Yönetim + roguelike oyunları oyuncu karaktere büründüğünde en iyi çalışır. Menajerin kimliği görünüşünden veya yazılmış bir geçmişinden değil, **kariyerinden ve kararlarından** gelir: "bir alt ligi şampiyon yapan, pres canavarı arketipiyle oynayan, iki wonderkid satan adam."

- **Menajerin mekanik karakteri = arketip + perk'ler.** Ayrı bir "menajer trait" sistemine gerek yok; arketip (roguelike'ın "sınıf"ı) + perk'ler + itibar + Efsaneler Salonu menajerin kim olduğunu söyler.
- **MVP (minimal, kilitli):** **isim + milliyet** + meta iskelet + (arketip yüzeye çıktığında) bir başlangıç arketip seçimi + minimal **menajer–oyuncu bağı** mekaniği ("genç oyuncu sana baba gibi bağlanıyor" beat'i, dram sütununa hizmet eder). **Görünüş/portre yok** (monogram token yeter); derin karakter-yaratma UI'ı yok.
- **Sonraya:** drama'ya hook olan menajer stilleri (adam-yönetici → moral, disiplinci → skandal/antrenman, medya-uzmanı → basın), derin arketip ağacı, menajer kökeni, board ile ilişki.
- **Alternatif yön:** daha RPG-vari bir menajer (görünüş, yazılmış kişilik, backstory) yapılabilir — ama projeksiyonu zayıflatır ve MVP'yi şişirir. Öneri: hafif tut.

---

## 5. Progresyon ve Retention

- **Asıl retention motoru: hikaye.** İnsanlar mekaniği değil, bulup büyüttükleri oyuncuyu, verdikleri dramatik kararı, kaybettikleri derbiyi hatırlar. Dram motoru (4.7) + hafıza katmanı (4.8) bu yüzden bir "özellik" değil, tutma stratejisinin merkezidir.
- **Kısa vadeli kanca:** Sıradaki maç, sıradaki transfer penceresi, çözülmeyi bekleyen bir dram kararı.
- **Orta vadeli:** Sezon hedefini tutturmak, yükselmek, bir gencin yayını tamamlamak.
- **Uzun vadeli:** Perk/arketip unlock, "efsane menajer" kariyeri, Efsaneler Salonu, daha büyük kulüpler.
- Erken sürümlerde zamana-yayılan agresif mekanik (idle/timer) yok — önce core loop + dram'ın kendisi eğlenceli ve tekrar oynanabilir olmalı.

---

## 6. Monetizasyon

Erken sürümde monetizasyon yok ya da minimal; önce tutma (retention) kanıtlanır. Sonrasında düşünülecek, oyuncu-dostu ve türe uygun seçenekler:

- **Kozmetik / meta hızlandırma** (agresif paywall değil).
- **Ödüllü reklam** — ekstra scout raporu, run "devam" hakkı gibi *değer katan* yerlerde.
- **Tek seferlik premium unlock** (reklamsız + ekstra arketip).

Sıralama net: önce eğlence ve tutma, sonra para. Para modeli çekirdeği bozmayacak.

---

## 7. Teknik Mimari

### 7.1 Simülasyon çekirdeğini motordan ayır

Tüm simülasyon mantığı **saf C#**, Unity'ye (MonoBehaviour'a) bağımsız bir katman.
- **Headless çalışır** — Unity açmadan binlerce sezon simüle edilebilir (bkz. Bölüm 9).
- **Unit-test edilebilir** — sim mantığı oyun/editor bağımsız test edilir.
- **Temiz sınır** — sim, sunum katmanına *event* yayar; UI sim'i çağırır ama içine sızmaz.

### 7.2 Veri modeli

- **Statik/config veri:** ScriptableObject (attribute tanımları, taktik parametreleri, denge sabitleri) — editor'de tweak'lenebilir.
- **Runtime state:** Düz `[Serializable]` C# sınıfları (Unity referansı yok), sim çekirdeğinde yaşar.
- **Save/Load:** JSON serileştirme + **versiyonlama** (şema değişince eski save'leri migrate et).

### 7.3 Determinizm ve test edilebilirlik

- Seed'li RNG, enjekte edilen bir `IRandom` arayüzü üzerinden (global `Random` yok) → testlerde sahte RNG, oyunda seed'li RNG.
- Deterministik sim → reproducible hatalar, "aynı seed" debug, replay.

### 7.4 Yapı

- Sim alt-sistemleri (chance generation, resolution, event) arayüzlerle ayrık; bağımlılıklar enjekte edilir.
- Event-driven maç akışı: sim olay yayar, sunum katmanı dinler.
- Sim CPU-bound ve hafif; batch/headless sim için sıcak yollarda allocation'lara dikkat (pooling, struct).

### 7.5 Karakter ve Dram sistemleri

- **Trait'ler veri-güdümlü:** Her trait bir ScriptableObject + davranış; sim'e ve dram motoruna *hook* olarak enjekte edilir (yeni trait eklemek kod değil veri işi olsun).
- **Dram motoru bir kural + ağırlık + cooldown sistemi:** state ve trait'leri izleyen tetikleyiciler, olasılıkla olay seçer, olay bütçesiyle frekansı sınırlar. Olaylar da (trait'ler gibi) veri olarak tanımlanır → içerik kod dokunmadan büyür.
- **`MatchContext`:** maçın önemini taşıyan girdi; hem sim'i (trait modülasyonu) hem anlatı üretimini besler.
- **Anlatı/hafıza katmanı sim'in *tüketicisidir*:** sim ham olayları yayar, anlatı katmanı bunları oyuncu yolculuk günlüğüne ve recap'e dönüştürür. Sim bu katmandan habersiz kalır (tek yönlü bağımlılık, temiz sınır korunur).

---

## 8. UI / UX

Mobil-first, tablo-hafif. MVP için gereken çekirdek ekranlar:

1. **Kadro / taktik** — maç öncesi ana karar ekranı
2. **Maç anlatısı** — dakika dakika text/minimal 2D akış
3. **Sezon / lig durumu** — puan durumu, fikstür, hedef
4. **Transfer / scout** — havuz, teklifler, scout raporları
5. **Menajer / meta** — itibar, perk, arketip

Unity UI Toolkit önerilir (veri-bağlama ve tablo ağırlıklı UI için uygun). Küçük ekran, oyunu sadeliğe zorlar — bu bir avantaj.

---

## 9. Kapsam — MVP

**Ship edilebilir küçük şey > bitmemiş büyük şey.**

**MVP:**
- Tek lig + basit kupa, birkaç yüz kurgusal oyuncu
- İstatistiksel maç sim + **isimli, bağlam-duyarlı** text anlatı (görsel yok)
- **Sınırlı ama gerçek karakter katmanı:** ~6–8 mekanik trait (derbi canavarı, soyunma odası lideri, cam adam, antrenman kaçkını vb.) + temel kişilik eksenleri
- **Çekirdek dram motoru:** ~8–12 sonuçlu, kararlı olay (transfer talebi, skandal, taraftar tepkisi, kaptan emekliliği gibi) + frekans kontrolü
- **Hafıza minimumu:** oyuncu yolculuk günlüğü + duygusal satış anı + sezon sonu recap
- Taktik + kadro + transfer + basit scout + antrenman
- Bir tam sezon = bir run; yükselme/kovulma; basit meta-progression (itibar + birkaç perk + Efsaneler Salonu çekirdeği)
- Save/load, sezon akışı, 5 çekirdek ekran
- Monetizasyon yok

> Karakter ve dram MVP'nin dışına atılamaz — çünkü oyunun *soul*'u onlar. Ama küçük ve veri-güdümlü tutulur: az sayıda trait/olay, sonradan içerik olarak büyür.

**MVP'ye girmeyecekler (sonraya):**
- 2D "key moments" görselleştirme
- İdle/idle-hybrid katman
- **Derin ilişki ağı** (oyuncu-oyuncu klikleri, mentor zincirleri) — MVP'de temel ilişkiler yeter
- **Geniş dram içeriği** (yüzlerce olay, el yapımı set-piece zincirleri) — MVP çekirdek motor + küçük olay setiyle sınırlı
- Derin arketip ağacı, çok ligli dünya
- Community data / gerçek isim desteği
- Monetizasyon
- Rakip AI'ın gelişmiş taktik/transfer zekası (MVP'de kural tabanlı yeter)

**Bilinçli olarak ASLA eklenmeyecekler (CM 01/02 dersi — "sonraya" değil, hiç):**
Fanların modern FM'den kaçıp CM 01/02'ye sarılma sebebi onun **yalınlığı** ve hızıdır (bir sezon bir öğleden sonrada biter). GAFFER şu şişkinliği asla eklemez: basın toplantıları, board/yönetim toplantıları maratonu, ajan pazarlığı bürokrasisi, sosyal medya simülasyonu, oyuncu-ego İK yükü. Dram sistemi bunun **panzehiridir**: dram bir İK yükü değil, hızlı-tatmin eden bir *karar anıdır* — sürtünme değil, tuz. Sadelik bir eksik değil, bilinçli ve korunacak bir özelliktir.

---

## 10. Doğrulama Planı

İlk kodlanan şey UI değil, maç simülasyon fonksiyonu. UI'dan önce headless olarak binlerce sezon simüle edilir ve dağılımlara bakılır:

- **Şampiyon dağılımı** makul mü? (Favoriler öne çıkıyor ama tek takım domine etmiyor.)
- **Gol dağılımı** sağlıklı mı? (Maç başı ~2.5–3 gol civarı, aşırı uçlar seyrek.)
- **Favori–sürpriz dengesi** doğru mu? (Güçlü takım genelde kazanıyor ama upset oranı inandırıcı.)
- **Puan tablosu** inandırıcı mı? (Sezon sonu tablo gerçek bir ligi andırıyor mu?)

Bu, "eğlenceli mi" değil **"inandırıcı mı"** sorusunu erkenden cevaplar. Çekirdek tutmadan üstüne UI koymak boşa emektir.

---

## 11. Yol Haritası

| Aşama | Çıktı | Amaç |
|---|---|---|
| **0. Sim çekirdeği** | Saf C# maç sim + seed'li RNG + headless runner | İnandırıcılığı kanıtla |
| **1. Doğrulama & tuning** | Dağılım analizleri, denge tuning | "İnandırıcı mı" evet olana kadar |
| **2. Sezon iskeleti** | Lig akışı, save/load, hedef/kovulma | Bir run baştan sona çalışsın |
| **3. Yönetim sistemleri** | Transfer, scout, taktik, antrenman | Karar döngüsü tamam |
| **4. Meta katman** | İtibar, birkaç perk | Run'lar arası çekim |
| **5. UI & cila** | 5 ekran, maç anlatı sunumu | Oynanabilir |
| **6. MVP ship** | Dar ama kapanmış oyun | Yayına hazır |

---

## 12. Riskler

- **Denge tuning'i zaman yiyor.** En büyük risk; erken headless doğrulama ile öne alınır.
- **Scope kayması.** "Bir de şunu eklesem" en büyük düşman. MVP kesim listesine sadık kal.
- **Sistemik AI derinliği.** Rakip transfer/taktik AI'ı sonsuz iterasyon kuyusu; MVP'de kural tabanlı tut.

---

## 13. Alternatif Yönler

Bu doküman **roguelike-sezon** çekirdeğine commit ediyor. Anchor değişirse:

- **Idle/management hybrid:** Sen yokken kulüp "yaşar", dönünce karar verirsin. Güçlü retention ama dengesi acımasız. Meta katmanına sonradan opsiyonel eklenebilir.
- **Saf underdog kariyeri (roguelike'sız):** Duygusal olarak güçlü ama düz FM-lite'a yakın, daha az farklılaşmış.
- **Dar dikey — sadece scout/transfer:** Maç sim'i minimal, tüm derinlik scouting'de. En küçük scope, en hızlı ship.

---

*Bu doküman yaşayan bir tasarım pusulasıdır; sim çekirdeği doğrulandıkça (Bölüm 10) sayısal denge ve sistem detayları güncellenir.*
