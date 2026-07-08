# GAFFER — İlerleme ve Kararlar Günlüğü (PROGRESS)

> Bu doküman `ROADMAP.md`'nin yanında **"ne yaptık, neyi neden seçtik"** kaydını tutar.
> **ROADMAP = plan** (sıra + çıkış kriterleri); **PROGRESS = gerçekleşen + alınan kararlar.**
> Amaç: ilerlemeyi ve kararları kaybetmemek. Güncel durumu yansıtır (kronolojik değil).

---

## Güncel durum · 2026-07-08

- **Faz 0** ✅ · **Faz 1** ✅ (★ Gate A geçildi) · **Faz 2** 🟡 (çekirdek bitti, JSON adapter kaldı) · **Faz 3** 🟡 (üreteç + kadro→güç köprüsü hazır)
- **53 dotnet testi yeşil** — `dotnet test tests/Gaffer.Tests.csproj` (aynı testler Unity Test Runner'da da koşar)
- Bir lig sezonu **uçtan uca oynanıyor** (headless + **Season Player** editor demosu); **kaydet/yükle çekirdeği** + **deterministik oyuncu üreteci** (garanti wonderkid dâhil) + **kadro→`TeamStrength` köprüsü** (`BuildEffectiveStrength`) hazır

---

## Gate durumu

| Kapı | Faz | Durum | Ölçüm |
|---|---|---|---|
| ★ Gate A | 1 sonu | ✅ **GEÇILDI** | gol 2.69/maç · favori %51.6 kazanır · ev %38.2 > deplasman %35.8 · şampiyon dağılımı sağlıklı (regresyon testleriyle kilitli) |
| ★ Gate B | 5 sonu | ⬜ | Hikaye kendiliğinden beliriyor mu? |
| ★ Gate C | 8 | ⬜ | MVP Definition of Done |

---

## Tamamlananlar (katman bazında)

- **Common** — `Result`/`Result<T>`, `IRandom` + `SplitMix64RandomNumberGenerator` (golden-locked, `State` getter save için)
- **Domain** — `Attributes`, `Player`, `PlayerId`, `Position` (Players); `TeamStrength`, `ClubId`, `Club`, `Squad` (Clubs); `League` (Leagues)
- **Application/Simulation** — `MatchContext`/`MatchImportance`, `MatchCommand → MatchSimulator → MatchOutcome` (Poisson şans üretimi + kalite çözümü, portlar arkasında), `MatchSimulationSettings` (tuned), `EffectiveStrengthBuilder` (`Squad → TeamStrength`: hat-bazlı rol-rating ortalaması; boş hat → kadro geneline düşer)
- **Application/Season** — `FixtureScheduler` (çift devre, circle method), `LeagueTable`, `LeagueSeason` (hafta ilerlet + sonuç geçmişi + `Restore`), `MatchResult` (skor + dakika-golleri), `BoardTarget` + `SeasonEvaluator` → `SeasonVerdict`
- **Application/Serialization** — `SeasonSaveData` DTO + `SeasonSaveMapper` + `SaveSchema`/`SaveMigrator` + `ISerializer` port *(JSON impl Infrastructure'da kaldı)*
- **Application/Generation** — `PlayerGenerator` (deterministik oyuncu) + `PlayerNameGenerator` (kurgusal isim) + `PlayerPoolGenerator` (garanti wonderkid) + `GenerationContext`
- **Editor (`Gaffer.Editor`)** — `SeasonHarnessWindow` (dağılım/Gate A workbench) + `SeasonPlayerWindow` (bir sezonu izle: tablo + son hafta sonuçları/gol dakikaları + verdict)
- **Composition** — `MatchSmokeTest` (Play → Console) + sahne
- **Tests** — 53 test (`Gaffer.Tests`); `Gaffer.Tests.Unity` runtime determinizm kontrolü
- **Altyapı** — `tests/` dotnet köprüsü (net8.0 / LangVersion 9), `.editorconfig` (§1+§2), `.gitignore`

---

## Alınan kararlar (ve gerekçeleri)

1. **.NET 8 (`net8.0`) + `LangVersion 9.0`** — test köprüsü SDK 8'de koşar (`~/.dotnet`, sudo'suz kuruldu). LangVersion 9 = Unity 6 paritesi: köprü, Unity'nin reddedeceği bir şeyi (file-scoped namespace vb.) de reddeder. net9 gereksiz (STS); net8 LTS.
2. **Branch akışı = lokal `--no-ff` merge; branch'ler BÜYÜK feature/faz için** — her küçük dilim için ayrı branch açma (çok branch birikmişti); dilimler tek feature branch'inde commit olarak birikir, feature bütünlenince merge. **Doküman/küçük değişiklik doğrudan `main`'e.** PR/review şimdilik yok. Merge edilmiş branch'ler silinmez (boş/kullanılmayan hariç).
3. **Araçlar Unity Editor Window** — `SeasonHarness` konsol uygulaması yerine editor window'a taşındı (ART_STYLE). **`tests/` dotnet köprüsü KORUNDU** — o bir `.exe`/araç değil, çekirdeğin Unity'siz doğrulanmasını sağlayan test mekanizması.
4. **`TeamStrength` → Domain** — `Club` ve maç sim aynı değer nesnesini paylaşsın, duplication olmasın.
5. **`Club`/`League` Domain'de, sezon orkestrasyonu `Application/Season`'da** (dokümanın katman haritasına uygun).
6. **Kulüp/lig isimleri: kurgusal-üretilmiş (kombinatoryal), sahte-gerçek DEĞİL** — hukuki risk (tanınabilir sahteler bile marka riski), roguelike "kendi dünyan" kimliği, MVP kapsamı ("gerçek isim → sonraya"). İsimler **veri**, localization key değil. Üreteç Faz 3.
7. **Denge: `MeanChanceQuality` 0.20 → 0.17** → gol ~2.69 (hedef ~2.5–3). `BalanceSO` Faz 3'e ertelendi (şimdilik tuned `Default` + workbench slider'ları yeterli).
8. **Save = snapshot + `schemaVersion` + migration + RNG state** — replay-based değil (drama/transfer gelince her kararı replay-edilebilir tutmak kırılgan). Replay determinizmle zaten mümkün; save mekanizması değil.
9. **`.meta` dosyaları elle GUID'li üretildi** — iskelet Unity açmadan commitlenebilsin, stabil kalsın.
10. **Saf çekirdek disiplini** — `Common`/`Domain`/`Application` `noEngineReferences`; oklar hep içe (`Presentation → Application → Domain`).
11. **Editor araçları ≠ ship edilen UI.** Editor window'lar (`Gaffer.Editor`) geliştiriciye hizmet eder; feature geldikçe evrilir, gereksizleşince silinir (ör. `MatchSmokeTest` sonra atılır, `SeasonHarness` kalıcı dev aracı). **Ship edilen oyuncu-UI'si ayrı bir Faz 7 işi** (`Presentation` + `Composition`, runtime, UI Toolkit) — editor window oyuna giremez.
12. **Oyuncu isimleri = jenerik insan isimleri (kombinatoryal, gerçekçi).** Kulüp isimleri kurgusal (gerçek-kulüp değil) ama oyuncu isimleri sıradan ad+soyad → marka değil, belirli gerçek oyuncuyu kopyalamadıkça sorun yok. Milliyet-flavor'lı havuzlar sonraya.
13. **Garanti wonderkid = havuza gem-context tohumlama.** İlk N oyuncu düşük-görünür/yüksek-gizli/genç context'ten üretilip karıştırılır; keşif **nadir** kalır (doğrulama testiyle sınırlı).
14. **CM 01/02 dersleri dokümanlara işlendi** — keşif garantisi (NON-NEGOTIABLE), düşük-sürtünme transfer + gergin ekonomi, blackbox quirk'leri, "asla eklenmeyecekler" listesi, paylaşılabilir legend card (post-MVP).

