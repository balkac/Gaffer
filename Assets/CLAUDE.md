# CLAUDE.md — GAFFER

Roguelike futbol menajerliği · Mobil (iOS/Android) · Unity 6 (`6000.3.16f1`) · Universal 2D (URP 2D) · C#.
Bu dosya oturum başında okunur ve davranışı belirler. Ayrıntılar için:
`docs/GDD.md` (ne yapıyoruz), `docs/TDD.md` (oyuna-özel nasıl), `docs/ART_STYLE` (görsel dil).

**➜ Nerede kaldık? `docs/PROGRESS.md`'yi oku** (ilerleme + alınan kararlar + sıradaki adım). `docs/ROADMAP.md` faz durumunu (✅/🟡/⬜) gösterir. Yeni oturuma bu ikisiyle başla. **Testler:** `PATH="$HOME/.dotnet:$PATH" dotnet test tests/Gaffer.Tests.csproj`. **Bu köprünün kapsamını bil:** framework'süz olan her şey derlenir — `Common`/`Domain`/`Application`, ayrıca `UserData` (save serializer), `Tools/SeasonHarness` (Gate A enstrümanı) ve **tek tek adı verilmiş**, framework'süz olduğu için dahil edilen dosyalar: `Infrastructure/Localization/GameStrings.cs`, `Presentation/Squad/AbilityBand.cs` (wildcard **değil** — bu iki katmanın geri kalanı `UnityEngine`'e dokunur ve dışarıda kalmalı; adı verilen bir dosyaya `using UnityEngine` eklemek bu build'i bilerek patlatır. **Ölçüt:** dosya framework'süz VE içindeki kural `dotnet test`'te doğrulanmaya değer. **Ve işin YARISI budur:** köprüye bir dosya eklemek Unity'de derlenmesini sağlamaz — `Tests/EditMode/Gaffer.Tests.asmdef` da o dosyanın assembly'sine referans vermelidir, yoksa headless yeşil olur ve **editör kırmızı**. 2026-08-16'da tam olarak böyle oldu.)

