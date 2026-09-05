# GAFFER — UI/UX Referans Levhası

`ART_STYLE.md` **neye benzeyeceğini** söyler (renk, tipografi, ikon, arma). Bu levha **nasıl davranacağını** söyler:
ekran yapısı, gezinme, jest, dokunma hedefi. İkisi çelişirse ART_STYLE görünüşte, bu levha davranışta kazanır.

Kaynak, tür olarak bize en yakın ve **aynı motorla** yapılan oyundur: Football Manager. FM26 arayüzü Unity ile
yeniden yazıldı ve Sports Interactive tasarım gerekçesini açık yayımladı — bu, tahmin yürütmek yerine
gerekçesiyle birlikte kopyalanabilecek bir referans demek. İkinci kaynak grubu FM Touch'ın **eleştirileri**:
mobilde neyin battığı, neyin işlediğinden daha öğretici.

---

## 1. Üç ilke (FM26'dan alınır, aynen benimsenir)

SI'ın FM26 arayüzünü yeniden tasarlarken kullandığı üç ilke:

1. **Efficiency** — "making info quicker to access, navigation around the game faster and **reducing the number
   of button clicks**".
2. **Familiarity** — arayüz değişse de oyun "hâlâ tanıdığın Football Manager gibi hissetmeli".
3. **Predictability** — "easy to pick up and master".

**Gaffer'daki karşılığı.** Efficiency bizde en sert kısıttır: bir run tek sezondur, menajerin haftada verdiği
karar sayısı azdır, dolayısıyla **her fazladan dokunuş oyunun payından çalar**. Familiarity bizde *futbol
menajerliği* okuryazarlığıdır — kadro bir liste, taktik bir şekil, oyuncu bir kart; bunları yeniden icat etmeyiz.
Predictability, aynı jestin her ekranda aynı anlama gelmesidir (§3).

---

## 2. Tile & Card — ekran mimarisinin tek şeması

FM26'nın taşıyıcı fikri: *"tiles are the component parts of every in-game screen in FM26, providing key
snapshots of relevant info. When clicked on, each tile opens up into a Card that carries more detail."*

Yani ekran = **özet karolar**; dokunuş = **detay kartı**. Derinlik ekrana yayılmaz, bir katman aşağı iner.

**Bağlayıcı kural.** Gaffer'ın her ekranı bu iki seviyeyle kurulur:

| Seviye | Ne taşır | Gaffer'daki örnek |
|---|---|---|
| **Tile** | Tek bakışta okunan özet; sayı + isim, cümle değil | Sahadaki pozisyon kartı (`.slot`), lig satırı, oyuncu satırı (`.row`) |
| **Card / Sheet** | Karonun detayı ve o detaya ait eylemler | "Buraya kim oynasın" seçim yaprağı (`.sheet`), oyuncu profili, maç raporu |

Bundan çıkan alt kurallar:
- Bir karo **kendi başına** anlamlı olmalı; açıklamasını kart taşır.
- Kart, karonun **üstünde** açılır (overlay), yanına yeni bir sütun açmaz — telefonda sütun yoktur.
- Karttan çıkış her zaman görünürdür (§4, "menü kapanı").
- Bir bilgi iki karoda birden özetlenmez; tekrar, ekranın hangi karosunun otorite olduğunu belirsizleştirir.

**Not.** Bu, `#4 command in → outcome out` ile aynı yöne bakar: karo outcome'un görünümü, kart o outcome'a
uygulanabilecek komutların yeri.

---

## 3. Jest sözlüğü (tek anlam, her ekranda aynı)

FM Mobile taktikte hem **dokun-sonra-dokun** hem **sürükle-üstüne-bırak** destekler; ikisi birlikte bulunur,
biri diğerinin yerine geçmez. Sebebi Predictability: baş parmağıyla sürükleyen de, emin olmak isteyip iki kez
dokunan da aynı sonuca varır.

Gaffer'ın sözlüğü:

| Jest | Anlamı | Nerede |
|---|---|---|
| **Dokun (karo)** | "Bunu bana anlat / burayı doldur" → kart açar | Her yerde |
| **Sürükle (karo → karo)** | "Yer değiştir" | Yalnız **kaymayan** yüzeylerde (taktik tahtası) |
| **Sürükle (içerik, dikey)** | Kaydır | Kayan her liste — `DragToScroll` |
| **Basılı tut** | Kayan bir listede öğeyi *yerinde* yeniden sırala | Şimdilik hiçbir yerde; `DragGesture` destekler |

