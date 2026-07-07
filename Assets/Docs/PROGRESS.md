# GAFFER — İlerleme ve Kararlar Günlüğü (PROGRESS)

> Bu doküman `ROADMAP.md`'nin yanında **"ne yaptık, neyi neden seçtik"** kaydını tutar.
> **ROADMAP = plan** (sıra + çıkış kriterleri); **PROGRESS = gerçekleşen + alınan kararlar.**
> Amaç: ilerlemeyi ve kararları kaybetmemek. Güncel durumu yansıtır (kronolojik değil).

---

## Güncel durum · 2026-07-07

- **Faz 0** ✅ · **Faz 1** ✅ (★ Gate A geçildi) · **Faz 2** 🟡 (çekirdek bitti, JSON adapter kaldı)
- **40 dotnet testi yeşil** — `dotnet test tests/Gaffer.Tests.csproj` (aynı testler Unity Test Runner'da da koşar)
- Bir lig sezonu **uçtan uca oynanıyor** (headless, UI'sız); **kaydet/yükle çekirdeği** var (deterministik, versiyonlu)

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
- **Domain** — `Attributes` (Players); `TeamStrength`, `ClubId`, `Club` (Clubs); `League` (Leagues)
- **Application/Simulation** — `MatchContext`/`MatchImportance`, `MatchCommand → MatchSimulator → MatchOutcome` (Poisson şans üretimi + kalite çözümü, portlar arkasında), `MatchSimulationSettings` (tuned)
- **Application/Season** — `FixtureScheduler` (çift devre, circle method), `LeagueTable`, `LeagueSeason` (hafta ilerlet + sonuç geçmişi + `Restore`), `BoardTarget` + `SeasonEvaluator` → `SeasonVerdict`
- **Application/Serialization** — `SeasonSaveData` DTO + `SeasonSaveMapper` + `SaveSchema`/`SaveMigrator` + `ISerializer` port *(JSON impl Infrastructure'da kaldı)*
- **Editor (`Gaffer.Editor`)** — `SeasonHarnessWindow` (ART_STYLE broadcast, dağılım/Gate A workbench)
- **Composition** — `MatchSmokeTest` (Play → Console) + sahne
- **Tests** — 40 test (`Gaffer.Tests`); `Gaffer.Tests.Unity` runtime determinizm kontrolü
- **Altyapı** — `tests/` dotnet köprüsü (net8.0 / LangVersion 9), `.editorconfig` (§1+§2), `.gitignore`

---

## Alınan kararlar (ve gerekçeleri)

1. **.NET 8 (`net8.0`) + `LangVersion 9.0`** — test köprüsü SDK 8'de koşar (`~/.dotnet`, sudo'suz kuruldu). LangVersion 9 = Unity 6 paritesi: köprü, Unity'nin reddedeceği bir şeyi (file-scoped namespace vb.) de reddeder. net9 gereksiz (STS); net8 LTS.
2. **Branch akışı = lokal `--no-ff` merge** — PR/review şimdilik yok (sonra eklenebilir); feature branch'leri **silinmiyor**, geçmişte grup olarak duruyor.
3. **Araçlar Unity Editor Window** — `SeasonHarness` konsol uygulaması yerine editor window'a taşındı (ART_STYLE). **`tests/` dotnet köprüsü KORUNDU** — o bir `.exe`/araç değil, çekirdeğin Unity'siz doğrulanmasını sağlayan test mekanizması.
4. **`TeamStrength` → Domain** — `Club` ve maç sim aynı değer nesnesini paylaşsın, duplication olmasın.
5. **`Club`/`League` Domain'de, sezon orkestrasyonu `Application/Season`'da** (dokümanın katman haritasına uygun).
6. **Kulüp/lig isimleri: kurgusal-üretilmiş (kombinatoryal), sahte-gerçek DEĞİL** — hukuki risk (tanınabilir sahteler bile marka riski), roguelike "kendi dünyan" kimliği, MVP kapsamı ("gerçek isim → sonraya"). İsimler **veri**, localization key değil. Üreteç Faz 3.
7. **Denge: `MeanChanceQuality` 0.20 → 0.17** → gol ~2.69 (hedef ~2.5–3). `BalanceSO` Faz 3'e ertelendi (şimdilik tuned `Default` + workbench slider'ları yeterli).
8. **Save = snapshot + `schemaVersion` + migration + RNG state** — replay-based değil (drama/transfer gelince her kararı replay-edilebilir tutmak kırılgan). Replay determinizmle zaten mümkün; save mekanizması değil.
9. **`.meta` dosyaları elle GUID'li üretildi** — iskelet Unity açmadan commitlenebilsin, stabil kalsın.
10. **Saf çekirdek disiplini** — `Common`/`Domain`/`Application` `noEngineReferences`; oklar hep içe (`Presentation → Application → Domain`).

---

## Kalan / sıradaki

- **Faz 2 kapanışı:** `Infrastructure/Persistence` JSON impl — Newtonsoft paketi (`com.unity.nuget.newtonsoft-json`) + `ISerializer` impl + dosya I/O (async). *Unity tarafı, kullanıcı doğrular.*
- **Bonus:** Editor harness'ı `Application/Season`'ı kullanacak şekilde sadeleştir (harness'taki lig mantığı duplication'ı bitsin).
- **Demo:** "Season Player" editor window — bir sezonu hafta hafta izle (erken oynanabilir demo; gerçek UI Faz 7).
- **Sonra Faz 3 (Yönetim):** `PlayerGenerator` + `ClubGenerator` + isim üreteci + taktik + transfer/scout + `BalanceSO`.

---

## Nasıl koşulur (hatırlatma)

- **Testler:** `PATH="$HOME/.dotnet:$PATH" dotnet test tests/Gaffer.Tests.csproj`
- **Harness:** Unity → menü **`Gaffer > Season Harness`** → Run
- **Maç smoke:** Unity → `Assets/_Project/Scenes/MatchSmokeTest.unity` → **Play** (Console'a maç akar)

---

*Her faz/dilim bittikçe bu doküman güncellenir. Kararlar değişirse eski karar üstü çizilmeden güncellenir, gerekçe not düşülür.*