**Tek kullanımlık typecheck csproj'u bunu YAKALAYAMAZ.** O, bütün katmanları *tek bir assembly'de* derler; assembly sınırları orada yoktur, dolayısıyla eksik bir asmdef referansı ona görünmez. Derleme doğrulaması için iyidir, **bağımlılık doğrulaması için değil** — asmdef'e dokunan her değişiklik **dördüncü kapıdan** geçmelidir (aşağıda). Bu köprünün kapsamı dışında kalan **`Infrastructure`'ın geri kalanı, `Composition`, `Presentation`, `Editor` hiçbir derleyici denetiminden geçmez**. Oralarda yeşil test, "derleniyor" demek değildir. (Gerektiğinde bu katmanları Unity'nin kendi assembly'lerine karşı derleyen tek kullanımlık bir csproj kurulabilir — 2026-08-06 review'unda öyle yapıldı; bkz. PROGRESS.)

**Dördüncü kapı — Unity CLI (headless editör).**
```
~/.unity/bin/unity test --mode EditMode --output /tmp/gaffer-editmode.xml --non-interactive --no-banner --timeout 1800
```
Projeyi gerçek Editor'de batch modda açar. Kritik olan şu: **Unity tek bir test koşmadan önce her assembly'yi derlemek zorundadır** — yani `Infrastructure`, `Composition`, `Presentation`, `Editor` ve bütün `.asmdef` referans grafiği bu kapıdan geçer. Yukarıdaki iki aracın yapısal olarak göremediğini gören tek otomatik kontrol budur; "Unity'yi açıp bakar mısın" artık bir doğrulama yöntemi değil.

**Ölçüldü (2026-08-30):** temiz ağaçta 584/584 yeşil (582 `Gaffer.Tests` + 2 `Gaffer.Tests.Unity`). `Presentation`'a bilerek konan bir derleme hatası koşuyu `exit 6` + `Aborting batchmode due to failure: Scripts have compiler errors.` ile düşürür — kapı gerçekten kapanıyor, varsayım değil. Sonuç NUnit XML'e yazılır; `--filter` daraltır, `--mode PlayMode` diğer platformu koşar.

**Ne zaman:** `.asmdef`'e veya `Infrastructure`/`Composition`/`Presentation`/`Editor`'e dokunan her değişiklikte, ve iş teslim edilmeden önce. **Ne zaman değil:** saf çekirdek düzenlemelerinde — bu kapı ~4 dk (editör açılışı + derleme), `dotnet test` saniyeler. Sıra: `dotnet test` → typecheck → `dotnet format` → `unity test`. **Şartlar:** Editor projeyi açık tutmamalı (`Library` kilidi çakışır); `unity` PATH'te değil, tam yolla çağrılır; çıktı repo dışına yazılır.

**Editor açıkken — `com.unity.pipeline` (hızlı iç döngü + gözle görme).** Açık Editor'e `localhost:7800`'den komut geçirir; `~/.unity/bin/unity status` bağlı örneği gösterir.
```
unity command recompile            # sonra recompile_status → completed
unity command get_console_logs
unity command capture_game_view --source screen --width 1080 --height 1920 --save_path Temp/shot.png
```
**Neden önemli:** `recompile`, dördüncü kapının derlediği aynı kör katmanları ~4 dk yerine **~9 saniyede** derler (ölçüldü 2026-08-30). Yani: `recompile` iç döngü kapısı, `unity test` teslim kapısıdır — biri diğerinin yerine geçmez. Ve `capture_game_view` ile yazdığın ekran **görülebilir**; "UI'ı okuyarak doğruladım" artık geçerli bir cümle değil.

**UI Toolkit ekranını yakalama sırası:** ekranlar runtime'da `UIDocument`'a kurulur, o yüzden `open_scene Assets/_Project/Scenes/Game.unity` → `editor_play` → `capture_game_view --source screen` (overlay panel yalnız Play Mode'da yakalanır; `--source camera` onu ıskalar) → `editor_stop`. **Play'de önce ana menü gelir** (2026-09-16): kabuğa girmek için "New run" ya da "Continue" düğmesine `NavigationSubmitEvent` gönderilir; `editor_stop` kirli run'ı `persistentDataPath/gaffer-run.json`'a otomatik kaydeder, yani bir sonraki Play "Continue" ile aynı run'ı açar — temiz bir run istiyorsan "New run". Kaydet ve Ana menü dördüncü sekme MENÜ'dedir (sekmeler `.tab` sınıflı `Button`). Düğme aramak için `UQueryExtensions.Query<Button>(root, null, "button")` — üçüncü argüman `null` olursa overload belirsizliği derleme hatası verir. **Bir dokunuşun arkasındaki ekran** (sheet, rapor) `eval` ile açılır: `var doc = UnityEngine.Object.FindFirstObjectByType<UnityEngine.UIElements.UIDocument>(); var el = UnityEngine.UIElements.UQueryExtensions.Q(doc.rootVisualElement, null, "<sınıf>"); var e = UnityEngine.UIElements.ClickEvent.GetPooled(); e.target = el; el.SendEvent(e);` — eval'da `using` yoktur, uzantı metodları tam adıyla çağrılır; ve `ClickEvent` bir `Button`'ı **tetiklemez**, düğme için `NavigationSubmitEvent.GetPooled()` gönderilir (2026-09-05, ölçüldü). `editor_play`'den sonra ~10 sn bekle; erken gelen komut "no reachable Pipeline servers" der. Sekme, segment ve pill'ler de `Button`'dır (`NavigationSubmitEvent`). **Dram kartındaki seçenek bloğu `Button` değildir** (`.choice`, satırlar gibi `ClickEvent`). Dram tetiklemek için bir seed aramak yerine `eval` ile "Haftayı Oyna → Devam" döngüsü kurulur; `--json` çıktısında sonuç `data.result.result` alanındadır (2026-09-16, bir döngü bunu yanlış okuyup 20 hafta kör oynadı). Sheet'i kapatmak için `.sheet__scrim` ararken **görünen** olanı seç (parent'ı `DisplayStyle.Flex`): ağaçta kadronun seçim yaprağı önce gelir ve `Q` ilk eşleşeni verir — bir çekim bu yüzden sheet'i kapatmadan alındı.

**Tuzaklar:** her yazma **authoring root'a (`Assets/`) hapsedilmiştir** — mutlak yol vermek de kurtarmaz, `Temp/shot.png` de `/…/Gaffer/Temp/shot.png` de `Assets/Temp/shot.png` olarak düşer ve yanında `.meta` üretir. Yani çekim repoyu kirletir: al, dışarı kopyala, `.png` ile `.meta`'yı birlikte sil (`unity test` sonrası `git status` ile doğrula). `unity test` ile aynı anda çalışmaz (`Library` kilidi). Unity değişikliği zaman damgasından değil içerik hash'inden görür — `touch` derleme tetiklemez. Ve **`recompile` yalnız script'leri kapsar**: `.uss`/`.uxml`/asset düzenlemesi ekrana yansımaz, `eval` ile `AssetDatabase.ImportAsset(<yol>, ImportAssetOptions.ForceUpdate)` gerekir (bir çekim boşa gitti, iki görüntü aynı çıktı).

**Unity'nin resmi Claude Code eklentisi (`unity@unity-agent-plugin`, local scope, 2026-09-16).** Yalnız skill taşır; araç kurmaz — `unity-cli` skill'i zaten kurulu olan CLI'ı anlatır, MCP/hook içermez. Bu dosya bağlayıcı kalır, eklenti değil. Bilinen sürtüşmeler: **(1)** `ui`/`ui-uitk` UXML + USS üretmeye yönlendirir; Gaffer'da ekran **C#'ta kurulur**, tek tema `UI/Theme/Gaffer.uss`, UXML yok — proje kalıbı kazanır. **(2)** Skill "UXML/USS Editor dışından doğrulanamaz, kullanıcıdan Editor'e odaklanmasını iste" der; yanlış, yukarıdaki `eval` + `ImportAsset` + `capture_game_view` sırası geçerlidir. **(3)** Skill `element.style.*`'ı yasaklar; Presentation'da dinamik konum/görünürlük için bilinçli kullanılır, statik stil USS'te kalır. **(4)** Skill `unity`'yi PATH'te varsayar; burada tam yolla çağrılır. Faydalı olduğu yer: USS kısıt tablosu (`gap`/`z-index`/`box-shadow`/`:nth-child` yok, `transition` base class'a), Faz 7'de `localization` (`com.unity.localization` geçişi), ileride IAP/LevelPlay/sprite-atlas.

**Nasıl inşa edilir → `docs/engineering-standards/`** (`ARCHITECTURE.md`, `CONVENTIONS.md`, `PERFORMANCE.md`, `UNITY.md`, `starter-tree.md`) **bağlayıcıdır.** Katmanlar, assembly'ler, isimlendirme, hata modeli, async sınırı, GC/alloc disiplini, engine yaşam döngüsü, test köprüsü oradan gelir. Çelişkide **standartlar kazanır**; TDD yalnız oyuna-özel kararları taşır.

---

## Bir cümlede
Bir alt lig kulübünde bir sezonun var; hedefi tuttur ya da kovul. Her sezon bir "run", her kovulma bir sonraki denemenin yakıtı. **Oyunun ürettiği şey duygu, sayı değil.** Formül: **Hikaye = Simülasyon + Karakter + Hafıza.**

## Tasarım aksiyomu
Simülasyon dramın rakibi değil, toprağıdır. Ali Yılmaz hikayesi *çünkü* sim onu inandırıcı büyüttüğü için işler. Sim'i zayıflatma; üstüne karakter ve hafıza koy.

---

## NON-NEGOTIABLE (asla ihlal etme)
1. **Saf çekirdek (`Domain` + `Application`) `UnityEngine`'e referans veremez.** `dotnet test` ile Unity'siz koşar. İhlal → derleme patlar (kasıtlı güvenlik ağı). (`.asmdef` `noEngineReferences`)
2. **Simülasyon deterministik.** Rastgelelik enjekte edilen `IRandom` üzerinden (somut: `SplitMix64RandomNumberGenerator`); global `Random` yasak. Aynı seed + input → aynı output.
3. **İçerik veri-güdümlü.** Yeni trait / dram olayı / taktik / denge = yeni ScriptableObject asset (`Infrastructure/Configuration`), **kod değil**.
4. **Command in → outcome out + tek yönlü bağımlılık.** Oklar hep içe: `Presentation → Application → Domain`. Çekirdek bir komut alır, değişmez outcome döner; UI outcome'u **replay** eder, çekirdeğe uzanıp state diff'lemez.
5. **Çekirdekte `throw` değil `Result`.** Beklenen hata `Result`/`Result<T>` (dependency-free `Common`); yalnız bozulan invariant fail-fast. (CONVENTIONS §4)
6. **Test önce sim.** UI'dan önce maç sim + headless doğrulama. Çekirdek "inandırıcı" olana kadar üstüne bir şey koyma.
7. **Trait'ler mekanik olarak gerçek.** Sim çıktısını ölçülebilir değiştirmiyorsa flavor text'tir — kabul etme.
8. **Ham kullanıcı-metni yok.** Tüm UI + anlatı metni localization key'leriyle string table'dan; kodda/veride düz metin yasak. Çekirdek **key** üretir (`attr.finishing.abbrev`, `role.centre_back.abbrev`), kelimeyi seçmek Presentation'ın işi. **Tek muafiyet: ship edilmeyen geliştirici araçları** — hangi assembly'de olursa olsun (`Gaffer.Editor`, `Gaffer.Tools.SeasonHarness`, …), ölçüt **`includePlatforms: ["Editor"]`**: build'e giremeyen kod oyuncuya metin gösteremez. Bunlar tanımı gereği İngilizce'dir ve karşılık arayacakları string table yoktur. Muafiyet **genişletilmez**: shipping assembly'lerin hiçbiri bu tiplere erişemez, dolayısıyla kural derleyiciyle zorlanır (örn. `Editor/Harness/HarnessLabels.cs` `internal`; `HtmlReportWriter` harness raporu).
9. **Enum'lar isimle persist edilir, ordinal'le değil.** Save'de isim (`PersistedPlayerRole`), `.asset`'e serialize olan enum'larda ise Unity int yazdığı için değerler explicit **pinlenir** ve asla yeniden sıralanmaz/kullanılmaz — testle kilitli (`PersistedEnumValueTests`).

---

## Katman haritası (bkz. TDD §3 + `starter-tree.md`) — katman başına tek assembly
```
Common  Domain  Application(Simulation/Generation/Drama/Narrative/Season/Run)   → saf C#, UnityEngine YOK
Infrastructure(Configuration SO + Persistence + Localization)  Presentation(UI Toolkit)  Composition  → UnityEngine VAR
Tests (dotnet + Unity)   Tools/SeasonHarness (1000-sezon dotnet konsol)   UserData (save serializer)
```
**Sezon harness'ı (Gate A).** Enstrümanın kendisi `Tools/SeasonHarness`'ta (saf C#, `includePlatforms: Editor`); üç yüzü var ve **hepsi aynı kodu** çalıştırır: Unity'deki `SeasonHarnessWindow`, `dotnet test`'teki erişilebilirlik testi, ve konsol kabuğu `tools/SeasonHarnessCli` (Assets dışında — Unity assembly'sine `Main` koymak test köprüsünün entry point'iyle çakışırdı). Konsol: `dotnet run --project tools/SeasonHarnessCli -- --seasons 1000`; dağılımı basar, gate düşerse exit 1.
**Localization.** Çekirdek **key** üretir (#8), kelimeler `Infrastructure/Localization`'da (`GameStrings` + `StringTableSO`). `com.unity.localization` paketi **bilinçli olarak Faz 7'ye ertelendi** — o güne dek string table bu katmanın kendi tipidir.
**`Application/Run` = run akışının tek sahibi.** `RunSession` lig + sezon + finans + dram + moral + sezon-no'yu tutar; `RunSessionFactory.Start/Resume` tek kurulum kapısıdır (ctor `internal`). Komutlar outcome döner (`WeekOutcome`, `LineupOutcome`, `DramaResolution`, `TransferOutcome`, `SeasonRollover`) — UI bunları **replay eder**, çekirdeğe uzanıp state diff'lemez (#4). Editör pencereleri ve gelecekteki Presentation, akışın kopyası değil, bunun üstünde ince view'dur.
`Common/Domain/Application` `.asmdef`'lerinde `noEngineReferences`. `TraitSO`/`DramaEventSO` `Infrastructure`'da authoring yüzeyi; yüklemede saf `Domain` tipine map'lenir. `enum` yerine `Id` (tanım veride).

## İnşa sırası (bkz. GDD §11 / TDD §14)
0) Common + Domain + Application/Simulation + Tools/SeasonHarness → deterministik sim + **binlerce sezon doğrulama**
1) Sim tuning ("inandırıcı mı" evet olana dek) 2) Application/Season + Infrastructure/Persistence (sezon + versiyonlu save/load)
3) Infrastructure/Configuration + Application/{Generation,Drama} (trait + üreteç + çekirdek dram) 4) Application/Narrative (günlük + satış anı + recap)
5) Meta (itibar, perk, Efsaneler Salonu) 6) Presentation + Composition + Art 7) MVP ship