**Kayan bir yüzeyde sürüklemeyi iki anlama geldirme.** Bir liste hem kaydırılıp hem içinden oyuncu
sürüklenmeye çalışıldığında her eşik/gecikme ayarı yalnızca hangisinin bozuk hissettirdiğini değiştirir.
Çözüm sürüklemeyi kaldırmak ve yerine **kart** koymaktır (yedek listesinden sürükleme kaldırıldı, "buraya kim
oynasın" yaprağı geldi) — bu aynı zamanda Efficiency'yi düşürmez, çünkü kart tek dokunuşla açılır.

---

## 4. Mobilde batma listesi (FM Touch eleştirilerinden — hiçbiri tekrarlanmayacak)

Pocket Tactics'in FM26 Touch incelemesi, doğrudan bizim de düşebileceğimiz dört tuzağı adlandırıyor:

1. **Dokunuş kaydolmuyor.** *"Often, I'd find the game not reacting to my taps, even after poking the screen
   multiple times."* → Girdi güvenilirliği bir cila işi değil, **birinci sınıf özelliktir**. Bizdeki karşılığı:
   satırın tamamı hedeftir, çocuk elemanlar `PickingMode.Ignore`'dur, ve bir kaydırma bittiğinde altındaki
   satır bunu seçim sanmaz.
2. **Yedekte kimin aynı pozisyonu oynadığı gösterilmiyor.** *"the game doesn't indicate who on your bench plays
   in the same position"* — menajeri tek tek profil açmaya zorluyor. → Seçim yaprağında uygunluk
   **işaretlenir** (`.row--natural` / `.row--samline`); bu bir süs değil, referansın açıkça eksik dediği şeydir.
3. **Menü kapanı.** *"I couldn't escape the menu until the final whistle."* → Açılan her yüzeyin **görünür ve
   ulaşılabilir** bir çıkışı olur. Bir görünümü gizleyen düğme, gizlediği şeyin içinde yaşayamaz (tek yönlü
   geçiş hatası tam olarak buydu).
4. **Onay, eylemin olduğu yerde değil.** FM26 Touch'ta oyuncu değişikliğinin onayı mesajlar menüsündedir. →
   Sonuç, komutun verildiği ekranda gösterilir; menajer sonucu aramaya gitmez.

---

## 5. Okunabilirlik ve dokunma hedefi

SI, FM26 için erişilebilirlik çıtasını yükselttiklerini ve *"unreadable font sizes"* ile tanımsız kontrastları
düzelttiklerini yazıyor — yani bu, büyük bir stüdyonun da düzeltmek zorunda kaldığı bir hata.

Bağlayıcı taban değerler:

- **Dokunma hedefi ≥ 44pt** (Apple HIG: "minimum tappable area of 44×44 points for all controls").
  Material 48dp der; ikisinin büyüğünü almak bedava, `--tap-min` bunu karşılar.
- **Referans çözünürlük 1080×1920.** Fiziksel piksel ile logical point karıştırılmaz: 1080 fiziksel px ≈ 400
  point. Bir tokeni "gözle küçük görünmüyor" diye seçmek, editörde doğru telefonda üç kat küçük demektir.
- **Kontrast tokenden gelir.** ART_STYLE'ın parlaklık rampası değer gösterir; metin/zemin çifti gözle değil
  token çiftiyle seçilir.

---

## 6. Gezinme ve ekran adlandırma

- FM'de menü, "üzerinde bulunduğun ekranların listesi"dir ve ekranlar
  `<ekran>: <bölüm> <panel>` diye adlandırılır. Bu, **hiyerarşiyi başlıkta görünür kılar** — nerede olduğunu
  başlığı okuyarak bilirsin. Gaffer'da başlık her zaman *ekran + o an bakılan şey* olur.
- FM26 ana ekranı ("Portal") gelen kutusuyla **birleştirildi**: mesaj, haber, fikstür ve takvim tek yerde.
  Gaffer'ın hafta ekranı bunun karşılığıdır — anlatı, fikstür ve karar kartları ayrı sekmelere dağılmaz.
- Filtreler ekranın **sağ üstünde** durur (FM konvansiyonu). 50 bin oyunculu transfer listesi geldiğinde
  filtre yeri tartışma konusu olmayacak.
- Navigasyon üstte, kategori sayısı az; her kategori altında derinlik. Telefonda alt bar da meşrudur, ama
  **kategori sayısı** her hâlükârda tek elin parmaklarını geçmez.

---

## 7. Bunu bir sonraki ekranda nasıl kullanacağız

Yeni bir ekran tasarlarken sırayla sorulur:

1. Bu ekranın **karoları** ne? (Her karo tek bakışlık bir özet mi, yoksa gizli bir tablo mu?)
2. Her karonun **kartı** ne, ve kartta hangi komutlar var?
3. Menajerin buradaki tipik işi **kaç dokunuşta** bitiyor? (Efficiency — sayıyı düşür.)
4. Aynı jest bu ekranda başka bir ekrandakinden farklı bir şey mi yapıyor? (Predictability — yapmamalı.)
5. Kaçış görünür mü, onay eylemin yerinde mi, hedefler 44pt mi?

---

## Kaynaklar

- [FM26's Reimagined User Interface — footballmanager.com](https://www.footballmanager.com/fm26/features/fm26s-reimagined-user-interface) (üç ilke, Tile & Card, Portal, erişilebilirlik)
- [Football Manager 26 Touch review — Pocket Tactics](https://www.pockettactics.com/football-manager-26-touch/review) (mobil batma listesi)
- [The User Interface — FM24 manual, Sports Interactive](https://community.sports-interactive.com/sigames-manual/football-manager-2024/the-user-interface-r4951/) (ekran adlandırma, filtre yeri)
- [Tactics — FM26 Mobile manual, Sports Interactive](https://community.sports-interactive.com/sigames-manual/football-manager-2026-mobile/tactics-r5249/) (dokun-dokun ve sürükle-bırak birlikte)
- [ScrollView with drag scrolling — Unity Discussions](https://discussions.unity.com/t/scrollview-with-drag-scrolling/861606) (UI Toolkit'te içerik sürükleyerek kaydırma yalnız touch'tadır; mouse için elle yazılır)
- [Foundations: target sizes — TetraLogical](https://tetralogical.com/blog/2022/12/20/foundations-target-size/) (Apple 44pt / Material 48dp)
