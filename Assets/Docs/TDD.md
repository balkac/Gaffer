# GAFFER — Teknik Tasarım Dokümanı (TDD)
**Platform:** Mobil (iOS / Android) · **Motor:** Unity 6 (`6000.3.16f1`) · **Pipeline:** Universal 2D (URP 2D) · **Dil:** C#

> Bu doküman **oyuna-özel** teknik kararları taşır. *Nasıl inşa edilir* (mimari, katmanlar, konvansiyonlar, performans, test) için **engineering-standards** klasörüne devreder: `ARCHITECTURE.md`, `CONVENTIONS.md`, `PERFORMANCE.md`, `starter-tree.md`. **Çelişki olursa standartlar kazanır.** Oyunun *ne* olduğu için `GDD.md`'ye bakılır; görsel dil için `ART_STYLE`.
>
> Claude Code'a: bu dosyayı `docs/TDD.md`'ye koy, `CLAUDE.md`'den referansla. Standartları repo'ya kopyala (kökte `.editorconfig`/`.gitattributes`, `docs/` içinde playbook'lar; `starter-tree.md`'den iskele).

---

## 1. Amaç ve Kullanım

- Kararları taşır, implementasyonu değil. Kod örnekleri **illüstratiftir** — sözleşmeyi gösterir, bağlayıcı değildir; gerçek kod `CONVENTIONS.md`'ye uyar.
- **[NON-NEGOTIABLE]** etiketli kurallar ihlal edilmez.
- Kod stili, isimlendirme, SOLID, hata modeli, katman/klasör/async kuralları burada **tekrarlanmaz** — `CONVENTIONS.md` + `ARCHITECTURE.md` bağlayıcıdır. Bu doküman yalnız onların GAFFER'a nasıl **eşlendiğini** ve oyuna-özel tasarımı anlatır.

---

## 2. Teknik İlkeler (standartlara uyum)

Hepsi engineering-standards'tan gelir; burada GAFFER'a bağlanır.

