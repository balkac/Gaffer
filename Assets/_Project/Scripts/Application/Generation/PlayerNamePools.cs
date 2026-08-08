using System.Collections.Generic;
using Gaffer.Common;

namespace Gaffer.Application.Generation
{
    /// <summary>
    /// The world's name material, one <see cref="PlayerNamePool"/> per nationality. This table is the
    /// single source of truth for which nationalities exist: a nationality exists exactly when it has
    /// names, which is what stops the old defect where a generated "Italian" was called Harry Walker.
    ///
    /// <para><b>Why a grammar and not a list.</b> The pools it replaced held 28 first x 28 last names —
    /// 784 possible people, a hard ceiling that a 500-player league already ran into (367 distinct
    /// names) and a 50,000-player world shattered (784 distinct, 1.6%). Each pool here authors first
    /// names directly but *builds* surnames from stems x endings x separator, so ~124 authored strings
    /// per nationality yield 37,632 or more full names, and the ten pools together reach roughly
    /// 865,000. Ordinary given names carry no legal risk; the surnames are constructed, so no real
    /// squad list is copied (PROGRESS decision #6) and the names stay data, not localization keys.</para>
    ///
    /// <para><b>No diacritics, deliberately.</b> Every string here is plain ASCII. The shipped font
    /// atlas covers English and Turkish, so a Portuguese tilde or a Spanish acute would render as a
    /// missing glyph on device — a believability regression worse than the accent's absence.</para>
    ///
    /// <para><b>Cost.</b> Everything is <c>static readonly</c>, built once at type initialisation
    /// (PERFORMANCE §8). Drawing a player costs one rng value for the nationality plus two for the
    /// name, and one string allocation; nothing here is rebuilt, copied or LINQ'd per call.</para>
    /// </summary>
    public static class PlayerNamePools
    {
        // ---------------------------------------------------------------- England

        private static readonly string[] EnglandFirstNames =
        {
            "James", "William", "Thomas", "George", "Harry", "Jack", "Charlie", "Oscar",
            "Leo", "Arthur", "Henry", "Alfie", "Freddie", "Theo", "Ethan", "Noah",
            "Daniel", "Samuel", "Joseph", "Adam", "Owen", "Callum", "Connor", "Kyle",
            "Nathan", "Aaron", "Reece", "Liam", "Toby", "Elliot", "Marcus", "Dominic",
            "Spencer", "Rory", "Bailey", "Jude", "Miles", "Isaac", "Louie", "Ollie",
            "Sonny", "Bobby", "Reuben", "Jonah", "Wesley", "Curtis", "Ashton", "Tyler",
            "Declan", "Lewis", "Joel", "Kieran", "Brandon", "Hugo", "Stanley", "Alfred",
        };

        private static readonly string[] EnglandStems =
        {
            "Ash", "Brad", "Cal", "Dun", "Elm", "Far", "Gar", "Hal",
            "Ing", "Kel", "Lang", "Marl", "Nor", "Oak", "Pem", "Quar",
            "Rad", "Stan", "Thorn", "Wen", "Black", "Brack", "Corn", "Dray",
            "Elder", "Fen", "Gil", "Hather", "Iven", "Ken", "Lyn", "Med",
            "Nay", "Ock", "Barn", "Ral", "Sed", "Tal", "Under", "Vin",
            "Wal", "Wick", "Yar", "Bram", "Chad", "Har", "Hem", "Kirk",
            "Lod", "Mor", "Nether", "Pen", "Red", "Shel", "Wol", "Whit",
        };

        private static readonly string[] EnglandEndings =
        {
            "worth", "ley", "ton", "field", "wood", "ridge", "brook", "ford",
            "well", "stone", "don", "bury",
        };

        // ---------------------------------------------------------------- Scotland

        private static readonly string[] ScotlandFirstNames =
        {
            "Callum", "Euan", "Fraser", "Hamish", "Angus", "Duncan", "Malcolm", "Alistair",
            "Struan", "Gordon", "Blair", "Kenneth", "Douglas", "Innes", "Lachlan", "Murray",
            "Ruaridh", "Stuart", "Craig", "Iain", "Logan", "Ross", "Cameron", "Finlay",
            "Archie", "Brodie", "Rory", "Dougal", "Ewan", "Niall", "Torin", "Alasdair",
            "Colin", "Gregor", "Hugh", "Jamie", "Lorne", "Magnus", "Neil", "Kerr",
            "Sandy", "Tavish", "Graeme", "Hector", "Rab", "Fergus", "Kenzie", "Ronan",
            "Greig", "Murdo", "Calum", "Drew", "Kyle", "Rowan", "Shaw", "Wallace",
        };

