# GAFFER — Art Style Bible

> Bu, görsel dilin **metin** karşılığıdır. Render edilmiş, bağlayıcı referans levhası `ART_STYLE.html`'dir (palet + örnek arma + ikon seti + imza ekran mockup'ı). **Çelişki olursa `ART_STYLE.html` kazanır.**
>
> Tüm görsel işleri Claude üretir. AI ile art'ta en zor şey **tutarlılık**; bu yüzden yön illüstrasyona değil, **token'lı vektör (SVG) sistemine** dayanır — bu aynı zamanda Claude'un güvenilir ürettiği şeyle (temiz SVG) örtüşür.

---

## 1. Yön ve gerekçe

**Yön: "Matchday Broadcast Graphics".** Arayüz, yönettiğin ama asla izlemediğin bir maçın *yayın grafikleri paketidir*: score bug, dizilim lower-third'ü, taktik tahtası. "Görsel maç motoru yok" kısıtını bir zayıflık olmaktan çıkarıp kimliğe çevirir. Gece-sahası teal zemini + yayın magentası accent + condensed sportif tipografi.

**Neden bu yön (solo + AI-üretim için):**
- Tipografi + vektör + token, illüstre karakterlerden **çok daha tutarlı** üretilir.
- Kulüp armaları/ikonlar **parametrik SVG** ile yüzlerce kez tutarlı çıkar.
- "Koyu zemin + tek acid-accent" ve "cream + editorial" kombinasyonları şu an AI-default (templated) okunur; broadcast yönü bunlardan ayrışır ve konuya bağlıdır.

---

## 2. Renk sistemi (token — bağlayıcı)

| Token | Hex | Rol |
|---|---|---|
| `--pitch` | `#0C1B1A` | taban zemin (gece-sahası teal-siyah) |
| `--pitch-raised` | `#122624` | kart / yüzey |
| `--pitch-line` | `#1E3733` | çizgi / kenarlık |
| `--chalk` | `#EAF2EE` | birincil metin |
| `--muted` | `#7C938C` | ikincil metin |
| `--accent` | `#FF2E7E` | **İMZA — tek accent (yayın magentası)** |
| `--win` | `#2FD48A` | semantik (yalnız sonuç bağlamı) |
| `--loss` | `#FF5A4D` | semantik |
| `--draw` | `#E7B84B` | semantik |

Alternatif accent (takas edilirse): elektrik-lime `#E8FF3A`.
**Kural:** tek accent + zengin nötr taban. İkinci parlak renk *eklenmez* (semantikler hariç).

---

## 3. Tipografi

- **Display/başlık:** güçlü condensed grotesque (editoryal-sportif his) — açık lisanslı: *Archivo* / *Anton* / *Bebas Neue*. Büyük harf + sıkı tracking (broadcast lower-third hissi).
- **Gövde/veri:** temiz humanist sans — *Inter* / *IBM Plex Sans*.
- Sayılar **tabular** (tablolar hizalı). Hiyerarşi boyut + ağırlıkla kurulur, renk israfıyla değil.

---

## 4. İkonografi

- Tek çizgi ağırlığı (~1.6), **24px baz grid**, keskin-ama-yuvarlatılmış köşe dili. Attribute, trait, aksiyon ikonları aynı sette.
- Trait ikonları küçük **rozet** sistemine oturur (kişilik dilini görsel yapar).

### 4.1 Attribute gösterimi (data-viz kuralı — tek accent'e sadık)

Oyuncu attribute'ları (0–100) üç *ayrı* görsel kanalla okunur; karışmazlar:

1. **Değer büyüklüğü = parlaklık rampası + accent (FM'in yeşilinin işlevi, tek-accent paletiyle).** `≥85 → --accent (yanar)`, `70–84 → --chalk (bold)`, `55–69 → #9DAAA4 (soluk)`, `40–54 → --muted`, `<40 → #46564f`. Göz doğrudan iyilere gider. **İkinci parlak renk (FM yeşili/kırmızısı) kullanılmaz.**
2. **Pozisyon önemi = tint, renk değil.** Rol-anahtarı attribute'ların satırı hafif `--accent` yıkaması (~%9 opacity) + sol accent kenar çizgisi alır → "bu pozisyon için önemli". *Önemi* işaretler, *değeri* değil. Rol→key eşlemesi veriden gelir.
3. **Scout belirsizliği = aralık + bulanık bant.** Az gözlemlenmişken sayı yerine **aralık** ("72–86") ve bar'da soluk accent belirsizlik bandı; potansiyel `? ? ?`. Scout kalitesi arttıkça netleşir.

Sayılar tabular; iki sütun (Teknik | Fiziksel) + gruplar. Referans mockup'lar: `profile_role_setpieces`, `all_role_profiles`, `profile_masked_vs_unmasked`.

---

## 5. Kulüp arması sistemi (parametrik — kritik)

Yüzlerce kurgusal kulüp için tutarlı arma üretimi:

```
Arma = KalkanŞekli (set: 5–6) × RenkÇifti (kulüp paleti) × MerkezMotif (set: ~20) × [opsiyonel Bant/Yıldız]
```

Hepsi SVG; token paletinden beslenir. "Her kulüp elle çizilsin" tuzağını çözer ve stili kilitler.

---

## 6. Oyuncu temsili

- **MVP'de AI portresi YOK** (ölçekte tutarsız ve uncanny). Yerine: kulüp renklerinde **initial-token** veya küçük bir **stilize silüet/avatar** seti (birkaç şablon, basit parametrelerle).
- Portre istenirse: sonraya, kilitli bir üretim hattı + sabit stil anchor ile.

---

## 7. Asset envanteri

UI kit (bileşen + state'ler, 9-slice), ikon seti, arma sistemi, oyuncu token/avatar, tipografi ölçeği, maç-anlatı ekranı görsel dili, **(sonraya) paylaşılabilir "legend card"** (post-MVP; Efsaneler Salonu için dışa aktarılabilir kart — oyuncunun yayını + "found free → sold 40M" beat'i, broadcast stilinde; GDD §4.9), (sonraya) minimal 2D "key moment" saha görseli.

---

## 8. Teknik kısıtlar

- Hedef mobil yoğunluk: @2x/@3x; safe-area farkında.
- **SVG kaynak → sprite atlas** (veya mümkün yerde vektör). İkonlar atlaslanır; UI 9-slice.
- Her asset token paletine referans verir (**hard-coded hex yasak**) → tema tek yerden değişir.
- UI Toolkit sinerjisi: token'lar ≈ USS değişkenleri, SVG ≈ UI Toolkit vektör.

---

## 9. Tutarlılık protokolü (AI art için — NON-NEGOTIABLE)

1. **Kilitli style anchor:** `ART_STYLE.html` + palet/ikon/arma/ekran örnek levhası.
2. Tüm art **SVG** olarak, aynı token'lara referansla üretilir.
3. Her üretim isteğinde style anchor'a atıf (palet + çizgi ağırlığı + köşe dili + "yapma" listesi).
4. Yeni asset, mevcut sete *yan yana* konup tutarlılık gözden geçirilmeden merge edilmez.

**Yapma:** ikinci parlak accent (semantik hariç) · MVP'de AI oyuncu portresi · gradient/gölge şöleni (derinlik kenarlık + boşlukla) · cream+serif+terracotta klişesi · emoji'yi kalıcı ikon yerine koymak.