## Doğrulama hedefleri (§11)
Headless sezon sim: şampiyon dağılımı makul · gol ~2.5–3/maç · favori genelde kazanır ama upset inandırıcı · puan tablosu gerçekçi. Soru "eğlenceli mi" değil, **"inandırıcı mı"**.

---

## Çalışma ritmi
- **İskeleyi `starter-tree.md`'den kur** (`Gaffer` ile): katman klasörleri + `.asmdef`'ler + `Result` (`Common`) + `dotnet test` köprüsü **önce**, feature kodundan evvel.
- Küçük, test-kapsamlı adımlar. Her sim/dram değişikliği bir `dotnet` testiyle (`Scenario_Condition_Result`); RNG stub'la deterministik assertion. Editörü açmadan headless doğrula.
- Çekirdekte `throw` değil `Result`. Denge sabitlerini koda gömme → `Infrastructure/Configuration` SO'larında.
- Yeni trait/olay: önce asset şeması, sonra sim/dram hook'u, sonra "bu gerçekten çıktıyı değiştiriyor mu" testi.

## Art kuralları (bkz. docs/ART_STYLE)
- Yön: **"Matchday Broadcast Graphics"** — arayüz, izlemediğin maçın yayın grafikleri.
- Tüm görsel **SVG + token** (`--pitch #0C1B1A`, `--chalk #EAF2EE`, `--accent #FF2E7E`). Hard-coded hex yok.
- **Tek accent (magenta)**; ikinci parlak renk yok (semantik win/loss/draw hariç).
- İkonlar 24px grid, tek çizgi ağırlığı. Armalar parametrik sistemden. **MVP'de AI oyuncu portresi yok.**
- Cream+serif+terracotta klişesine kayma; bu proje teal/broadcast.
- Her art isteğinde `docs/ART_STYLE` levhasına atıf yap.

## Dil / yerelleştirme (bkz. TDD §12.5)
- Kod ve teknik yorumlar İngilizce.
- **Kaynak/referans locale = İngilizce (`en`).** Lansmanda `en` + `tr` ship edilir; Türkçe **native** yazılır (makine çevirisi değil).
- Tüm metin localization key üzerinden; `TraitSO`/`DramaEventSO` metin alanları key tutar, literal değil.
- Anlatı şablonlarını localization-dostu kur: interpole varlıklar atomik, cümle-ortası çekim (ünlü uyumu) bağımlılığından kaçın. Türkçe ek-motoru sonraya, opsiyonel.
- UI kopyası: sade fiil, cümle düzeni, klişe yok — özne oyuncunun kontrol ettiği şey.