---

## Kalan / sıradaki

- **Faz 3 (Yönetim, devam) — sıradaki:** ✅ `Squad` (kadro) + `BuildEffectiveStrength` (kadrodan `TeamStrength` türet) hazır. **Sıradaki:** `Club`'a `Squad` bağla + `LeagueSeason` maç gücünü kadrodan türetsin (köprüyü sim'e kablola) → sim'de **golcü seçimi** (event oyuncuya bağlanır → **isimli golcüler**) → taktik (dizilim + tempo/pres/risk) → transfer/scout (düşük-sürtünme + gergin ekonomi) → `ClubGenerator`/isim üreteci → `BalanceSO`.
- **Faz 2 kapanışı:** `Infrastructure/Persistence` JSON impl — Newtonsoft paketi + `ISerializer` impl + dosya I/O (async). *Unity tarafı.*
- **Bonus:** Editor harness'ı `Application/Season`'ı kullanacak şekilde sadeleştir (duplication).

---

## Nasıl koşulur (hatırlatma)

- **Testler:** `PATH="$HOME/.dotnet:$PATH" dotnet test tests/Gaffer.Tests.csproj`
- **Harness:** Unity → menü **`Gaffer > Season Harness`** → Run
- **Season Player (demo):** Unity → menü **`Gaffer > Season Player`** → Start Season → Advance Week
- **Maç smoke:** Unity → `Assets/_Project/Scenes/MatchSmokeTest.unity` → **Play** (Console'a maç akar)

---

*Her faz/dilim bittikçe bu doküman güncellenir. Kararlar değişirse eski karar üstü çizilmeden güncellenir, gerekçe not düşülür.*