1. **[NON-NEGOTIABLE] Saf çekirdek framework'süz.** `Domain` + `Application` `UnityEngine`'e referans vermez; `.asmdef` `noEngineReferences` ile derleme-zamanı zorlanır. *Sim'i Unity açmadan `dotnet test` ile çalıştırabilmeliyim.* (ARCHITECTURE §1, §5)
2. **[NON-NEGOTIABLE] Deterministik sim.** Enjekte edilen `IRandom` (seed'li); global `UnityEngine.Random`/`System.Random` yasak. Somut tip algoritma adıyla: `SplitMix64RandomNumberGenerator` (aile adı değil — CONVENTIONS §2). Aynı seed + input → aynı output. (CONVENTIONS §4)
3. **[NON-NEGOTIABLE] İçerik veri-güdümlü.** Trait/olay/taktik/denge = ScriptableObject; yeni içerik = asset, kod değil.
4. **[NON-NEGOTIABLE] Command in → outcome out.** Çekirdek bir *komut* alır, *değişmez bir outcome kaydı* döner; sunum onu **replay** eder, çekirdeğe geri uzanıp durum diff'lemez. `Result<T>` ile eşleşir. (ARCHITECTURE §8)
5. **[NON-NEGOTIABLE] Hata modeli.** Beklenen/kurtarılabilir hata `Result`/`Result<T>` döner; bozulan invariant fail-fast `throw`. `Result` dependency-free `Common`'da. Üçüncü-parti exception'ları adapter sınırında yakalayıp `Result.Failure`'a çevir. (CONVENTIONS §4)
6. **Async sınırı.** Async yalnız Infrastructure+ (Unity'de UniTask); `Domain`/`Application` senkron kalır, asla `Task`/`UniTask` dönmez. (ARCHITECTURE §5)
7. **Davranış varyasyonu enjekte edilen port ile** (kompozisyon > kalıtım); her implementasyon port'un sözleşmesini korur (LSP). (ARCHITECTURE §9)
8. **Test önce sim.** UI'dan önce headless doğrula (Bölüm 11).

---

## 3. Katman Eşlemesi (`.asmdef` — ARCHITECTURE §1–2 + `starter-tree.md`)

Standart şema **birebir** uygulanır; **katman başına tek assembly**, feature'lar klasör. GAFFER modülleri şöyle oturur:

| Katman (assembly) | GAFFER içeriği |
|---|---|
| `Gaffer.Common` | `Result` / `Result<T>`, `IRandom` + `SplitMix64RandomNumberGenerator` |
| `Gaffer.Domain` | `Player`, `Club`, `League`, `ManagerCareer` + değer nesneleri; saf `Trait` / `DramaEvent` tanımları. **Framework yok, exception yok.** |
| `Gaffer.Application` | Use case'ler, feature klasörleri: `Simulation/`, `Generation/`, `Drama/`, `Narrative/`, `Season/`. **Saf + senkron.** |
| `Gaffer.Infrastructure` | ScriptableObject config'ler (`TraitSO`, `DramaEventSO`, `BalanceSO`) + config→Domain köprüsü; JSON serileştirme + save/load I/O; localization tabloları. Framework-coupled, **async izinli**. |
| `Gaffer.Presentation` | UI Toolkit view'ları, input, animasyon. |
| `Gaffer.Composition` | Bootstrapper + elle wiring + entry point. İsim **`Composition`**, `App` değil (ARCHITECTURE §1). |
| `Gaffer.Editor` | Editör araçları. |
| `Gaffer.Tests` / `Gaffer.Tests.Unity` | Saf testler (`dotnet`) / Unity Test Runner. |

**Eski taslaktan değişen:** `Core.Sim` / `Core.Drama` / `Core.Narrative` ayrı assembly'leri → **tek `Application` altında feature klasörleri** (ARCHITECTURE §2: katman başına tek assembly). `Core.Model`→`Domain`, `Data`→`Infrastructure`, `Game`→`Presentation` + `Composition`; yeni `Common` (`Result`/`IRandom`).

**Config→model köprüsü (kritik):** `TraitSO` / `DramaEventSO` *authoring yüzeyidir* ve `Infrastructure`'da yaşar; yüklemede saf `Trait` / `DramaEvent` (`Domain`) tiplerine map'lenir. Saf katmanlar SO'yu değil, map'lenmiş saf tipi tüketir → çekirdek framework'süz kalır (ARCHITECTURE §7 "config as override" ruhunda).

**Bağımlılık kuralı (NON-NEGOTIABLE):** `Common`/`Domain`/`Application` `.asmdef`'lerinde `UnityEngine` referansı yok (`noEngineReferences: true`). `starter-tree.md`'deki referans tablosu bağlayıcıdır.

---

## 4. Klasör Yapısı (`starter-tree.md`'den, `MyGame`→`Gaffer`)

```
Assets/_Project/Scripts/
  Common/            Gaffer.Common          — Result, IRandom + SplitMix64RandomNumberGenerator
  Domain/            Gaffer.Domain   → Common          — Player, Club, League, ManagerCareer, Trait, DramaEvent (saf)
  Application/       Gaffer.Application → Domain, Common
    Simulation/                             — SimulateMatch use case (orchestrator + IChance*/IStrengthModel)
    Generation/                             — PlayerGenerator (isim/attribute/trait üretimi)
    Drama/                                  — dram motoru (tick, seçim, effect)
    Narrative/                              — JourneyLog derleme, recap
    Season/                                 — sezon akışı orkestrasyonu
    Serialization/                          — ISerializer + DTO'lar (impl Infrastructure'da)
  Infrastructure/    Gaffer.Infrastructure → Application, Domain, Common (+ UniTask)
    Configuration/                          — TraitSO / DramaEventSO / BalanceSO + SO→Domain köprü
    Persistence/                            — JSON serializer impl, save/load I/O (async)
    Localization/                           — String Table erişimi
  Presentation/      Gaffer.Presentation → Application, Domain (+ Common)
    <Feature>/Views/                        — UI Toolkit view'ları (UXML/USS)
    Input/
  Composition/       Gaffer.Composition → tümü          — GameBootstrapper, wiring, entry point
  Editor/            Gaffer.Editor
  Tests/
    EditMode/        Gaffer.Tests   → Domain, Application, Common   — saf, dotnet + Unity
    EditModeUnity/   Gaffer.Tests.Unity → + UnityEngine
Tools/
  SeasonHarness/                            — 1000-sezon dotnet konsol runner (→ Application)
docs/   GDD.md · TDD.md · ART_STYLE.html · ROADMAP.md · engineering-standards/
Assets/_Project/Art/                        — SVG kaynak + sprite atlas (bkz. Bölüm 13)
```

`.asmdef` referans kuralları ve `dotnet test` köprüsü (`tests/Gaffer.Tests.csproj`) için **`starter-tree.md` bağlayıcıdır.**

---

## 5. Veri Modeli (`Domain` — oyuna özel)

> İllüstratif; gerçek kod `CONVENTIONS.md`'ye uyar (cheap değerler için **property**, `_camelCase` private alanlar, one-public-type-per-file, verb+object metotlar, `Domain`'de **exception yok**).

```csharp
// Gaffer.Domain
public sealed class Player {
    // Katman 1 — rakamlar
    public Attributes Attributes { get; }       // dar set (~8-12)
    public int HiddenPotential { get; }          // scout maskeler
    // Katman 2 — kişilik
    public IReadOnlyList<TraitId> Traits { get; }
    public Personality Personality { get; }      // hırs, sadakat, ego, profesyonellik, mizaç
    // Katman 3 — ilişkiler
    public IReadOnlyList<Relationship> Relationships { get; }
    // Katman 4 — seninle geçmiş (Ali Yılmaz katmanı)
    public JourneyLog Journey { get; }           // debüt, kilit anlar, transfer — bkz. Bölüm 9
}

public struct Attributes {                       // struct: sıcak yolda allocation yok (PERFORMANCE); 0–100 (byte)
    // Teknik
    public byte Finishing, Technique, FirstTouch, Dribbling, Passing, Crossing, Heading, LongShots, Marking, Tackling;
    // Set-piece
    public byte Penalties, FreeKicks, Corners, LongThrows;
    // Fiziksel & Hareket
    public byte Pace, Acceleration, Stamina, Strength, Agility, Jumping, Balance, Positioning;
    // Kalecilik (yalnız GK anlamlı; outfield ~0)
    public byte Reflexes, Handling, AerialReach, CommandOfArea, OneOnOnes, Kicking, GkPositioning;
}
// NOT: FM'in "mental" ekseni (composure, vision, flair, work rate, off-the-ball, anticipation,
// decisions, leadership, aggression) burada YOK — Trait + Personality soğurur (çift-sayım yok).
// Pozisyona göre "rol-anahtarı" attribute'lar UI'da vurgulanır (bkz. ART_STYLE §4.1); bu bir
// gösterim kuralıdır, ayrı veri değil (rol → key-attribute eşlemesi config/Domain'de).

public sealed class ManagerCareer {              // meta, run'lar arası kalıcı
    public int Reputation { get; }
    public IReadOnlyCollection<PerkId> Perks { get; }
    public IReadOnlyCollection<ArchetypeId> Archetypes { get; }
    public IReadOnlyList<LegendRecord> HallOfFame { get; }   // senin yarattığın efsaneler
}
```

**Id disiplini:** `Trait`/`Perk`/`Event` `enum` değil hafif `Id` ile referanslanır — tanım *veride* yaşar.

**[NON-NEGOTIABLE] Oyuncular prosedürel üretilir — elle yazılmaz.** Tek kadro oyuncusu bile hardcode edilmez; `Application/Generation`'daki bir use case seed bazlı üretir (isim + **milliyet** + attribute + gizli potansiyel + trait/kişilik ağırlıklı atama). "Ali Yılmaz" bir tasarım varlığı değil, *bir run'da üretilmiş bir ismin* alabileceği değer. Oyuncuyu unutulmaz yapan onu *bulmak*; script'lenmiş karakter verilmiş olur. Hikaye hissini karakterin kendisi değil **üreteç + hafıza katmanı** (Bölüm 9) sağlar.

```csharp
// Gaffer.Application.Generation
public interface IPlayerGenerator {
    Player Generate(GenerationContext context, IRandom rng);   // deterministik
}

// GenerationContext (illüstratif): milliyet dağılımı, yaş bandı, potansiyel eğrisi, trait ağırlıkları...
// + keşif garantisi (aşağı bkz.):
//   int   GuaranteedHiddenGemsPerRun;   // her run'da garanti "keşfedilebilir cevher" sayısı
//   Band  gemVisibleBand;               // düşük GÖRÜNÜR değer (scout maskeli)
//   Band  gemHiddenPotentialBand;       // yüksek GİZLİ potansiyel
//   ...   // düşük fiyat + alt lig kulüplerine yerleştir
```

**[NON-NEGOTIABLE] Keşfedilebilir wonderkid garantisi (CM 01/02 dersi).** Keşif fantezisi tesadüfe bırakılamaz. Üreteç her run'da, **alt liglerde, ucuz, düşük-görünür-değerli ama yüksek-gizli-potansiyelli**, keşfedilmeyi bekleyen az sayıda oyuncu **garantili** üretir. Bunlar nadir olmalı (bulmak özel hissetsin) ama daima var olmalı. Scout belirsizliği (Bölüm 7 / gizli potansiyel maskesi) tam da bu cevherleri gizler; "bilgi satın alma" onları açığa çıkarır. CM 01/02'nin Tsigalko/Cherno Samba/Tô Madeira hissi burada doğar.

*İstisna (MVP dışı):* küçük el-yapımı "set-piece" varlıklar (efsane rakip menajer, veteran) sonra eklenebilir; kadro oyuncuları her zaman üretilir.

---

## 6. Simülasyon (`Application` use case — command→outcome)

Maç sim'i bir **use case**'tir: bir komut alır (iki takım + `MatchContext`), **değişmez bir outcome** döner; `Presentation` outcome'u replay eder (ARCHITECTURE §8).

```
SimulateMatch(command: MatchCommand, rng: IRandom) -> MatchOutcome
    1. BuildEffectiveStrength(team, ctx)   // attribute + taktik + form + moral + TRAIT modülasyonu
    2. GenerateChances(strengths, rng)     // Poisson-benzeri, tempo = orta saha oranı
    3. ResolveChances(chances, rng)        // xG-benzeri kalite × bitiricilik/kaleci
    4. -> MatchOutcome { Score, IReadOnlyList<MatchEvent> }   // event akışı = anlatının ham maddesi
```

**`MatchContext` — karakteri maça bağlayan köprü (NON-NEGOTIABLE):** maçın *önemini* taşır (`Importance`: Normal/Derby/Final/RelegationSixPointer, `CrowdSize`, `IsTitleDecider`, `Rivalry`). Adım 1'de trait'ler `ctx`'e bakar: "Derbi canavarı" `Derby`'de etkin attribute'u yükseltir, "Büyük maçta kaybolan" düşürür.

**Alt-sistemler port arkasında** (ARCHITECTURE §9): `IStrengthModel`, `IChanceGenerator`, `IChanceResolver`. Denge tuning'i implementasyonu değiştirir; pipeline sabit.

**Rakip menajer = "bir sonraki kararı kim veriyor" port'u** (ARCHITECTURE §9): `IManagerDecisionSource` döngüden komut üretir; şimdi kural-tabanlı AI, ileride replay/networked peer **aynı komut, aynı çekirdek** — ek kod yolu değil. **Komutu actor ile etiketle** (hangi menajer) ki ikinci katılımcı eklemek additive olsun.

---

## 7. Trait Sistemi (Infrastructure SO → Domain, Application tüketir)

**Amaç:** yeni trait = yeni asset. Authoring `Infrastructure`'da SO, çekirdek saf `Trait`'i tüketir.

```csharp
// Gaffer.Infrastructure.Configuration
[CreateAssetMenu]
public sealed class TraitSO : ScriptableObject {
    // Sim hook'ları: ctx-koşullu MatchModifiers, MoraleAura (soyunma odası lideri),
    // InjuryProneness (cam adam), DevelopmentModifier (antrenman kaçkını)
    // Dram hook'ları: BiasedEvents (bu trait hangi olaylara aday yapar)
    // Metin alanları LOCALIZATION KEY tutar (Bölüm 12.5), literal değil
}
```

Köprü `TraitSO`'yu saf `Domain.Trait`'e map'ler; `Application/Simulation` adım 1'de uygular, `Application/Drama` ağırlıklandırmada kullanır. **Tanım tek yerde, iki sistemde okunur.**

---

## 8. Dram Motoru (`Application/Drama`; olaylar Infrastructure SO)

**Şema — her olay bir veri kaydı** (`DramaEventSO` → saf `DramaEvent`): `Category`, `Triggers` (state+trait koşulları), `BaseWeight`, `CooldownWeeks`, `Choices` (her biri `Effect[]` → state değiştirir), `Copy` (localization key'li şablon).

**Motor döngüsü (haftalık tick, `Application`):** uygun olayları filtrele → ağırlık + trait bias ile seç (`IRandom`) → olay bütçesi uygula (frekans tavanı) → oyuncuya **KARAR** sun → seçim → `Effect`'ler (state değişir, `Result` ile) → `JourneyLog`+`Narrative`'e yaz.

**Üç kural motorda zorlanır (GDD):** sonuçlu (Choice→Effect), kararlı (≥2 anlamlı Choice), nadir (cooldown + bütçe). Set-piece olaylar (başkan ölümü, kulüp satışı) el yapımı; gerisi emergent.

---

## 9. Anlatı & Hafıza (`Application/Narrative` — sim+dram outcome tüketicisi)

Tek yönlü bağımlılık: sim/dram'ın döndürdüğü **outcome**'ları tüketir, çekirdeğe geri uzanmaz.

- **Maç event → beat:** "19'unda ilk derbi golü" oyuncunun `JourneyLog`'una yazılır.
- **Satış anı:** transfer bir beat değil *doruk*; duygusal işaretlenir ("bedava buldun → 40M").
- **Sezon recap:** beat'lerden anlatı özeti.
- **Efsaneler Salonu:** öne çıkan oyuncular `ManagerCareer.HallOfFame`'e (run'lar arası kalıcı).

Bu katman **read-model** üretir; state'in sahibi değildir.

---

## 10. Save / Load

- **Port `Application`'da, impl `Infrastructure`'da.** `ISerializer` (Application/Serialization) soyut; JSON impl + dosya I/O `Infrastructure/Persistence` (async, UniTask).
- **[NON-NEGOTIABLE] Versiyonlama:** her save `schemaVersion` taşır; şema değişince migration.
- **Sınırda hata:** JSON/IO exception'ları adapter'da yakalanır → `Result.Failure` (CONVENTIONS §4); exception saf katmana sızmaz.
- **Ayrım:** config (SO) serialize edilmez; yalnız runtime state. Yüklemede `TraitId`→SO yeniden bağlanır.
- **Format:** Newtonsoft (polimorfizm gerektiği için `JsonUtility` yerine).
- Seed + kararlar saklanırsa run **replay** edilebilir (deterministik).

---

## 11. Test & Doğrulama

Devreder: **`CONVENTIONS.md` §5 + `starter-tree.md` `dotnet test` köprüsü** bağlayıcı. Editörü açmadan `dotnet test` ile headless doğrula; suite her değişiklikte yeşil. Test isimleri `Scenario_Condition_Result` (ör. `SimulateMatch_WithSeed_IsDeterministic`).

- **İnandırıcılık harness'ı (ilk iş):** `Tools/SeasonHarness` — `Application` üstünde 1000 sezon simüle eden **dotnet konsol** (Unity'siz). Kontrol: şampiyon dağılımı, gol ~2.5–3/maç, favori–sürpriz dengesi, tablo inandırıcılığı. Soru "eğlenceli mi" değil **"inandırıcı mı"**.
- **Trait doğrulaması:** trait'in sim çıktısını ölçülebilir değiştirdiğini test et (flavor tuzağı).
- **Dram frekans testi:** uzun simülasyonda olay frekansı bütçe içinde (enflasyon regresyonu).
- **Keşif doğrulaması (Faz 3+, üreteç geldiğinde):** çok-sezon simülasyonda ara sıra düşük-görünür/genç oyuncuların gerçekten patladığını ve garanti "keşfedilebilir cevher"in (Bölüm 5) cevhere dönüştüğünü ölç. Gate A'daki "favori kazanır / upset inandırıcı"dan ayrı bir metrik: *keşif fantezisi gerçek mi?* Aşırı uçta olmasın (her cevher patlarsa keşif değersizleşir), ama sıfır da olmasın.
- **Transfer ekonomisi doğrulaması (Faz 3+):** çok-sezon simülasyonda transferin bir *para-basma makinesine* dönüşmediğini ölç (CM 01/02 dersi). "Keşfet-büyüt-sat" flip'i ödüllü olmalı ama AI mis-valuation'la kasa sonsuza şişmemeli; run ekonomisi gergin kalmalı — aksi hâlde roguelike'ın riski ölür.

