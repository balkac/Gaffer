using System.Collections.Generic;
using Gaffer.Common.Localization;

namespace Gaffer.Infrastructure.Localization
{
    /// <summary>
    /// The words. Every player-facing string the drama cards show, in English and Turkish, keyed by the
    /// keys <c>DramaCatalog.Default</c> hands out — the built-in copy the same way
    /// <c>TraitCatalog.Default</c> is the built-in trait set, and <see cref="StringTableSO"/> overrides
    /// it wholesale when a table asset is authored (config-as-override, ARCHITECTURE §7).
    ///
    /// <para><b>Why it sits in Infrastructure.</b> NON-NEGOTIABLE #8 splits keys from words:
    /// <c>Common</c>/<c>Domain</c>/<c>Application</c> may carry a key and never a word, and the string
    /// table is Infrastructure's job (CLAUDE.md layer map, "Infrastructure(Configuration SO +
    /// Persistence + <b>Localization</b>)"). This file is nevertheless framework-free and is compiled
    /// into the headless test bridge by name, exactly as <c>UserData</c> and <c>Tools/SeasonHarness</c>
    /// are — because the guard that matters (every key the catalog references has words in every
    /// shipped locale) has to run in <c>dotnet test</c>, not only when someone opens Unity. Adding
    /// <c>using UnityEngine</c> here breaks that build, loudly, which is the correct answer.</para>
    ///
    /// <para><b>English is the reference locale</b> (CLAUDE.md, "Dil / yerelleştirme"): the English row
    /// is written first and the Turkish row is written NATIVELY against the same situation — a
    /// different idiom in Turkish is right, a word-for-word rendering of the English is not.</para>
    ///
    /// <para><b>House rules for anything added here.</b> A title is a headline and not a label for the
    /// mechanic ("Four in the morning", not "Night club scandal"). A body is one or two sentences of
    /// what actually happened, with a concrete detail, in the voice of a press round-up or an
    /// assistant's briefing — never narration about how the manager feels, and never a sentence that
    /// would fit any other event. A choice is the decision a manager would make in his own words
    /// ("Fine him a week's wages"), not the effect: the card already prints the effects underneath in
    /// real numbers, so a label that restates them is wasted. Interpolated names stay ATOMIC and never
    /// take a suffix — see <see cref="TextTemplate"/> for why that is a build failure and not a note.
    /// No real club, player or competition (PROGRESS decision #6).</para>
    /// </summary>
    public static class GameStrings
    {
        /// <summary>The built-in copy. Built once; the table is immutable.</summary>
        public static StringTable Default { get; } = Build();