        private static readonly string[] ScotlandStems =
        {
            "Kel", "Aul", "Bain", "Braid", "Cal", "Cor", "Cul", "Dair",
            "Don", "Dour", "Ewen", "Fad", "Farl", "Gil", "Gow", "Glen",
            "Hard", "Rait", "Ivor", "Kean", "Ken", "Kin", "Lach", "Lam",
            "Lear", "Len", "Lor", "Mair", "Mor", "Muir", "Nair", "Neil",
            "Ogil", "Ork", "Pher", "Quar", "Ram", "Ran", "Rob", "Ruar",
            "Sken", "Sorl", "Tag", "Tar", "Tor", "Vean", "Wal", "Whit",
            "Yair", "Bal", "Brod", "Cair", "Dun", "Fing", "Guth", "Strath",
        };

        private static readonly string[] ScotlandEndings =
        {
            "an", "och", "ie", "ay", "ell", "ison", "mont", "our",
            "ish", "ray", "ny", "ock",
        };

        private static readonly string[] ScotlandSeparators = { " ", " ", " ", " ", " Mac", " Mc" };

        // ---------------------------------------------------------------- Wales

        private static readonly string[] WalesFirstNames =
        {
            "Rhys", "Owain", "Gareth", "Dylan", "Ieuan", "Cai", "Bleddyn", "Emrys",
            "Geraint", "Huw", "Iwan", "Llion", "Meirion", "Osian", "Tomos", "Aled",
            "Bryn", "Carwyn", "Dewi", "Elis", "Gwilym", "Hefin", "Idris", "Ianto",
            "Lloyd", "Macsen", "Morgan", "Nye", "Pryderi", "Rhodri", "Sion", "Steffan",
            "Taliesin", "Trystan", "Wyn", "Alun", "Arwel", "Berwyn", "Ceri", "Dafydd",
            "Eifion", "Emlyn", "Gethin", "Gwyn", "Iolo", "Islwyn", "Lewys", "Ellis",
            "Evan", "Ifor", "Padrig", "Rhydian", "Selwyn", "Tudur", "Vaughan", "Wynne",
        };

        private static readonly string[] WalesStems =
        {
            "Ev", "Ow", "Grif", "Hen", "Prit", "Vaugh", "Mer", "Bev",
            "Llyr", "Rhod", "Pen", "Tren", "Nan", "Aber", "Cad", "Cled",
            "Din", "Eir", "Ffos", "Gwyl", "Hef", "Iest", "Llan", "Mael",
            "Nev", "Pow", "Rhud", "Tal", "Tud", "Wynn", "Bryn", "Caer",
            "Cyn", "Der", "Eth", "Gar", "Glyn", "Hyf", "Meir", "Mad",
            "Mor", "Nyth", "Ogl", "Pemb", "Rhyd", "Seg", "Tref", "Tyw",
            "Uch", "Vron", "Wern", "Bran", "Cern", "Dyf", "Elw", "Gwer",
        };

        private static readonly string[] WalesEndings =
        {
            "ans", "ens", "iths", "ard", "ith", "an", "ell", "ion",
            "ys", "ric", "edd", "or",
        };

        // ---------------------------------------------------------------- Ireland

        private static readonly string[] IrelandFirstNames =
        {
            "Sean", "Ciaran", "Eoin", "Padraig", "Niall", "Cormac", "Declan", "Fintan",
            "Ronan", "Oisin", "Aidan", "Brendan", "Colm", "Dara", "Emmet", "Fergal",
            "Gearoid", "Kian", "Liam", "Micheal", "Odhran", "Peadar", "Ruairi", "Senan",
            "Tadhg", "Ultan", "Conor", "Diarmuid", "Eamon", "Finbar", "Iarla", "Kevin",
            "Lorcan", "Malachy", "Naoise", "Oran", "Rory", "Shane", "Turlough", "Barry",
            "Cathal", "Donal", "Enda", "Fiachra", "Garvan", "Killian", "Lochlann", "Manus",
            "Owen", "Riordan", "Seamus", "Tomas", "Aengus", "Breandan", "Daithi", "Ruadhan",
        };