---

## 12. Tech Stack

- **Unity:** `6000.3.16f1` (Unity 6). Pinli.
- **Pipeline:** **Universal 2D (URP 2D)** — kilitli. Gerekçe: oyun UI Toolkit ağırlıklı (pipeline'a çoğunlukla dokunmaz), ama ertelenen **2D "key moments" viewer** için sahne-içi 2D gerekecek; URP 2D modern/bakımlı, mobilde iyi defaultlar, sonradan migration derdini önler (düşük-pişmanlık, ileri-dönük). *Kurulum notu:* 2D template'in örnek içeriğini (2D ışık sahnesi vb.) sıyır — iş UI Toolkit'te; pipeline sadece viewer için hazır dursun.
- **UI:** UI Toolkit (UXML/USS) — **her şey için**, uGUI'ye girme. GAFFER veri/tablo-yoğun + mobil; retained visual tree bu profilde öngörülebilir performans verir. Sinerji: `ART_STYLE` token'ları ≈ USS değişkenleri, SVG ikon/armalar ≈ UI Toolkit vektör. Not: `PERFORMANCE.md` §2'nin world-space HUD önerisi *küçük sabit HUD* içindir; büyük otomatik-yerleşimli UI'yi kendi dokümanın da UI Toolkit'e yönlendirir. Sık güncellenen öğeleri (tıklayan saat/score bug) izole et ki tüm paneli invalidate etmesin ("update frequency'ye göre böl" ruhu). TextMeshPro uGUI'ye bağlı; UI Toolkit kendi metnini kullanır.
- **Animasyon:** UI-only olduğu için ağırlıkla USS transition/animation. `PERFORMANCE.md`'nin DOTween/ParticleSystem/SpriteRenderer-pool alışkanlıkları GAFFER'da büyük ölçüde N/A; ama **yapısal** perf ilkeleri (az MonoBehaviour, pooling, idle'da sıfır allocation, O(n) çekirdek, config'ten bütçe) aynen geçerli.
- **Serialization:** Newtonsoft (Bölüm 10).
- **Kod stili / isimlendirme / SOLID / hata:** `.editorconfig` + `CONVENTIONS.md` bağlayıcı (burada tekrarlanmaz). Namespace `Gaffer.<Katman>.<Feature>`; klasör = assembly son segmenti (ARCHITECTURE §2).

---

## 12.5 Yerelleştirme (i18n)

Localization önce mimari, sonra dil kararıdır. Sistem doğru kurulursa dil eklemek çeviri işi olur, refactor değil.

- **[NON-NEGOTIABLE] Ham kullanıcı-metni yok.** Tüm UI *ve* anlatı metni bir localization sisteminden, **stabil semantik key**'lerle gelir (`event.transfer_request.title` gibi). Kod ve veri key referanslar, düz metin değil.
- **Kaynak/referans locale = İngilizce (`en`).** Tüm gelecek çevirilerin pivotu; en iyi araç/LLM desteği ve referans bolluğu.
- **Lansman locale'leri = İngilizce + Türkçe (`tr`).** Türkçe **native** yazılır (makine çevirisi değil) — dramın duygusal kalitesi korunur. İkili seçim yok: İngilizce erişim + Türkçe native kalite, ikisi de day-one.
- **Araç:** Unity Localization paketi (`com.unity.localization`) — String Table, Locale yönetimi, Smart String. Sayı/para (transfer bedeli)/tarih formatı locale-aware.

**Anlatı şablonu yerelleştirmesi (bu oyuna özel tuzak):** Dram/maç metni şablondan, varlık interpolasyonuyla üretilir — sabit string'den zordur.
- Türkçe (ve diğer çekimli diller) interpole edilen isme **ek uyumu** ister (ünlü uyumu: `{club}'a/e karşı`). İngilizce neredeyse çekimsizdir → basit; bu da İngilizce'yi referans locale yapmanın bir nedeni.
- **MVP stratejisi:** şablonları localization-dostu kur — interpole varlıkları atomik tut, cümle-ortası çekim bağımlılığından kaçın, token'ları ek gerektirmeyen konumlara yerleştir.
- **Sonraya (opsiyonel):** algoritmik ünlü uyumuyla Türkçe ek üreten yardımcı fonksiyon — doğal çekim için, MVP'de şart değil.
- MVP'de şablon sayısı az (~8–12 olay) → localization maliyeti şablonla ölçeklenir, oynanışla değil. Şimdi doğru kurmak neredeyse bedava.

**Veri modeli notu:** `TraitSO` / `DramaEventSO` içindeki metin alanları (ör. `NarrativeTemplate Copy`) *literal metin değil, localization key* tutar.

---

## 13. Sanat Yönetimi (Art Style Bible)

> Tüm görsel işleri Claude üretecek. AI ile art'ta en zor şey **tutarlılık**. Bu yüzden yön, tutarlı üretilebilir bir sisteme göre seçildi — illüstrasyona değil, **token'lı vektör sistemine** dayanıyor. Bu aynı zamanda Claude'un güvenilir ürettiği şeyle (temiz SVG) örtüşüyor: seçim hem estetik hem pratik.

### 13.1 Yön ve gerekçe
**Yön: "Matchday Broadcast Graphics".** Arayüz, yönettiğin ama asla izlemediğin bir maçın *yayın grafikleri paketidir*: score bug, dizilim lower-third'ü, taktik tahtası. "Görsel maç motoru yok" kısıtını bir zayıflık olmaktan çıkarıp kimliğe çevirir. Gece-sahası teal zemini + yayın magentası accent + condensed sportif tipografi.

**Neden bu yön (solo + AI-üretim için):**
- Tipografi + vektör + token, illüstre karakterlerden **çok daha tutarlı** üretilir.
- Kulüp armaları/ikonlar **parametrik SVG** ile yüzlerce kez tutarlı çıkar.
- "Koyu zemin + tek acid-accent" ve "cream + editorial" kombinasyonları şu an AI-default (templated) okunur; broadcast yönü bunlardan ayrışır ve konuya bağlıdır.
- **Kaynak referansı:** `docs/ART_STYLE` levhası (bu bölümün render edilmiş, bağlayıcı hali). Çelişki olursa levha kazanır.

### 13.2 Renk sistemi (token — bağlayıcı; kaynak: `docs/ART_STYLE`)
- `--pitch` (taban zemin): `#0C1B1A` (gece-sahası teal-siyah)
- `--pitch-raised` (kart/yüzey): `#122624` · `--pitch-line` (çizgi/kenarlık): `#1E3733`
- `--chalk` (birincil metin): `#EAF2EE` · `--muted` (ikincil): `#7C938C`
- `--accent` (İMZA, tek accent — yayın magentası): `#FF2E7E` *(alternatif, takas edilirse: elektrik-lime `#E8FF3A`)*
- **Semantik (yalnız sonuç bağlamı):** `--win #2FD48A`, `--loss #FF5A4D`, `--draw #E7B84B`
- Kural: tek accent + zengin nötr taban. İkinci parlak renk *eklenmez* (semantikler hariç).

### 13.3 Tipografi
- **Display/başlık:** güçlü condensed grotesque (editoryal-sportif his) — açık lisanslı örnek: *Archivo* / *Anton* / *Bebas Neue* ailesinden biri.
- **Gövde/veri:** temiz humanist sans — *Inter* / *IBM Plex Sans*.
- Sayılar tabular-figürlü (tablolar hizalı olsun). Hiyerarşi boyut + ağırlıkla kurulur, renk israfıyla değil.

### 13.4 İkonografi
- Tek çizgi ağırlığı, 24px baz grid, keskin-ama-yuvarlatılmış köşe dili. Attribute, trait, aksiyon ikonları aynı sette.
- Trait ikonları küçük "rozet" sistemine oturur (kişilik dilini görsel yapar).

### 13.5 Kulüp arması sistemi (parametrik — kritik)
Yüzlerce kurgusal kulüp için tutarlı arma üretimi:
```
Arma = KalkanŞekli (set: 5-6) × RenkÇifti (kulüp paleti) × MerkezMotif (set: ~20) × [opsiyonel Bant/Yıldız]
```
Hepsi SVG; token paletinden beslenir. Bu, "her kulüp elle çizilsin" tuzağını çözer ve stili kilitler.

### 13.6 Oyuncu temsili
- **MVP'de AI portresi YOK** (ölçekte tutarsız ve uncanny). Yerine: kulüp renklerinde **initial-token** veya küçük bir **stilize silüet/avatar** seti (birkaç şablon, basit parametrelerle).
- Portre istenirse: sonraya, kilitli bir üretim hattı + sabit stil anchor ile.

### 13.7 Asset envanteri
UI kit (bileşen + state'ler, 9-slice), ikon seti, arma sistemi, oyuncu token/avatar, tipografi ölçeği, maç-anlatı ekranı görsel dili, (sonraya) minimal 2D "key moment" saha görseli.

### 13.8 Teknik kısıtlar
- Hedef mobil yoğunluk: @2x/@3x; safe-area farkında.
- **SVG kaynak → sprite atlas** (veya mümkün yerde vektör). İkonlar atlaslanır; UI 9-slice.
- Her asset token paletine referans verir (hard-coded hex yasak) → tema tek yerden değişir.

### 13.9 Tutarlılık protokolü (AI art için — NON-NEGOTIABLE)
1. **Kilitli style anchor sheet:** repoda `docs/ART_STYLE.html` + örnek referans levhası (palet + örnek arma + örnek ikon + bir ekran).
2. Tüm art **SVG** olarak, aynı token'lara referansla üretilir.
3. Her üretim isteğinde style anchor'a atıf yapılır (palet + çizgi ağırlığı + köşe dili + "yapma" listesi).
4. Yeni asset, mevcut sete *yan yana* konup tutarlılık gözden geçirilmeden merge edilmez.

---

## 14. Yol Haritası Eşlemesi (inşa sırası)

GDD Bölüm 11'i implementasyona bağlar (assembly adları Bölüm 3 şemasında):

| Sıra | Katman/feature odağı | Çıktı |
|---|---|---|
| 0 | `Common` + `Domain` + `Application/Simulation` + `Tools/SeasonHarness` | Deterministik maç sim + binlerce sezon doğrulama |
| 1 | `Application/Simulation` tuning | "İnandırıcı mı" evet olana kadar denge |
| 2 | `Application/Season` + `Infrastructure/Persistence` | Sezon iskeleti, save/load (versiyonlu) |
| 3 | `Infrastructure/Configuration` + `Application/Generation` + `Application/Drama` | Trait sistemi + üreteç + çekirdek dram |
| 4 | `Application/Narrative` | Yolculuk günlüğü + satış anı + recap |
| 5 | `Domain` (ManagerCareer) + `Application/Season` | İtibar, perk, Efsaneler Salonu çekirdeği |
| 6 | `Presentation` + `Composition` + `Art` | 5 ekran + style bible'a göre görseller + wiring |
| 7 | — | MVP ship |

**Kural:** Bölüm 0–1 bitmeden UI/art'a geçilmez. Çekirdek inandırıcı değilse üstüne bir şey koymak boşa emek.

---

## 15. Claude Code ile Çalışma Notları

- **`CLAUDE.md`** repo kökünde; bu TDD'ye, `ART_STYLE`'a ve **engineering-standards**'a referans versin. Standartlar *nasıl inşa edilir*'in tek kaynağıdır.
- **İskeleyi `starter-tree.md`'den kur** (`Gaffer` ile): katman klasörleri + `.asmdef`'ler + `Result` (`Common`) + `dotnet test` köprüsü **önce**, feature kodundan evvel.
- **Küçük, test-kapsamlı adımlar:** her sim/dram değişikliği bir `dotnet` testiyle gelsin; "önce test/harness, sonra implementasyon".
- **Assembly sınırını güvenlik ağı gibi kullan:** ajan çekirdeğe `UnityEngine` sokmaya kalkarsa `dotnet` köprüsü/`.asmdef` derlemez.
- **Çekirdekte `throw` değil `Result`:** beklenen hata `Result`, yalnız bozulan invariant fail-fast (CONVENTIONS §4).
- **Denge veriyle:** tuning sabitleri `Infrastructure/Configuration` SO'larında; koda gömme.
- **Art istekleri style anchor'a atıfla:** palet token'ları, tek accent, 24px ikon grid, parametrik arma sistemi bağlamı verilsin.

---

*Bu doküman GDD'nin teknik karşılığıdır ve yaşayan bir sözleşmedir; engineering-standards'a devreder, oyuna-özel kararları taşır. Sim çekirdeği doğrulandıkça (Bölüm 11) güncellenir.*
