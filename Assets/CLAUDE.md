# CLAUDE.md — GAFFER

Roguelike futbol menajerliği · Mobil (iOS/Android) · Unity 6 (`6000.3.16f1`) · Universal 2D (URP 2D) · C#.
Bu dosya oturum başında okunur ve davranışı belirler. Ayrıntılar için:
`docs/GDD.md` (ne yapıyoruz), `docs/TDD.md` (oyuna-özel nasıl), `docs/ART_STYLE` (görsel dil).

**Nasıl inşa edilir → `docs/engineering-standards/`** (`ARCHITECTURE.md`, `CONVENTIONS.md`, `PERFORMANCE.md`, `starter-tree.md`) **bağlayıcıdır.** Katmanlar, assembly'ler, isimlendirme, hata modeli, async sınırı, test köprüsü oradan gelir. Çelişkide **standartlar kazanır**; TDD yalnız oyuna-özel kararları taşır.

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
8. **Ham kullanıcı-metni yok.** Tüm UI + anlatı metni localization key'leriyle string table'dan; kodda/veride düz metin yasak.

---

## Katman haritası (bkz. TDD §3 + `starter-tree.md`) — katman başına tek assembly
```
Common  Domain  Application(Simulation/Generation/Drama/Narrative/Season)   → saf C#, UnityEngine YOK
Infrastructure(Configuration SO + Persistence + Localization)  Presentation(UI Toolkit)  Composition  → UnityEngine VAR
Tests (dotnet + Unity)   Tools/SeasonHarness (1000-sezon dotnet konsol)
```
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