        private static readonly string[] IrelandStems =
        {
            "Bran", "Cass", "Con", "Dev", "Doh", "Dol", "Dun", "Far",
            "Fin", "Flan", "Gal", "Gorm", "Hen", "Hig", "Kear", "Kier",
            "Lough", "Mag", "Mal", "Mel", "Moon", "Mul", "Nol", "Nug",
            "Phel", "Quig", "Rat", "Rean", "Rig", "Roan", "Scan", "Shan",
            "Sween", "Teag", "Tier", "Toom", "Tul", "Twom", "Whel", "Breen",
            "Carr", "Clon", "Corr", "Cul", "Del", "Duff", "Ear", "Feen",
            "Gaff", "Han", "Kin", "Lav", "Mur", "Neel", "Kav", "Quin",
        };

        private static readonly string[] IrelandEndings =
        {
            "agan", "an", "ey", "ney", "ell", "erty", "on", "in",
            "ane", "ally", "ley", "aghan",
        };

        private static readonly string[] IrelandSeparators = { " ", " ", " ", " O'", " Mc" };

        // ---------------------------------------------------------------- France

        private static readonly string[] FranceFirstNames =
        {
            "Antoine", "Baptiste", "Cedric", "Damien", "Etienne", "Fabien", "Gaspard", "Hugo",
            "Julien", "Kilian", "Loic", "Mathis", "Nicolas", "Olivier", "Pascal", "Quentin",
            "Remi", "Sebastien", "Thibault", "Ugo", "Valentin", "Yann", "Adrien", "Bastien",
            "Clement", "Dorian", "Emile", "Florian", "Gaetan", "Herve", "Jules", "Lucas",
            "Marius", "Noe", "Octave", "Pierre", "Raphael", "Simon", "Theo", "Vincent",
            "Xavier", "Amaury", "Benoit", "Corentin", "Dimitri", "Edouard", "Franck", "Gregoire",
            "Hadrien", "Ismael", "Jerome", "Ludovic", "Maxime", "Nolan", "Romain", "Tanguy",
        };

        private static readonly string[] FranceStems =
        {
            "Bar", "Beau", "Bel", "Bre", "Cham", "Char", "Cla", "Cour",
            "Del", "Dre", "Dur", "Fon", "Four", "Gau", "Gir", "Gran",
            "Guer", "Jol", "Lain", "Lam", "Lan", "Lav", "Leb", "Lem",
            "Mar", "Mer", "Mon", "Mor", "Nou", "Pau", "Per", "Pey",
            "Pon", "Quen", "Rem", "Rib", "Rol", "Rou", "Sav", "Ser",
            "Tar", "Thi", "Tour", "Val", "Var", "Ver", "Vig", "Vin",
            "Arn", "Chal", "Fer", "Mal", "Sou", "Bri", "Dun", "Gour",
        };

        private static readonly string[] FranceEndings =
        {
            "bois", "court", "dier", "mont", "rand", "teau", "vier", "nier",
            "sson", "let", "ville", "quet",
        };

        private static readonly string[] FranceSeparators = { " ", " ", " ", " ", " ", " ", " ", " Le " };

        // ---------------------------------------------------------------- Spain

        private static readonly string[] SpainFirstNames =
        {
            "Alvaro", "Andres", "Borja", "Carlos", "David", "Enrique", "Fernando", "Gonzalo",
            "Hector", "Ignacio", "Javier", "Koldo", "Luis", "Manuel", "Nacho", "Oscar",
            "Pablo", "Quique", "Raul", "Sergio", "Tomas", "Unai", "Victor", "Xavi",
            "Yago", "Adrian", "Aitor", "Bruno", "Cesar", "Diego", "Eduardo", "Felipe",
            "Gabriel", "Hugo", "Inigo", "Jorge", "Julio", "Lucas", "Marcos", "Mateo",
            "Nicolas", "Pedro", "Rafael", "Ramiro", "Ruben", "Samuel", "Santiago", "Sebastian",
            "Teo", "Vicente", "Alonso", "Bernat", "Cristian", "Dani", "Emilio", "Guillem",
        };

