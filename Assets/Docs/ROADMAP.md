# GAFFER — Yürütme Yol Haritası (Roadmap)

> Bu doküman **sıra** ve **çıkış kriteri** taşır — takvim değil. Solo + Claude Code ile en büyük risk scope kaymasıdır; panzehir, her fazı "ne zaman bittiğini" net söyleyen bir çıkış kriterine bağlamaktır. Bağlam: `docs/GDD.md`, `docs/TDD.md`, `docs/ART_STYLE`, `docs/engineering-standards/` (mimari/konvansiyon/test kaynağı).
>
> **Efor** göreli (S / M / L), takvim değil. Solo bir projede tarih vermek yanıltır; fazı çıkış kriteriyle kapat, tarihle değil.

---

## 📍 Güncel durum (2026-07-09)

**Faz 0 ✅ · Faz 1 ✅ (★ Gate A geçildi) · Faz 2 🟡 · Faz 3 🟡** — bir lig sezonu uçtan uca oynanıyor (headless + Season Player demo), kaydet/yükle + deterministik oyuncu üreteci (garanti wonderkid) + kadro→güç köprüsü + gruplu FM-benzeri attribute modeli + **rol-özel rating** + **oyuncu gelişimi** (keşfet-büyüt-sat flip'i çekirdekte kapandı) hazır, **119 dotnet testi yeşil**. Ayrıntı + alınan kararlar: **[`PROGRESS.md`](PROGRESS.md)**.

---

## Rehber ilkeler

1. **İnandırıcılık kapısı önce gelir.** Faz 0–1 geçmeden hiçbir şeyin üstüne bir şey konmaz. Çekirdek inandırıcı değilse UI/art/dram boşa emektir.
2. **Dikey dilim düşün.** Her faz, oyunun uçtan uca *çalışan* bir katmanını ekler; yatay olarak her sistemi yarım bırakma.
3. **Küçük ship > büyük yarım.** MVP dar ama kapalı olmalı; vizyon sonraya.
4. **Enine kesen işler faz değildir.** i18n, test disiplini ve art tutarlılığı ilk günden akar (aşağıda ayrı izlek).

---

## Genel görünüm

| Faz | Ad | Durum | Efor | Kapı |
|---|---|---|---|---|
| 0 | Kurulum + İnandırıcılık Çekirdeği | ✅ Tamam | **L** | ★ Gate A |
| 1 | Tuning + Test Ağı | ✅ Tamam (Gate A geçildi) | M | ★ Gate A |
| 2 | Sezon İskeleti | 🟡 Çekirdek bitti · JSON adapter kaldı | M | |
| 3 | Yönetim Sistemleri | 🟡 Üreteç + wonderkid + kadro→güç + attribute + rol-özel rating + gelişim ✅ · devam | **L** | |
| 4 | Karakter + Dram | ⬜ | **L** | |
| 5 | Hafıza + Anlatı | ⬜ | M | ★ Gate B |
| 6 | Meta / Roguelike | ⬜ | M | |
| 7 | UI + Art + Localization | ⬜ | **L** | |
| 8 | MVP Ship | ⬜ | M | ★ Gate C |

> Katman/feature detayları her fazın kendi bölümünde. Güncel gerçekleşen + kararlar: [`PROGRESS.md`](PROGRESS.md).

---

## Faz 0 — Kurulum + İnandırıcılık Çekirdeği · **L** · ★
İskeleti kur, sonra oyunun kalbini kanıtla.

**Teslimatlar:**
- Repo + Unity `6000.3.16f1` (Universal 2D) + **`starter-tree.md`'den** `.asmdef` iskeleti (`Common/Domain/Application` → UnityEngine YOK) + `Result` (`Common`) + `dotnet test` köprüsü. `CLAUDE.md`, `docs/` ve engineering-standards yerinde.
- `Domain`: minimal `Player` (şimdilik sadece attribute), `Club`, `League`.
- `Application/Simulation`: `SimulateMatch(...)` (command→outcome) + `IRandom` (`SplitMix64...`) + `MatchContext`.
- `Tools/SeasonHarness`: bir sezonu, sonra **1000 sezonu** Unity'siz (dotnet konsol) simüle eden runner + dağılım çıktısı.
- *Not:* Faz 0'da oyuncular kaba stub (yalnız attribute); isim/trait/kişilik sonra gelir.

**Çıkış kriteri (Gate A'nın yarısı):** 1000 sezon headless dönüyor ve dağılımlar okunabilir raporlanıyor.

---

## Faz 1 — Tuning + Test Ağı · **M** · ★
"İnandırıcı mı?" sorusunu evet yap.

**Teslimatlar:**
- Denge sabitleri `Infrastructure/Configuration` SO'larında; tuning döngüsü.
- `Tests` (dotnet köprüsü): RNG stub'la deterministik sim testleri, `Scenario_Condition_Result`.

**Çıkış kriteri — ★ GATE A (kritik):**
- Şampiyon dağılımı makul (favoriler öne çıkıyor, tek takım domine etmiyor).
- Gol dağılımı ~2.5–3/maç, aşırı uçlar seyrek.
- Favori genelde kazanıyor ama upset oranı inandırıcı.
- Sezon sonu tablo gerçek bir ligi andırıyor.
- Regresyon testleri yeşil.

> **Bu kapı geçilmeden Faz 2+ başlamaz.** Geçmiyorsa: sim modelini gözden geçir; en kötü senaryoda konsepti yeniden düşün. Bu, projenin en ucuz "yanlışsa şimdi öğren" noktası.

---

## Faz 2 — Sezon İskeleti · **M**
Bir run uçtan uca çalışsın (UI olmadan bile).

**Teslimatlar:**
- Lig akışı: fikstür, hafta ilerletme, puan durumu.
- Yönetim hedefi + sezon sonu değerlendirme (yüksel / kal / kovul).
- **Save/Load** — JSON + `schemaVersion` (versiyonlu, baştan).

**Çıkış kriteri:** Bir tam sezon = bir run, koddan baştan sona oynanabiliyor; kaydet/yükle çalışıyor.

---

## Faz 3 — Yönetim Sistemleri · **L**
Karar döngüsünü tamamla.

**Teslimatlar:**
- `PlayerGenerator` (deterministik): **isim + milliyet** + attribute + gizli potansiyel dağılımı + trait/kişilik ağırlıklı atama. Attribute'lar **gruplu 0–100 set** (Teknik / Set-piece / Fiziksel & Hareket / Kalecilik) ve **pozisyona uygun** üretilir (GDD §4.2, ART_STYLE §4.1).
- **Keşfedilebilir wonderkid garantisi** (CM 01/02 dersi): her run'da alt liglerde, ucuz, düşük-görünür ama yüksek-gizli-potansiyelli, keşfedilmeyi bekleyen az sayıda cevher garantili üretilir (TDD §5).
- Taktik (dizilim + tempo/pres/risk eksenleri), kadro seçimi.
- Transfer + **scout belirsizliği** (potansiyel maskeli), basit antrenman. **Düşük-sürtünme model** (basit teklif/karşı-teklif, ajan bürokrasisi yok; "keşfet-büyüt-sat" flip'i çekirdek ödül) ama **run ekonomisi gergin** (satmak bedel taşır — para basma makinesi değil; GDD §4.4).
- Rakip menajer AI'ı: kural tabanlı transfer/taktik.

**Çıkış kriteri:** Kadro, taktik, transfer, antrenman kararları anlamlı ve sonuçlu; oyuncular üretiliyor (elle karakter yok); **keşif fantezisi gerçek** — ara sıra düşük-görünür genç patlıyor, garanti cevher cevhere dönüşüyor (TDD §11 keşif doğrulaması).

---

## Faz 4 — Karakter + Dram · **L**
Oyunun ruhu. (GDD 4.1, 4.2, 4.7)

**Teslimatlar:**
- Trait sistemi (veri-güdümlü): ~6–8 mekanik trait, `MatchContext`-duyarlı (derbi canavarı vb.).
- Dram motoru: ~8–12 sonuçlu/kararlı olay (koşul + ağırlık + cooldown + Choice→Effect) + olay bütçesi.
- Küçük set-piece olay(lar): kulüp satışı, başkan gibi el-yapımı çıpalar.
- Minimal menajer–oyuncu bağı beat'i.

**Çıkış kriteri:** Bir trait sim çıktısını ölçülebilir değiştiriyor (flavor değil); dram nadir, kararlı, state değiştiren olaylar olarak tetikleniyor.

---

## Faz 5 — Hafıza + Anlatı · **M** · ★
Simülasyonu hikayeye çevir. (GDD 4.8)

**Teslimatlar:**
- Oyuncu **yolculuk günlüğü** (debüt, kilit anlar, bağ, transfer).
- Maç anlatısı = **karakter anı** (isimli, bağlamlı beat'ler).
- **Satış anı** duygusal işaretlemesi + sezon sonu recap.

**Çıkış kriteri — ★ GATE B:** Test oynanışında kendiliğinden bir "Ali Yılmaz yayı" beliriyor ve *yazılmış gibi* hissettiriyor. Bu, oyunun temel bahsi — hissetmiyorsa dram/hafıza ayarını derinleştir.

---

## Faz 6 — Meta / Roguelike · **M**
Run'ları birbirine bağla. (GDD 4.9, 4.10)

**Teslimatlar:**
- Menajer: **isim + milliyet** (minimal; portre yok).
- İtibar + birkaç perk + bir başlangıç arketip seçimi.
- Efsaneler Salonu çekirdeği (run'lar arası kalıcı).

**Çıkış kriteri:** Kovulmak run'ı bitiriyor ama kariyeri değil; bir sonraki run daha yüksek tavanla başlıyor.

---

## Faz 7 — UI + Art + Localization · **L**
Oynanabilir ve gösterilebilir hale getir.

**Teslimatlar:**
- 5 çekirdek ekran (UI Toolkit): kadro/taktik, maç anlatısı, sezon/lig, transfer/scout, menajer/meta.
- Art: `docs/ART_STYLE` broadcast sistemi — token'lı SVG, parametrik armalar, ikonlar, imza maç ekranı.
- **Localization bağlama:** String Table'lar, key'ler; **EN + TR** (Türkçe native).

**Çıkış kriteri:** Tam bir run UI üzerinden oynanabiliyor, iki dilde, art tutarlı.

---

## Faz 8 — MVP Ship · **M** · ★
Dar ama kapalı.

**Teslimatlar:** cila, store hazırlığı, temel analytics (opsiyonel), **monetizasyon yok**.

**Çıkış kriteri — ★ GATE C (MVP Definition of Done):**
- Tek lig + kupa, üretilen kadrolar.
- İstatistiksel sim + karakter-anı anlatısı.
- Taktik/transfer/scout/antrenman karar döngüsü.
- Trait + dram + hafıza (küçük ama gerçek).
- Bir tam sezon = run; yükselme/kovulma; itibar + birkaç perk + Efsaneler Salonu.
- Save/load, 5 ekran, EN+TR.
- Çökme yok; bir run baştan sona akıcı.

---

## Enine kesen izlekler (tüm fazlar boyunca)

- **i18n:** İlk günden key'ler, ham string yasak. Faz 7'de dil eklemek "çeviri" olsun, refactor değil.
- **Test disiplini:** Her sim/dram değişikliği bir `dotnet` testiyle gelir; deterministik assertion; editörü açmadan headless doğrula.
- **Art tutarlılığı:** Görsel iş başladığında her asset `docs/ART_STYLE` levhasına atıfla, token'lı SVG olarak.
- **Denge veriyle:** Sabitler `Infrastructure/Configuration` SO'larında; koda gömme.

---

## Karar kapıları (go / no-go)

- **★ Gate A (Faz 1 sonu):** ✅ **GEÇILDI (2026-07-07)** — gol 2.69/maç, favori %51.6 kazanır, ev > deplasman, şampiyon dağılımı sağlıklı; regresyon testleriyle kilitli. *En ucuz iptal/pivot noktası olmaktan çıktı.*
- **★ Gate B (Faz 5 sonu):** Hikaye kendiliğinden beliriyor mu? Bu emergent dram bahsinin tutup tutmadığı an.
- **★ Gate C (Faz 8):** MVP Definition of Done karşılandı mı? Ship kararı.

---

## MVP sonrası ufuk (bilinçli ertelenenler)

2D "key moments" görselleştirme · **paylaşılabilir "legend card"** (Efsaneler Salonu kartı — MVP'de yalnız çekirdek liste var) · idle/idle-hybrid katman · derin ilişki ağı · geniş dram içeriği (yüzlerce olay, set-piece zincirleri) · derin arketip ağacı · çok ligli dünya · community data / gerçek isim · **monetizasyon** (kozmetik, ödüllü reklam, premium unlock) · gelişmiş rakip AI · Türkçe ünlü-uyumu ek-motoru · daha zengin/RPG-vari menajer (görünüş, backstory).

---

*Yol haritası yaşayan bir dokümandır; Gate A geçildikçe alt fazların çıkış kriterleri sayısal olarak sıkılaştırılır.*