        private static StringTable Build()
        {
            return new StringTable(
                Locales.Shipped,
                new[]
                {
                    // --- the screens (Faz 7) ---------------------------------------------------------
                    // Interface copy, not narrative: a label names what a thing IS, in as few words as it
                    // takes, and a button names the ACTION rather than its result. Turkish is written
                    // native — the shortest natural phrase, not the English one rendered word for word.
                    Row(
                        "ui.squad.eleven",
                        "THE ELEVEN",
                        "İLK 11"),
                    Row(
                        "ui.squad.bench",
                        "BENCH",
                        "YEDEKLER"),
                    Row(
                        "ui.squad.position",
                        "POSITION",
                        "SIRA"),
                    Row(
                        "ui.squad.week",
                        "WEEK",
                        "HAFTA"),
                    Row(
                        "ui.squad.attack",
                        "ATK",
                        "HÜC"),
                    Row(
                        "ui.squad.midfield",
                        "MID",
                        "ORT"),
                    Row(
                        "ui.squad.defence",
                        "DEF",
                        "SAV"),
                    Row(
                        "ui.squad.view_pitch",
                        "Pitch",
                        "Saha"),
                    Row(
                        "ui.squad.view_list",
                        "List",
                        "Liste"),
                    Row(
                        "ui.squad.picker_who",
                        "WHO PLAYS HERE",
                        "BURAYA KİM OYNASIN"),
                    Row(
                        "ui.squad.picker_where",
                        "WHERE DOES HE PLAY",
                        "NEREDE OYNASIN"),
                    Row(
                        "ui.action.auto_pick",
                        "Auto-pick",
                        "Otomatik seç"),
                    Row(
                        "ui.action.play_week",
                        "Play the week",
                        "Haftayı oyna"),
                    Row(
                        "ui.message.no_fixture",
                        "No fixture this week.",
                        "Bu hafta maç yok."),

                    // --- what a position is CALLED, in three characters ------------------------------
                    // The tiles on the board and every row in the sheet are labelled with these, so they
                    // are read more often than any other string in the game and are the only ones whose
                    // LENGTH is load-bearing: `.row__role` is a FIXED 100px column at 22px with 2px
                    // letter-spacing, so a fourth character crowds it and a longer one clips rather than
                    // reflows. UiCopyTests pins three.
                    //
                    // Football Manager ships ONE code set worldwide — its Turkish build still says GK, DC,
                    // DR — because a single set is cheaper to maintain across thirty languages. We ship
                    // two, because we ship two languages and Turkish football already has its own
                    // abbreviations: a Turkish player reads STP and DOS without translating, and reads DC
                    // by first remembering what it stands for in English. English keeps the conventional
                    // codes rather than FM's positional ones (RB, not DR) — they are what an English
                    // reader outside FM expects.
                    Row("role.goalkeeper.abbrev", "GK", "KL"),
                    Row("role.right_back.abbrev", "RB", "SĞB"),
                    Row("role.centre_back.abbrev", "CB", "STP"),
                    Row("role.left_back.abbrev", "LB", "SLB"),
                    Row("role.defensive_midfield.abbrev", "DM", "DOS"),
                    Row("role.central_midfield.abbrev", "CM", "MOS"),
                    Row("role.attacking_midfield.abbrev", "AM", "OOS"),
                    Row("role.right_midfield.abbrev", "RM", "SĞO"),
                    Row("role.left_midfield.abbrev", "LM", "SLO"),
                    Row("role.right_wing.abbrev", "RW", "SĞA"),
                    Row("role.left_wing.abbrev", "LW", "SLA"),
                    Row("role.striker.abbrev", "ST", "FV"),

                    // --- the narrative layer (Faz 5.4) -----------------------------------------------
                    // One line per kind of career moment. House rules on top of the ones above, and they
                    // are what keep a journey readable rather than a fixture list:
                    //   * ONE sentence. These stack — a career prints a dozen of them in a column — and a
                    //     second sentence in each turns a timeline into an essay.
                    //   * The line says what HAPPENED, never how anyone felt about it. "His first goal for
                    //     the club" earns its weight from being true and rare; "the moment he announced
                    //     himself" is the game telling the player what to feel, which is the one thing a
                    //     memory must not do.
                    //   * The name is ATOMIC and takes no suffix (TextTemplate enforces this, and it is a
                    //     build failure, not a note). Turkish is written natively AROUND that constraint:
                    //     the name sits where a Turkish sentence can leave it uninflected — usually first,
                    //     or before a comma — rather than the English word order being copied and a suffix
                    //     wished onto it.
                    Row(
                        "moment.debut",
                        "{player} made his first appearance for the club.",
                        "{player} kulüpteki ilk maçına çıktı."),
                    // The minute arrives ALREADY MARKED — "68'" rather than "68" — because the mark is
                    // part of how a minute is written, and writing it in the template would put an
                    // apostrophe straight after an interpolated value, which is exactly the shape the
                    // suffix guard exists to refuse. It caught this copy on the first run. Rendering the
                    // mark at the edge also leaves each locale free to write a minute its own way.
                    Row(
                        "moment.first_goal",
                        "{minute} — {player} scored his first goal for the club.",
                        "{minute} — {player} kulüpteki ilk golünü attı."),
                    Row(
                        "moment.big_match_goal",
                        "{minute} — {player} scored on the day it mattered.",
                        "{minute} — {player} büyük maçta golünü buldu."),
                    Row(
                        "moment.derby_goal",
                        "{minute} — {player} scored in the derby.",
                        "{minute} — {player} derbide golü attı."),
                    Row(
                        "moment.title_decider_goal",
                        "{minute} — {player} scored with the title on the line.",
                        "{minute} — {player} şampiyonluk yolunda golü attı."),
                    Row(
                        "moment.relegation_goal",
                        "{minute} — {player} scored in a relegation six-pointer.",
                        "{minute} — {player} küme düşme mücadelesinde golü attı."),
                    Row(
                        "moment.brace",
                        "{player} scored twice.",
                        "{player} iki gol attı."),
                    Row(
                        "moment.hattrick",
                        "{player} scored a hat-trick.",
                        "{player} hat-trick yaptı."),
                    Row(
                        "moment.appearance_milestone",
                        "{player} reached {count} appearances for the club.",
                        "{player} kulüpteki {count}. maçına çıktı."),
                    Row(
                        "moment.goal_milestone",
                        "{player} reached {count} goals for the club.",
                        "{player} kulüpteki {count}. golüne ulaştı."),
                    Row(
                        "moment.signing",
                        "{player} signed for {club}.",
                        "{player} artık {club} oyuncusu."),
                    Row(
                        "moment.sale",
                        "{player} was sold.",
                        "{player} satıldı."),
                    Row(
                        "moment.academy_arrival",
                        "{player} came through the academy.",
                        "{player} altyapıdan geldi."),
                    Row(
                        "moment.breakout_season",
                        "{player} kicked on this season.",
                        "{player} bu sezon sıçrama yaptı."),
                    Row(
                        "moment.retirement",
                        "{player} hung up his boots.",
                        "{player} kariyerini noktaladı."),

                    // --- transfer-request ------------------------------------------------------------
                    // He is good enough that someone came in for him and the window is open. Refusing
                    // costs him morale, selling takes his fee, keeping him costs cash.
                    Row(
                        "drama.transfer_request.title",
                        "His agent called first",
                        "Önce menajeri aradı"),
                    Row(
                        "drama.transfer_request.body",
                        "{player} asked to be let go this morning, before training. The window is open and someone has already been on the phone about him.",
                        "{player} bu sabah antrenmandan önce kapıyı çaldı, gitmek istiyor. Transfer dönemi açık ve kendisini soran da olmuş."),
                    Row(
                        "drama.transfer_request.refuse",
                        "Tell him he is not for sale",
                        "Satılık olmadığını söyle"),
                    Row(
                        "drama.transfer_request.sell",
                        "Let him go if the money is right",
                        "Para tamamsa yolu açık olsun"),
                    Row(
                        "drama.transfer_request.persuade",
                        "Improve his deal and keep him",
                        "Sözleşmesini iyileştir, kalsın"),

                    // --- night-club-scandal ----------------------------------------------------------
                    // A young player, photographed. The fine is one week of his actual wage; backing him
                    // lifts him and costs the rest of the squad, who watched him get away with it.
                    Row(
                        "drama.night_club_scandal.title",
                        "Four in the morning",
                        "Sabahın dördü"),
                    Row(
                        "drama.night_club_scandal.body",
                        "{player} came out of a club at four on a Thursday and someone filmed the walk to the car. By breakfast it had been shared eleven thousand times.",
                        "{player} perşembeyi cumaya bağlayan gece dörtte kulüpten çıkarken arabaya kadar kamerayla takip edilmiş. Görüntü kahvaltıya kalmadan on bir bin kez paylaşıldı."),
                    Row(
                        "drama.night_club_scandal.fine",
                        "Fine him a week's wages",
                        "Bir haftalık maaşını kes"),
                    Row(
                        "drama.night_club_scandal.closed_doors",
                        "Keep it inside the building",
                        "Mesele bina içinde kalsın"),
                    Row(
                        "drama.night_club_scandal.back_him",
                        "Back him in front of the cameras",
                        "Kameraların önünde arkasında dur"),

                    // --- dressing-room-rift ----------------------------------------------------------
                    // Three defeats running, no single subject: it is the room, not a man.
                    Row(
                        "drama.dressing_room_rift.title",
                        "Nobody spoke on the coach home",
                        "Dönüş yolunda kimse konuşmadı"),
                    Row(
                        "drama.dressing_room_rift.body",
                        "Three defeats running, and Saturday's row in the tunnel is still doing the rounds. The senior players have stopped eating at the same table.",
                        "Üst üste üç yenilgi, üstüne cumartesi tünelde çıkan tartışma hâlâ kulaktan kulağa dolaşıyor. Tecrübeliler artık aynı masada yemiyor."),
                    Row(
                        "drama.dressing_room_rift.meeting",
                        "Get them in a room and have it out",
                        "Hepsini bir odaya topla, konuşun"),
                    Row(
                        "drama.dressing_room_rift.let_it_burn",
                        "Say nothing and let them sort it",
                        "Karışma, kendileri çözsün"),

                    // --- fan-protest -----------------------------------------------------------------
                    // Four defeats. The club is the subject, so {club} is the name that goes in.
                    Row(
                        "drama.fan_protest.title",
                        "They waited by the players' car park",
                        "Otoparkın çıkışını tuttular"),
                    Row(
                        "drama.fan_protest.body",
                        "Four defeats on the spin, and three hundred of them stayed behind after the whistle with the {club} board's names on their banners. The stewards moved them off an hour after full time.",
                        "Dört maçtır galibiyet yok; maç sonunda üç yüz kadar taraftar otoparkın önünde bekledi, pankartlarda {club} yönetiminin isimleri vardı. Güvenlik onları ancak bir saat sonra dağıtabildi."),
                    Row(
                        "drama.fan_protest.face_them",
                        "Walk out and talk to them",
                        "Dışarı çık, yüzlerine karşı konuş"),
                    Row(
                        "drama.fan_protest.ignore",
                        "Stay inside and let it pass",
                        "İçeride kal, geçmesini bekle"),

                    // --- wonderkid-wants-minutes -----------------------------------------------------
                    // Nineteen or under, a long way below his ceiling, and benched — the trigger is
                    // "he is not playing", so the copy is about minutes and nothing else.
                    Row(
                        "drama.wonderkid_wants_minutes.title",
                        "Warming up, week after week",
                        "Her hafta ısınıyor, oturuyor"),
                    Row(
                        "drama.wonderkid_wants_minutes.body",
                        "{player} has not started a match all season and spent Saturday warming up on the touchline without coming on. His father stopped an assistant at the training-ground gate to ask whether the boy would play somewhere else.",
                        "{player} sezon boyunca bir kez bile ilk on birde başlamadı; cumartesi de kenarda ısındı durdu, oyuna hiç girmedi. Babası tesisin kapısında yardımcı antrenörü durdurup çocuğun başka yerde oynayıp oynamayacağını sordu."),
                    Row(
                        "drama.wonderkid_wants_minutes.promise_starts",
                        "Tell him he starts the next one",
                        "Söz ver, gelecek maç ilk on birde"),
                    Row(
                        "drama.wonderkid_wants_minutes.wait_your_turn",
                        "Tell him to earn it in training",
                        "Sırasını beklesin, antrenmanda kazansın"),

                    // --- budget-cut ------------------------------------------------------------------
                    // Institutional and rare. The money is stated by the effect lines, not here.
                    Row(
                        "drama.budget_cut.title",
                        "The board has been through the books",
                        "Yönetim defterleri açtı"),
                    Row(
                        "drama.budget_cut.body",
                        "The finance director spent the weekend inside the {club} accounts. The club is paying out more than the gate and the shirt deal bring in, and he wants it corrected before the next board meeting.",
                        "Mali işler direktörü hafta sonunu {club} hesaplarının başında geçirmiş. Gişeden ve forma sponsorundan girenden fazlası çıkıyor; işin bir sonraki yönetim toplantısına kadar düzelmesini istiyor."),
                    Row(
                        "drama.budget_cut.accept",
                        "Take the cut and get on with it",
                        "Kesintiyi kabul et, işine bak"),
                    Row(
                        "drama.budget_cut.fight_it",
                        "Fight it line by line at the meeting",
                        "Toplantıda kalem kalem itiraz et"),

                    // --- captain-succession ----------------------------------------------------------
                    // Thirty-three or over and a dressing-room leader. "Anoint" is what passes the trait
                    // on to a team-mate; the card names the heir afterwards, so the copy does not guess.
                    Row(
                        "drama.captain_succession.title",
                        "He asked who takes the armband",
                        "Bandı kime bırakacağını sordu"),
                    Row(
                        "drama.captain_succession.body",
                        "{player} has had the armband for years and knows he is nearer the end than the middle. He came to find you after training: he would rather hand it over himself than have it taken off him.",
                        "{player} yıllardır kaptanlık bandını taşıyor ve sonun yakın olduğunu kendisi de biliyor. Antrenmandan sonra yanına geldi: bandı elinden alınmasındansa kendi eliyle devretmeyi tercih ediyor."),
                    Row(
                        "drama.captain_succession.anoint",
                        "Let him name the next captain",
                        "Yeni kaptanı kendisi seçsin"),
                    Row(
                        "drama.captain_succession.your_call",
                        "Tell him the armband is your call",
                        "Bandın kimde olacağına sen karar ver"),

                    // --- club-takeover ---------------------------------------------------------------
                    // Once per run, ever. A set-piece: new owners, and whether you are in the photograph.
                    Row(
                        "drama.club_takeover.title",
                        "New names on the paperwork",
                        "Evrakta yeni isimler"),
                    Row(
                        "drama.club_takeover.body",
                        "The sale of {club} went through on Thursday afternoon. The new owners want the manager standing beside them at Monday's press conference, smiling.",
                        "{club} satışı perşembe öğleden sonra tamamlandı. Yeni sahipler pazartesi günkü basın toplantısında teknik direktörü yanlarında, gülümserken görmek istiyor."),
                    Row(
                        "drama.club_takeover.back_the_owners",
                        "Stand beside them at the press conference",
                        "Basın toplantısında yanlarında dur"),
                    Row(
                        "drama.club_takeover.keep_your_distance",
                        "Stay out of it and coach the team",
                        "Sen işine bak, takımı çalıştır"),

                    // --- press-war -------------------------------------------------------------------
                    // Only fires on a press-magnet. Letting him talk lifts him and costs the room.
                    Row(
                        "drama.press_war.title",
                        "Four minutes on a live microphone",
                        "Canlı yayında dört dakika"),
                    Row(
                        "drama.press_war.body",
                        "{player} was asked one question about the referee and answered for four minutes. Two papers led with it and the club phone has not stopped since.",
                        "{player} hakemle ilgili tek soruya dört dakika cevap verdi. İki gazete manşetten verdi, kulübün telefonu o günden beri susmuyor."),
                    Row(
                        "drama.press_war.muzzle_him",
                        "Keep him away from the microphones",
                        "Mikrofonlardan uzak tut"),
                    Row(
                        "drama.press_war.let_him_talk",
                        "Let him say what he likes",
                        "Bırak, ne diyecekse desin"),

                    // --- contract-standoff -----------------------------------------------------------
                    // Thirty or over and still worth a place. The year costs cash; refusing costs him.
                    Row(
                        "drama.contract_standoff.title",
                        "One more year, he says",
                        "Bir yıl daha istiyor"),
                    Row(
                        "drama.contract_standoff.body",
                        "{player} is into the last months of his contract and his agent has asked for another twelve months at the same money. He wants an answer before the window opens.",
                        "{player} sözleşmesinin son aylarında; menajeri aynı rakamla bir yıllık uzatma istiyor. Cevabı transfer dönemi açılmadan almak niyetinde."),
                    Row(
                        "drama.contract_standoff.give_him_the_year",
                        "Give him the extra year",
                        "İstediği bir yılı ver"),
                    Row(
                        "drama.contract_standoff.refuse",
                        "Let the contract run down",
                        "Uzatma yok, sözleşme bitsin"),
                });
        }

        // One row, one key, both locales side by side — the CSV shape Unity's Localization package
        // uses, so Faz 7's swap is a reader change and not a content change.
        private static StringTableEntry Row(string key, string english, string turkish)
        {
            return new StringTableEntry(
                key,
                new List<LocaleText>(2)
                {
                    new LocaleText(Locales.Reference, english),
                    new LocaleText(Locales.Turkish, turkish),
                });
        }
    }
}