        private static readonly string[] SpainStems =
        {
            "Alb", "Ang", "Arr", "Bal", "Barr", "Bel", "Ben", "Cab",
            "Cal", "Camp", "Car", "Cast", "Cor", "Cuen", "Delg", "Esc",
            "Esp", "Frag", "Gal", "Gar", "Gil", "Gom", "Hin", "Ibar",
            "Jim", "Lag", "Lar", "Led", "Lop", "Mall", "Mar", "Med",
            "Mol", "Mont", "Mor", "Nav", "Nog", "Ort", "Pal", "Par",
            "Pel", "Per", "Quin", "Ram", "Rec", "Rob", "Rom", "Sal",
            "San", "Serr", "Tor", "Val", "Vall", "Vil", "Zam", "Zur",
        };

        private static readonly string[] SpainEndings =
        {
            "era", "ero", "ado", "ano", "edo", "illo", "osa", "aza",
            "ejo", "ines", "uela", "ondo",
        };

        // ---------------------------------------------------------------- Portugal

        private static readonly string[] PortugalFirstNames =
        {
            "Bruno", "Diogo", "Eduardo", "Fabio", "Rui", "Tiago", "Vasco", "Nuno",
            "Jose", "Pedro", "Paulo", "Ricardo", "Miguel", "Andre", "Bernardo", "Carlos",
            "Daniel", "Duarte", "Fernando", "Filipe", "Francisco", "Gil", "Gustavo", "Helder",
            "Hugo", "Ivo", "Joel", "Jorge", "Leandro", "Luis", "Manuel", "Marco",
            "Mario", "Martim", "Matias", "Nelson", "Octavio", "Rafa", "Renato", "Rodrigo",
            "Salvador", "Samuel", "Sergio", "Telmo", "Tomas", "Valentim", "Vitor", "Xavier",
            "Afonso", "Alexandre", "Antonio", "Armando", "Artur", "Bento", "Caetano", "Dinis",
        };

        private static readonly string[] PortugalStems =
        {
            "Alm", "Alv", "Amar", "Bar", "Bat", "Braz", "Cald", "Cam",
            "Card", "Carv", "Cast", "Coel", "Cor", "Cost", "Cout", "Cunh",
            "Est", "Far", "Fer", "Fig", "Fons", "Fren", "Gom", "Gouv",
            "Guer", "Lem", "Lim", "Lop", "Lour", "Mac", "Mag", "Mart",
            "Mat", "Med", "Mel", "Mend", "Mir", "Mont", "Mor", "Nog",
            "Oliv", "Paiv", "Pach", "Pin", "Quint", "Ram", "Reb", "Res",
            "Rib", "Roch", "Sam", "Sant", "Serr", "Sim", "Soar", "Tav",
        };

        private static readonly string[] PortugalEndings =
        {
            "eira", "alho", "edo", "ado", "inho", "osa", "ela", "anho",
            "eiro", "ita", "oso", "ola",
        };

        private static readonly string[] PortugalSeparators = { " ", " ", " ", " ", " ", " ", " da ", " dos " };

        // ---------------------------------------------------------------- Netherlands

        private static readonly string[] NetherlandsFirstNames =
        {
            "Bram", "Daan", "Sven", "Joost", "Ruud", "Wessel", "Thijs", "Sander",
            "Niels", "Mees", "Lars", "Koen", "Jesse", "Guus", "Floris", "Bas",
            "Tim", "Stijn", "Rick", "Pim", "Nout", "Milan", "Luuk", "Kees",
            "Jurgen", "Ivo", "Hidde", "Gijs", "Frank", "Erik", "Dirk", "Cas",
            "Bart", "Arjen", "Wout", "Vincent", "Teun", "Sem", "Roel", "Quinten",
            "Peter", "Olaf", "Nick", "Maarten", "Loek", "Kars", "Jorrit", "Hugo",
            "Gerrit", "Ferdi", "Emiel", "Douwe", "Coen", "Bjorn", "Aart", "Willem",
        };

        private static readonly string[] NetherlandsStems =
        {
            "Bak", "Boer", "Bree", "Bruin", "Bos", "Brand", "Buit", "Dek",
            "Duin", "Dijk", "Doorn", "Elz", "Gron", "Haag", "Haan", "Heij",
            "Hoen", "Hoev", "Gaar", "Jans", "Rens", "Kroon", "Klaas", "Kool",
            "Kraan", "Kuip", "Lind", "Maas", "Meer", "Molen", "Mul", "Noord",
            "Oost", "Peel", "Post", "Reit", "Rijs", "Roos", "Schip", "Sloot",
            "Sluis", "Smit", "Steen", "Stel", "Stroo", "Terp", "Veen", "Velt",
            "Vier", "Vlas", "Water", "Weerd", "Wijn", "Zand", "Zuid", "Horst",
        };

        private static readonly string[] NetherlandsEndings =
        {
            "stra", "ker", "huis", "man", "veld", "berg", "dam", "hout",
            "kamp", "broek", "sma", "hoven",
        };

        private static readonly string[] NetherlandsSeparators =
        {
            " ", " ", " ", " ", " ", " van ", " van der ", " de ", " ten ",
        };

        // ---------------------------------------------------------------- Italy

        private static readonly string[] ItalyFirstNames =
        {
            "Alessandro", "Andrea", "Antonio", "Bruno", "Carlo", "Cristian", "Dario", "Davide",
            "Domenico", "Edoardo", "Emanuele", "Enrico", "Fabio", "Federico", "Filippo", "Francesco",
            "Gabriele", "Gennaro", "Giacomo", "Gianluca", "Giorgio", "Giovanni", "Giulio", "Graziano",
            "Ivan", "Jacopo", "Leonardo", "Lorenzo", "Luca", "Luigi", "Marco", "Mario",
            "Massimo", "Matteo", "Mauro", "Michele", "Mirko", "Nicola", "Paolo", "Pietro",
            "Raffaele", "Riccardo", "Roberto", "Salvatore", "Samuele", "Sergio", "Simone", "Stefano",
            "Tommaso", "Umberto", "Valerio", "Vincenzo", "Vito", "Alberto", "Claudio", "Daniele",
        };

        private static readonly string[] ItalyStems =
        {
            "Bar", "Bass", "Bell", "Ben", "Bert", "Bon", "Bracc", "Cal",
            "Camp", "Cann", "Capr", "Carr", "Cass", "Cast", "Catt", "Cerv",
            "Cian", "Col", "Corr", "Cost", "Croc", "Dall", "Fabb", "Falc",
            "Farr", "Fer", "Fior", "Frac", "Fusc", "Gall", "Gasp", "Gatt",
            "Giann", "Grass", "Grill", "Guerr", "Lamb", "Lanz", "Lomb", "Long",
            "Magg", "Mand", "Mar", "March", "Mass", "Mazz", "Mont", "Mor",
            "Nard", "Nic", "Orl", "Palm", "Pand", "Pass", "Past", "Pell",
        };

        private static readonly string[] ItalyEndings =
        {
            "elli", "etti", "ini", "ani", "oni", "aro", "asso", "ucci",
            "ello", "ino", "otti", "ari",
        };

        private static readonly string[] ItalySeparators =
        {
            " ", " ", " ", " ", " ", " ", " ", " ", " De ", " Di ",
        };

        // ---------------------------------------------------------------- Germany

        private static readonly string[] GermanyFirstNames =
        {
            "Andreas", "Bastian", "Benedikt", "Christoph", "Daniel", "Dennis", "Dominik", "Elias",
            "Fabian", "Felix", "Florian", "Frank", "Gregor", "Hannes", "Hendrik", "Jannik",
            "Jens", "Joachim", "Jonas", "Julian", "Kai", "Karl", "Klaus", "Konstantin",
            "Lars", "Lennart", "Leon", "Lukas", "Manuel", "Marcel", "Markus", "Martin",
            "Matthias", "Max", "Michael", "Moritz", "Nico", "Niklas", "Ole", "Oliver",
            "Patrick", "Philipp", "Rainer", "Ralf", "Robin", "Rolf", "Sascha", "Sebastian",
            "Simon", "Stefan", "Sven", "Thilo", "Thorsten", "Tobias", "Ulrich", "Yannick",
        };

        private static readonly string[] GermanyStems =
        {
            "Alt", "Brand", "Breit", "Buch", "Burk", "Dank", "Diet", "Drees",
            "Eber", "Ehr", "Eich", "Ell", "Engel", "Falk", "Fels", "Frisch",
            "Fuchs", "Geis", "Gerh", "Grim", "Gros", "Grun", "Haas", "Hall",
            "Har", "Heid", "Hein", "Herz", "Hes", "Hopf", "Holz", "Horn",
            "Jung", "Kell", "Kirch", "Klein", "Kraus", "Kuhn", "Lang", "Lind",
            "Lutz", "Mert", "Neu", "Nord", "Ober", "Reich", "Rein", "Rost",
            "Rot", "Sand", "Schell", "Seif", "Steig", "Stell", "Thal", "Vogel",
        };

        private static readonly string[] GermanyEndings =
        {
            "mann", "ner", "bach", "berg", "stein", "hardt", "feld", "hoff",
            "meier", "dorf", "ing", "wald",
        };

        // ---------------------------------------------------------------- The table

        // A pool without particles still needs a separator: the single blank that joins first name to
        // surname, so the generator's four-part concatenation is the same shape for every nationality.
        private static readonly string[] PlainSeparator = { " " };

        private static readonly PlayerNamePool[] Pools =
        {
            new PlayerNamePool("England", EnglandFirstNames, EnglandStems, EnglandEndings, PlainSeparator),
            new PlayerNamePool("Scotland", ScotlandFirstNames, ScotlandStems, ScotlandEndings, ScotlandSeparators),
            new PlayerNamePool("Wales", WalesFirstNames, WalesStems, WalesEndings, PlainSeparator),
            new PlayerNamePool("Ireland", IrelandFirstNames, IrelandStems, IrelandEndings, IrelandSeparators),
            new PlayerNamePool("France", FranceFirstNames, FranceStems, FranceEndings, FranceSeparators),
            new PlayerNamePool("Spain", SpainFirstNames, SpainStems, SpainEndings, PlainSeparator),
            new PlayerNamePool("Portugal", PortugalFirstNames, PortugalStems, PortugalEndings, PortugalSeparators),
            new PlayerNamePool("Netherlands", NetherlandsFirstNames, NetherlandsStems, NetherlandsEndings, NetherlandsSeparators),
            new PlayerNamePool("Italy", ItalyFirstNames, ItalyStems, ItalyEndings, ItalySeparators),
            new PlayerNamePool("Germany", GermanyFirstNames, GermanyStems, GermanyEndings, PlainSeparator),
        };

        private static readonly Dictionary<string, PlayerNamePool> ByNationality = BuildIndex(Pools);

        /// <summary>How many nationalities the world can generate.</summary>
        public static int Count => Pools.Length;

        public static PlayerNamePool At(int index) => Pools[index];

        /// <summary>
        /// Draws a nationality — uniformly, one rng value. The generator has no notion of *which*
        /// country a squad plays in, so a domestic bias would bake an English-world assumption into a
        /// multi-league generator; weighting belongs to whoever knows the league's country and can pass
        /// the nationality in itself.
        /// </summary>
        public static PlayerNamePool Draw(IRandom rng) => Pools[rng.NextInt(Pools.Length)];

        /// <summary>
        /// The pool for a nationality, or the first pool when the nationality is unknown — a save from
        /// an older world may carry a nationality this table no longer lists, and a plausible name beats
        /// a crash on a path that is not an invariant violation (CONVENTIONS §4).
        /// </summary>
        public static PlayerNamePool For(string nationality)
        {
            if (nationality != null && ByNationality.TryGetValue(nationality, out PlayerNamePool pool))
            {
                return pool;
            }

            return Pools[0];
        }

        private static Dictionary<string, PlayerNamePool> BuildIndex(PlayerNamePool[] pools)
        {
            var index = new Dictionary<string, PlayerNamePool>(pools.Length, System.StringComparer.Ordinal);
            for (int i = 0; i < pools.Length; i++)
            {
                index[pools[i].Nationality] = pools[i];
            }

            return index;
        }
    }
}
