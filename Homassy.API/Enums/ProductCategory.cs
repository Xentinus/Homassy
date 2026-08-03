namespace Homassy.API.Enums
{
    /// <summary>
    /// Canonical product category list. The numeric value is persisted in
    /// <c>Products.Category</c> and is what the frontend receives, so values must
    /// never be renumbered or reused.
    /// <para>
    /// Members are grouped into thematic numeric blocks with deliberate gaps (food
    /// 1-49, medicine 50-69, ..., bathroom 900-929, building materials 930-969,
    /// door/window hardware 970-999, garage 1000-1029). Add a new member to the
    /// block it belongs to using a free number in that range.
    /// </para>
    /// <para>
    /// The web enum and the three locale files are generated from this file: after
    /// changing it, add the member's labels to <c>LABELS</c> and run
    /// <c>npm run sync:product-category</c> in Homassy.Web.
    /// </para>
    /// </summary>
    public enum ProductCategory
    {
        // Egyéb
        Other = 0,                       // Egyéb

        // Élelmiszerek
        Grain = 1,                         // Gabonafélék (búza, rizs, kukorica, stb.)
        Bread = 2,                         // Kenyér és pékáruk
        CerealAndBreakfast = 3,           // Szemes müzli, müzli, stb.
        Pasta = 4,                        // Tészta
        Rice = 5,                         // Rizs
        Flour = 6,                        // Liszt
        Sugar = 7,                        // Cukor és édesítőszerek
        Salt = 8,                         // Só
        Spices = 9,                       // Fűszerek és fűszerkeverékek
        Oil = 10,                         // Étolaj
        Butter = 11,                      // Vaj
        Margarine = 12,                   // Margarin
        Cheese = 13,                      // Sajt
        Milk = 14,                        // Tej és tejszármazékok
        Yogurt = 15,                      // Joghurt és kefir
        Cream = 16,                       // Tejszín
        Egg = 17,                         // Tojás
        Meat = 18,                        // Friss hús
        Poultry = 19,                     // Baromfihús
        Fish = 20,                        // Hal és halak
        Seafood = 21,                     // Tengeri ételek
        Sausage = 22,                     // Kolbász és húskészítmények
        Vegetable = 23,                   // Friss zöldség
        Fruit = 24,                       // Friss gyümölcs
        BerryAndNuts = 25,                // Bogyók és diófélék
        Legume = 26,                      // Hüvelyesek
        Canned = 27,                      // Konzervek
        FrozenFood = 28,                  // Fagyasztott ételek
        Jam = 29,                         // Lekvár, méz, szirupok
        Peanut = 30,                      // Mogyoró és mogyoróvaj
        Chocolate = 31,                   // Csoki és kakaó
        Candy = 32,                       // Cukorka és édesség
        Snack = 33,                       // Snackek és chips
        Cookie = 34,                      // Sütik és péksütemények
        Beverage = 35,                    // Alkoholmentes italok
        Coffee = 36,                      // Kávé
        Tea = 37,                         // Tea
        AlcoholicBeverage = 38,           // Alkoholos italok
        Wine = 39,                        // Bor
        Beer = 40,                        // Sör
        Sauce = 41,                       // Szósz és mártás
        Condiment = 42,                   // Fűszerpaszta és füszersaláta
        Seasoning = 43,                   // Kevert fűszerek
        VitaminAndSupplement = 44,        // Vitaminok és étrend-kiegészítők

        // Gyógyszerek és egészségi termékek
        BakingIngredient = 45,            // Sütési alapanyag
        ReadyMeal = 46,                   // Készétel
        SoupAndBroth = 47,                // Leves és alaplé
        DriedFood = 48,                   // Szárított élelmiszer
        Pickles = 49,                     // Savanyúság
        Medicine = 50,                    // Gyógyszer
        PainRelief = 51,                  // Fájdalomcsillapító
        Antibiotic = 52,                  // Antibiotikum
        AntiInflammatory = 53,            // Gyulladáscsökkentő
        ColdAndFlu = 54,                  // Megfázás és influenza elleni
        Allergy = 55,                     // Allergia elleni
        Digestive = 56,                   // Emésztési problémák
        Cough = 57,                       // Köhögés elleni
        Vitamin = 58,                     // Vitaminkészítmények
        Supplement = 59,                  // Étrend-kiegészítő
        Probiotic = 60,                   // Probiotikus termékek
        FirstAid = 61,                    // Első segély termékek
        Bandage = 62,                     // Kötszerek
        Plaster = 63,                     // Sebtapasz
        Cotton = 64,                      // Pamut és hygienia termékek
        Gauze = 65,                       // Gáz és kötöző
        Ointment = 66,                    // Krém és kenőcs
        Syrup = 67,                       // Szirup és folyadék
        Tablet = 68,                      // Tabletta és kapszula
        Injection = 69,                   // Injekció

        // Személyi higiénia és szépségápolás
        Soap = 70,                        // Szappan
        Shampoo = 71,                     // Sampon
        Conditioner = 72,                 // Balzsam
        BodyWash = 73,                    // Tusfürdő
        ToothPaste = 74,                  // Fogkrém
        ToothBrush = 75,                  // Fogkefe
        Deodorant = 76,                   // Dezodor
        Perfume = 77,                     // Parfüm és eau de toilette
        ToiletPaper = 78,                 // WC papír
        FacialCare = 79,                  // Arcápolás
        SkinCare = 80,                    // Bőrápolás
        Moisturizer = 81,                 // Hidratáló krém
        Sunscreen = 82,                   // Napozási krém
        MakeUp = 83,                      // Smink
        MentalCare = 84,                  // Férfi ápolási termékek
        WomensCare = 85,                  // Női higiéniai termékek
        HairColor = 86,                   // Hajfesték
        Razor = 87,                       // Borotva és szőrtelenítés

        // Tisztítószerek és házartási termékek
        HairStyling = 88,                 // Hajformázó
        NailCare = 89,                    // Kézápolás és köröm
        Detergent = 90,                   // Mosószer
        SoftenerAndRinse = 91,            // Öblítő és általános tisztítószer
        Bleach = 92,                      // Fehérítő
        DishSoap = 93,                    // Edénymosó
        WindowCleaner = 94,               // Ablaktisztító
        Disinfectant = 95,                // Fertőtlenítőszer
        AirFreshener = 96,                // Levegőfrissítő
        Candle = 97,                      // Illatosító gyertya
        Deodorant_Home = 98,              // Szobadeodor
        Laundry = 99,                     // Mosás segédeszközök
        TrashBag = 100,                   // Szemetes zacskó
        AluminumFoil = 101,               // Fólia
        PlasticWrap = 102,                // Cling film
        FoodStorage = 103,                // Ételtárolás
        PaperTowel = 104,                 // Papírtörlő

        // Háztartási eszközök és felszerelések
        FloorCleaner = 105,               // Padlótisztító
        BathroomCleaner = 106,            // Fürdőszobai tisztítószer
        OvenCleaner = 107,                // Sütőtisztító
        DrainCleaner = 108,               // Lefolyótisztító
        Descaler = 109,                   // Vízkőoldó
        Kitchen = 110,                    // Konyhai eszközök
        Cookware = 111,                   // Edény és főzőedény
        Cutlery = 112,                    // Evőeszköz és villa
        Dinnerware = 113,                 // Tányér és csésze
        Glass = 114,                      // Pohár
        Utensil = 115,                    // Konyhai segédeszköz
        Appliance = 116,                  // Konyhai gépek
        Bakeware = 117,                   // Sütőforma
        CuttingBoard = 118,               // Vágódeszka
        KitchenScale = 119,               // Konyhai mérleg
        Furniture = 120,                  // Bútor
        Lighting = 121,                   // Világítás
        Bedding = 122,                    // Ágyneműk
        Pillow = 123,                     // Párna
        Blanket = 124,                    // Takaró
        Sheet = 125,                      // Lepedő
        Towel = 126,                      // Törölköző
        Curtain = 127,                    // Függöny
        Rug = 128,                        // Szőnyeg
        Storage = 129,                    // Tárolás és szekrény
        Shelf = 130,                      // Polc
        Decoration = 131,                 // Dekoráció
        Mirror = 132,                     // Tükör
        Artwork = 133,                    // Képek és falidekoráció
        Plant = 134,                      // Házinövények

        // Szerszámok és műhelygépek
        Vase = 135,                       // Váza
        PictureFrame = 136,               // Képkeret
        Clock = 137,                      // Óra
        Doormat = 138,                    // Lábtörlő
        LaundryBasket = 139,              // Szennyestartó
        Tool = 140,                       // Általános szerszám
        Hammer = 141,                     // Kalapács
        Screwdriver = 142,                // Csavarhúzó
        Wrench = 143,                     // Csavarkulcs
        Saw = 144,                        // Fűrész
        Drill = 145,                      // Fúró
        Pliers = 146,                     // Fogó
        Measuring = 147,                  // Mérő szerszám
        LevelAndSquare = 148,             // Szintezési és négyzetes szerszám
        Clamp = 149,                      // Szorítóeszköz
        ViseAndJig = 150,                 // Satu és fix szerszám
        PowerTool = 151,                  // Elektromos szerszám
        Chisel = 152,                     // Véső
        FileTool = 153,                   // Reszelő
        UtilityKnife = 154,               // Sniccer
        StudFinder = 155,                 // Falszkenner
        HeatGun = 156,                    // Hőlégfúvó
        Multimeter = 157,                 // Multiméter
        DrillBit = 158,                   // Fúrószár
        SawBlade = 159,                   // Fűrészlap
        Garden = 160,                     // Kertészeti szerszámok
        Shovel = 161,                     // Lapát
        Rake = 162,                       // Ról
        Hoe = 163,                        // Kapá
        Pruning = 164,                    // Metsző szerszám
        Watering = 165,                   // Öntözés

        // Elektromos termékek és alkatrészek
        GardenGloves = 166,               // Kertészkesztyű
        Spade = 167,                      // Ásó
        WateringCan = 168,                // Öntözőkanna
        GardenTwine = 169,                // Kertészzsineg
        Electronics = 170,                // Elektronika
        Battery = 171,                    // Akkumátor
        Charger = 172,                    // Töltő
        Cable = 173,                      // Kábel
        Adapter = 174,                    // Adapter
        Bulb = 175,                       // Izzó
        ExtensionCord = 176,              // Hosszabbítókábel
        PowerStrip = 177,                 // Elosztó

        // Autós és közlekedési termékek
        Headphones = 178,                 // Fejhallgató
        Speaker = 179,                    // Hangszóró
        Automotive = 180,                 // Autóápolás
        EngineOil = 181,                  // Motorolaj
        Coolant = 182,                    // Hűtőfolyadék
        BrakeFl = 183,                    // Fékolaj
        Wiper = 184,                      // Utasszélvédő lemez
        TireCare = 185,                   // Gumiabroncs ápolás
        CarWash = 186,                    // Autómosó
        Wax = 187,                        // Viasz

        // Ruházat és lábbelik
        WasherFluid = 188,                // Szélvédőmosó folyadék
        CarAirFreshener = 189,            // Autóillatosító
        Clothing = 190,                   // Ruha
        Shirt = 191,                      // Ing
        Pants = 192,                      // Nadrág
        Jacket = 193,                     // Kabát
        Sweater = 194,                    // Pulóver
        Dress = 195,                      // Ruha
        Underwear = 196,                  // Fehérnemű
        Socks = 197,                      // Zokni
        Shoes = 198,                      // Cipő
        Hat = 199,                        // Kalap
        Scarf = 200,                      // Sál
        Gloves = 201,                     // Kesztyű
        Belt = 202,                       // Öv
        Accessories = 203,                // Kiegészítők

        // Szórakoztatás és hobbi
        Suit = 204,                       // Öltöny
        Skirt = 205,                      // Szoknya
        Shorts = 206,                     // Rövidnadrág
        Swimwear = 207,                   // Fürdőruha
        Sportswear = 208,                 // Sportruha
        Bag = 209,                        // Táska
        Entertainment = 210,              // Szórakoztatás
        Book = 211,                       // Könyv
        Magazine = 212,                   // Folyóirat
        Game = 213,                       // Játék
        Sport = 214,                      // Sporteszköz
        Hobby = 215,                      // Hobbi anyagok
        Art = 216,                        // Művészeti kellékek
        Music = 217,                      // Zene és hangrendszer
        Toy = 218,                        // Játékszer gyerekeknek

        // Háziállat termékek
        MusicalInstrument = 219,          // Hangszer
        PetSupply = 220,                  // Háziállat termékek
        PetFood = 221,                    // Háziállat táp
        PetCare = 222,                    // Háziállat ápolás
        PetToy = 223,                     // Háziállat játékszer
        PetBed = 224,                     // Háziállat ágy és házzal

        // Iroda és papíráruk
        PetLeash = 225,                   // Póráz és nyakörv
        PetLitter = 226,                  // Alom
        PetBowl = 227,                    // Etetőtál
        PetCarrier = 228,                 // Szállítóbox
        AquariumSupply = 229,             // Akváriumkellék
        OfficeSupply = 230,               // Irodai kellékek általánosan
        Paper = 231,                      // Nyomtatópapír, jegyzetlap
        Notebook = 232,                   // Füzet, jegyzetfüzet
        Pen = 233,                        // Toll
        Pencil = 234,                     // Ceruza
        Marker = 235,                     // Filctoll, kiemelő
        Eraser = 236,                     // Radír
        Tape = 237,                       // Ragasztószalag
        Glue = 238,                       // Ragasztó
        Stapler = 239,                    // Tűzőgép és kapcsok
        Scissors = 240,                   // Olló
        Envelope = 241,                   // Boríték
        PrinterInk = 242,                 // Nyomtató tinta
        PrinterToner = 243,               // Nyomtató toner

        // Baba és gyerek termékek
        Folder = 244,                     // Iratgyűjtő
        Binder = 245,                     // Gyűrűs mappa
        StickyNote = 246,                 // Öntapadó jegyzetlap
        Calculator = 247,                 // Számológép
        Ruler = 248,                      // Vonalzó
        LabelMaker = 249,                 // Feliratozó
        BabyDiaper = 250,                 // Pelenka
        BabyWipes = 251,                  // Baba törlőkendő
        BabyFormula = 252,                // Baba tápszer
        BabyFood = 253,                   // Bébiétel
        BabyCare = 254,                   // Baba ápolás
        Nursing = 255,                    // Szoptatási kellékek
        Stroller = 256,                   // Babakocsi
        CarSeat = 257,                    // Gyerek autósülés

        // Tisztító eszközök
        BabyMonitor = 258,                // Babaőrző
        BabyBottle = 259,                 // Cumisüveg
        Pacifier = 260,                   // Cumi
        BabyBathTub = 261,                // Babakád
        HighChair = 262,                  // Etetőszék
        BabyCarrier = 263,                // Babahordozó
        Crib = 264,                       // Kiságy
        ChangingMat = 265,                // Pelenkázó lap
        BabyToy = 266,                    // Babajáték
        BabyClothing = 267,               // Babaruha
        Potty = 268,                      // Bili
        SafetyGate = 269,                 // Biztonsági rács
        Broom = 270,                      // Seprű
        Mop = 271,                        // Felmosó
        Bucket = 272,                     // Vödör
        Dustpan = 273,                    // Lapát szeméthez
        Squeegee = 274,                   // Lehúzó
        Sponge = 275,                     // Szivacs
        Brush = 276,                      // Kefe
        Scrubber = 277,                   // Súroló
        Vacuum = 278,                     // Porszívó
        VacuumBag = 279,                  // Porszívózsák

        // Kártevő elleni védelem
        Duster = 280,                     // Portörlő
        MicrofiberCloth = 281,            // Mikroszálas kendő
        CleaningGloves = 282,             // Gumikesztyű
        ToiletBrush = 283,                // WC-kefe
        Plunger = 284,                    // Duguláselhárító pumpa
        CarpetCleaner = 285,              // Szőnyegtisztító
        SteamCleaner = 286,               // Gőztisztító
        LintRoller = 287,                 // Szöszroller
        Clothesline = 288,                // Szárítókötél
        ClothesPeg = 289,                 // Ruhacsipesz
        InsectRepellent = 290,            // Rovarriasztó
        InsectTrap = 291,                 // Rovarcsapda
        RodentTrap = 292,                 // Rágcsáló csapda
        Pesticide = 293,                  // Növényvédő szer

        // Okosotthon és biztonság
        MothRepellent = 294,              // Molyirtó
        AntBait = 295,                    // Hangyacsalétek
        WaspTrap = 296,                   // Darázscsapda
        MoleRepellent = 297,              // Vakondriasztó
        BirdRepellent = 298,              // Madárriasztó
        SlugPellet = 299,                 // Csigaölő granulátum
        SmartHome = 300,                  // Okosotthon eszközök
        SmartBulb = 301,                  // Okos izzó
        SmartPlug = 302,                  // Okos dugalj
        DoorLock = 303,                   // Okos zár
        SmokeDetector = 304,              // Füstérzékelő
        CarbonMonoxideDetector = 305,     // Szén-monoxid érzékelő
        Alarm = 306,                      // Riasztó
        Camera = 307,                     // Biztonsági kamera

        // Levegő és víz kezelése
        SmartDoorbell = 308,              // Okos ajtócsengő
        MotionSensor = 309,               // Mozgásérzékelő
        OpeningSensor = 310,              // Nyitásérzékelő
        WaterLeakSensor = 311,            // Vízszivárgás-érzékelő
        SmartRadiatorValve = 312,         // Okos radiátorszelep
        SmartHub = 313,                   // Okos központ
        SmartSpeaker = 314,               // Okos hangszóró
        SmartSwitch = 315,                // Okos kapcsoló
        SmartBlind = 316,                 // Okos árnyékoló
        SmartIrrigationController = 317,  // Okos öntözésvezérlő
        SmartScale = 318,                 // Okos mérleg
        SafeBox = 319,                    // Széf
        AirPurifier = 320,                // Légtisztító
        Humidifier = 321,                 // Párásító
        Dehumidifier = 322,               // Párátlanító
        Fan = 323,                        // Ventilátor
        Heater = 324,                     // Hősugárzó
        WaterFilter = 325,                // Vízszűrő

        // Kis háztartási gépek
        WaterSoftener = 326,              // Vízlágyító
        AirFilter = 327,                  // Légszűrő
        AromaDiffuser = 328,              // Aroma diffúzor
        Hygrometer = 329,                 // Páratartalom-mérő
        AirQualityMonitor = 330,          // Levegőminőség-mérő
        WaterTestKit = 331,               // Vízteszt készlet
        CoffeeMaker = 340,                // Kávéfőző
        Kettle = 341,                     // Vízforraló
        Toaster = 342,                    // Kenyérpirító
        Blender = 343,                    // Turmixgép
        Mixer = 344,                      // Konyhai robot, mixer
        FoodProcessor = 345,              // Aprító
        SlowCooker = 346,                 // Lassúfőző
        RiceCooker = 347,                 // Rizsfőző

        // Nagy háztartási gépek
        AirFryer = 348,                   // Forrólevegős fritőz
        DeepFryer = 349,                  // Olajsütő
        SandwichMaker = 350,              // Szendvicssütő
        WaffleMaker = 351,                // Gofrisütő
        EggCooker = 352,                  // Tojásfőző
        ElectricGrill = 353,              // Elektromos grill
        Juicer = 354,                     // Gyümölcscentrifuga
        CoffeeGrinder = 355,              // Kávédaráló
        MilkFrother = 356,                // Habosító
        SodaMaker = 357,                  // Szódagép
        BreadMaker = 358,                 // Kenyérsütő gép
        IceCreamMaker = 359,              // Jégkrémkészítő
        Refrigerator = 360,               // Hűtőszekrény
        Freezer = 361,                    // Fagyasztó
        Oven = 362,                       // Sütő
        Cooktop = 363,                    // Főzőlap
        Microwave = 364,                  // Mikrohullámú sütő
        Dishwasher = 365,                 // Mosogatógép
        WashingMachine = 366,             // Mosógép
        Dryer = 367,                      // Szárítógép

        // Számítástechnika és mobil
        WasherDryer = 368,                // Mosó-szárítógép
        Stove = 369,                      // Tűzhely
        RangeHood = 370,                  // Páraelszívó
        WineCooler = 371,                 // Borhűtő
        WaterHeater = 372,                // Vízmelegítő
        Boiler = 373,                     // Kazán
        HeatPump = 374,                   // Hőpumpa
        SolarPanel = 375,                 // Napelem
        Generator = 376,                  // Áramfejlesztő
        SumpPump = 377,                   // Búvárszivattyú
        SepticTreatment = 378,            // Szennyvízkezelő adalék
        SoftenerSalt = 379,               // Regeneráló só
        ComputerAccessory = 380,          // Számítógép kiegészítő
        Keyboard = 381,                   // Billentyűzet
        Mouse = 382,                      // Egér
        Monitor = 383,                    // Monitor
        USBDrive = 384,                   // USB meghajtó
        ExternalHDD = 385,                // Külső merevlemez
        PhoneAccessory = 386,             // Telefon kiegészítők
        ScreenProtector = 387,            // Kijelzővédő
        PhoneCase = 388,                  // Telefontok
        ChargerCable = 389,               // Töltőkábel

        // Fotó és média
        Laptop = 390,                     // Laptop
        TabletDevice = 391,               // Táblagép
        DesktopComputer = 392,            // Asztali számítógép
        Printer = 393,                    // Nyomtató
        Scanner = 394,                    // Lapolvasó
        Router = 395,                     // Router
        NetworkSwitch = 396,              // Hálózati switch
        NetworkStorage = 397,             // Hálózati tároló
        UPSDevice = 398,                  // Szünetmentes tápegység
        Webcam = 399,                     // Webkamera
        CameraLens = 400,                 // Kamera objektív
        Tripod = 401,                     // Állvány
        MemoryCard = 402,                 // Memóriakártya
        ActionCamera = 403,               // Sportkamera

        // Parti és szezonális
        CameraBag = 404,                  // Fotós táska
        CameraBattery = 405,              // Kamera akkumulátor
        StudioLighting = 406,             // Fotós világítás
        Drone = 407,                      // Drón
        Gimbal = 408,                     // Gimbal
        LensFilter = 409,                 // Objektívszűrő
        PartySupply = 440,                // Parti kellékek
        BirthdaySupply = 441,             // Szülinapi kellékek
        SeasonalDecoration = 442,         // Szezonális dekoráció
        ChristmasDecoration = 443,        // Karácsonyi dekoráció
        HalloweenDecoration = 444,        // Halloween dekoráció

        // Utazás és poggyász
        Balloon = 445,                    // Ballon
        GiftWrap = 446,                   // Csomagolópapír
        GreetingCard = 447,               // Üdvözlőkártya
        DisposableTableware = 448,        // Egyszer használatos étkészlet
        Fireworks = 449,                  // Tűzijáték
        Luggage = 460,                    // Bőrönd
        TravelAccessory = 461,            // Utazási kiegészítők
        TravelBottle = 462,               // Utazó palackok
        PassportHolder = 463,             // Útlevél tok

        // Biztonság és vészhelyzeti
        Backpack = 464,                   // Hátizsák
        TravelPillow = 465,               // Utazópárna
        PackingCube = 466,                // Bőröndrendező
        TravelAdapter = 467,              // Utazó adapter
        LuggageScale = 468,               // Bőröndmérleg
        TravelLock = 469,                 // Bőröndzár
        Flashlight = 480,                 // Zseblámpa
        Headlamp = 481,                   // Fejlámpa
        Matches = 482,                    // Gyufa
        Lighter = 483,                    // Öngyújtó
        FireExtinguisher = 484,           // Tűzoltó készülék
        FirstAidKit = 485,                // Elsősegély készlet
        PPEMask = 486,                    // Védőmaszk
        SafetyGlasses = 487,              // Védőszemüveg
        WorkGloves = 488,                 // Munkavédelmi kesztyű

        // Egészség eszközök
        EarProtection = 489,              // Fülvédő
        HardHat = 490,                    // Védősisak
        SafetyVest = 491,                 // Láthatósági mellény
        Respirator = 492,                 // Légzésvédő maszk
        KneePad = 493,                    // Térdvédő
        FallProtection = 494,             // Zuhanásgátló
        EmergencyRadio = 495,             // Vészhelyzeti rádió
        EmergencyBlanket = 496,           // Hőtakaró
        WaterPurificationTablet = 497,    // Víztisztító tabletta
        EmergencyFood = 498,              // Tartalék élelmiszer
        FireBlanket = 499,                // Tűzoltó takaró
        Thermometer = 500,                // Lázmérő
        BloodPressureMonitor = 501,       // Vérnyomásmérő
        Glucometer = 502,                 // Vércukormérő
        PulseOximeter = 503,              // Pulzoximéter
        HeatingPad = 504,                 // Melegítő párna
        Massager = 505,                   // Masszírozó
        BraceSupport = 506,               // Támasztók, merevítők
        MobilityAid = 507,                // Járássegítő, kerekesszék

        // Szexuális jólét
        Nebulizer = 508,                  // Inhalátor
        Crutch = 509,                     // Mankó
        Walker = 510,                     // Járókeret
        Wheelchair = 511,                 // Kerekesszék
        HospitalBed = 512,                // Betegágy
        PillOrganizer = 513,              // Tablettatartó
        MedicalGloves = 514,              // Vizsgálókesztyű
        Syringe = 515,                    // Fecskendő
        IcePack = 516,                    // Hűtőtasak
        CompressionStocking = 517,        // Kompressziós harisnya
        BodyScale = 518,                  // Személymérleg
        SharpsContainer = 519,            // Éles hulladék gyűjtő
        SexualWellness = 520,             // Szexuális egészség
        Condom = 521,                     // Óvszer
        Lubricant = 522,                  // Síkosító

        // Látás és hallás
        IntimateWash = 523,               // Intim mosakodó
        PregnancyTest = 524,              // Terhességi teszt
        Contraceptive = 525,              // Fogamzásgátló
        VisionCare = 530,                 // Látásápolás
        EyeglassCleaner = 531,            // Szemüvegtisztító
        ContactLens = 532,                // Kontaktlencse
        HearingAid = 533,                 // Hallókészülék

        // Lábápolás és alátétek
        Eyeglasses = 534,                 // Szemüveg
        Sunglasses = 535,                 // Napszemüveg
        LensSolution = 536,               // Kontaktlencse-folyadék
        EyeDrops = 537,                   // Szemcsepp
        HearingAidBattery = 538,          // Hallókészülék-elem
        EarPlugs = 539,                   // Füldugó
        FootCare = 540,                   // Lábápolás
        Insoles = 541,                    // Talpbetét

        // Varrás és kézműves
        FootFile = 542,                   // Lábreszelő
        ShoePolish = 543,                 // Cipőkrém
        HeelPad = 544,                    // Sarokbetét
        ShoeLace = 545,                   // Cipőfűző
        ShoeBrush = 546,                  // Cipőkefe
        ShoeTree = 547,                   // Sámfa
        CompressionSock = 548,            // Kompressziós zokni
        AntifungalTreatment = 549,        // Gombaellenes készítmény
        SewingKit = 550,                  // Varrókészlet
        YarnAndKnitting = 551,            // Fonál és kötés
        CraftSupply = 552,                // Kézműves kellékek
        Fabric = 553,                     // Anyagok

        // Festés és dekorálás
        SewingMachine = 554,              // Varrógép
        Needle = 555,                     // Tű
        Thread = 556,                     // Cérna
        Button = 557,                     // Gomb
        Zipper = 558,                     // Cipzár
        Ribbon = 559,                     // Díszszalag
        Bead = 560,                       // Gyöngy
        Felt = 561,                       // Filclap
        ArtBrush = 562,                   // Művészecset
        Canvas = 563,                     // Festővászon
        ModelingClay = 564,               // Gyurma
        Sketchbook = 565,                 // Rajzfüzet
        CraftGlue = 566,                  // Kézműves ragasztó
        CraftKnife = 567,                 // Makettvágó
        CuttingMat = 568,                 // Vágóalátét
        CraftPaint = 569,                 // Hobbifesték
        Paint = 570,                      // Festék
        Primer = 571,                     // Alapozó
        BrushRoller = 572,                // Ecset és henger
        PainterTape = 573,                // Maszkoló szalag

        // Vízvezeték és elektromos
        Wallpaper = 574,                  // Tapéta
        WallpaperPaste = 575,             // Tapétaragasztó
        Varnish = 576,                    // Lakk
        WoodStain = 577,                  // Pácolóanyag
        WallFiller = 578,                 // Glett
        Putty = 579,                      // Kitt
        CaulkGun = 580,                   // Kinyomópisztoly
        PaintTray = 581,                  // Festőtálca
        DropCloth = 582,                  // Takarófólia
        SprayPaint = 583,                 // Festékspray
        PaintThinner = 584,               // Hígító
        Sander = 585,                     // Csiszológép
        SandingBlock = 586,               // Csiszolótalp
        PaintScraper = 587,               // Festéklehúzó
        WireBrush = 588,                  // Drótkefe
        Trowel = 589,                     // Simítókanál
        Plumbing = 590,                   // Vízvezeték szerelvények
        PipeFitting = 591,                // Idomok
        Faucet = 592,                     // Csaptelep
        ShowerHead = 593,                 // Zuhanyfej
        PipeInsulation = 594,             // Csőszigetelés
        PlumbersTape = 595,               // Teflonszalag
        Siphon = 596,                     // Szifon
        ToiletCistern = 597,              // WC-tartály
        ShutoffValve = 598,               // Elzárószelep
        FlexibleConnector = 599,          // Bekötőcső
        Electrical = 600,                 // Elektromos szerelvények
        Switch = 601,                     // Kapcsoló
        Outlet = 602,                     // Konnektor
        Fuse = 603,                       // Biztosíték
        Wire = 604,                       // Vezeték

        // Fűtés és hűtés
        JunctionBox = 605,                // Kötődoboz
        CircuitBreaker = 606,             // Kismegszakító
        WireConnector = 607,              // Sorkapocs
        CableConduit = 608,               // Védőcső
        VoltageTester = 609,              // Fázisceruza
        AirConditioner = 610,             // Légkondicionáló
        Radiator = 611,                   // Radiátor
        Thermostat = 612,                 // Szobatermosztát

        // Kültér és terasz
        AirVent = 613,                    // Szellőzőrács
        AirDuct = 614,                    // Légcsatorna
        ChimneyPart = 615,                // Kéménytartozék
        StoveFan = 616,                   // Kandallóventilátor
        RadiatorValve = 617,              // Radiátorszelep
        CondensatePump = 618,             // Kondenzvíz-pumpa
        UnderfloorHeating = 619,          // Padlófűtés elem
        Insulation = 620,                 // Szigetelőanyag
        WeatherStripping = 621,           // Ajtószigetelés
        WindowFilm = 622,                 // Ablakfólia
        Blinds = 623,                     // Reluxa
        MosquitoNet = 624,                // Szúnyogháló
        AtticLadder = 625,                // Padlásfeljáró
        Firewood = 626,                   // Tűzifa
        WoodPellet = 627,                 // Pellet
        Coal = 628,                       // Szén
        GasCylinder = 629,                // Gázpalack
        OutdoorFurniture = 630,           // Kerti bútor
        PatioUmbrella = 631,              // Napernyő
        GardenDecor = 632,                // Kerti dekoráció
        BirdFeeder = 633,                 // Madáretető

        // Grillezés és üzemanyag
        Hammock = 634,                    // Függőágy
        Swing = 635,                      // Hinta
        Sandbox = 636,                    // Homokozó
        Playhouse = 637,                  // Játszóház
        Trampoline = 638,                 // Trambulin
        SwimmingPool = 639,               // Medence
        PoolChemical = 640,               // Medence vegyszer
        PoolPump = 641,                   // Medenceszivattyú
        PoolCover = 642,                  // Medencetakaró
        PoolToy = 643,                    // Medencejáték
        OutdoorLighting = 644,            // Kültéri világítás
        SolarLight = 645,                 // Napelemes lámpa
        FirePit = 646,                    // Tűzrakóhely
        PatioHeater = 647,                // Kültéri hősugárzó
        GardenStatue = 648,               // Kerti szobor
        Fountain = 649,                   // Szökőkút
        Grill = 650,                      // Grill
        GrillAccessory = 651,             // Grill kiegészítők
        Charcoal = 652,                   // Faszen
        Propane = 653,                    // Propán palack

        // Kert és pázsit bővítés
        GrillBrush = 654,                 // Grillkefe
        GrillCover = 655,                 // Grilltakaró
        Skewer = 656,                     // Nyárs
        GrillThermometer = 657,           // Grill hőmérő
        SmokingChips = 658,               // Füstölőforgács
        FireStarter = 659,                // Tűzgyújtó
        GrillGrate = 660,                 // Grillrács
        Rotisserie = 661,                 // Forgónyárs
        Smoker = 662,                     // Füstölő
        PizzaOven = 663,                  // Kemence
        GrillTongs = 664,                 // Grillfogó
        BastingBrush = 665,               // Kenőecset
        GrillBasket = 666,                // Grillkosár
        GrillMat = 667,                   // Grillszőnyeg
        CharcoalChimney = 668,            // Faszéngyújtó kémény
        PropaneRegulator = 669,           // Gázreduktor
        Seeds = 670,                      // Magok
        Soil = 671,                       // Föld, virágföld
        Fertilizer = 672,                 // Műtrágya
        PlanterPot = 673,                 // Cserép
        Hose = 674,                       // Locsolótömlő
        Sprinkler = 675,                  // Permetező, szórófej
        LawnMower = 676,                  // Fűnyíró
        Trimmer = 677,                    // Szegélynyíró

        // Kemping és szabadidő
        PlantBulb = 678,                  // Virághagyma
        Seedling = 679,                   // Palánta
        SeedTray = 680,                   // Magvető tálca
        GrowLight = 681,                  // Növénylámpa
        Greenhouse = 682,                 // Üvegház
        ColdFrame = 683,                  // Melegágy
        PlantLabel = 684,                 // Növénycímke
        PruningSealant = 685,             // Sebkezelő
        GraftingSupply = 686,             // Szemzőkellék
        SoilTestKit = 687,                // Talajteszt
        SoilConditioner = 688,            // Talajjavító
        Perlite = 689,                    // Perlit
        Peat = 690,                       // Tőzeg
        BarkMulch = 691,                  // Kéregmulcs
        DecorativeGravel = 692,           // Díszkavics
        PavingStone = 693,                // Járdalap
        GardenBorder = 694,               // Ágyásszegély
        RaisedBed = 695,                  // Emelt ágyás
        CompostAccelerator = 696,         // Komposztgyorsító
        GardenSieve = 697,                // Kertészszita
        Dibber = 698,                     // Ültetőfa
        HarvestBasket = 699,              // Szedőkosár
        Tent = 700,                       // Sátor
        SleepingBag = 701,                // Hálózsák
        CampingMat = 702,                 // Polifoam, matrac
        CampingCookware = 703,            // Kemping főzőeszköz
        Cooler = 704,                     // Hűtőláda

        // Sport és fitnesz
        CampingChair = 705,               // Kempingszék
        CampingTable = 706,               // Kempingasztal
        CampingStove = 707,               // Kempingfőző
        CampingLantern = 708,             // Kempinglámpa
        GasCartridge = 709,               // Gázpatron
        WaterCanister = 710,              // Víztartály
        Tarp = 711,                       // Ponyva
        CampingAxe = 712,                 // Balta
        PocketKnife = 713,                // Bicska
        Compass = 714,                    // Kompasz
        GPSDevice = 715,                  // GPS készülék
        TrekkingPole = 716,               // Trekking bot
        CampingShower = 717,              // Kempingzuhany
        PortableToilet = 718,             // Mobil WC
        TentRepairKit = 719,              // Sátorjavító készlet
        FitnessEquipment = 720,           // Fitnesz eszközök
        YogaMat = 721,                    // Jóga szőnyeg
        Dumbbell = 722,                   // Súlyzó
        ResistanceBand = 723,             // Gumiszalag
        Treadmill = 724,                  // Futópad
        ExerciseBike = 725,               // Szobakerékpár
        RowingMachine = 726,              // Evezőgép
        EllipticalTrainer = 727,          // Elliptikus tréner
        WeightBench = 728,                // Súlyzópad
        Barbell = 729,                    // Rúdsúlyzó
        Kettlebell = 730,                 // Kettlebell
        JumpRope = 731,                   // Ugrálókötél
        PullUpBar = 732,                  // Húzódzkodó rúd
        FoamRoller = 733,                 // SMR henger
        ExerciseBall = 734,               // Fitneszlabda
        FitnessTracker = 735,             // Aktivitásmérő
        SportsDrink = 736,                // Sportital
        ProteinSupplement = 737,          // Fehérjepor
        SportsTape = 738,                 // Sporttapasz
        BoxingGlove = 739,                // Boxkesztyű
        Racket = 740,                     // Ütő
        Ball = 741,                       // Labda
        SkiEquipment = 742,               // Sífelszerelés
        Snowboard = 743,                  // Snowboard
        IceSkates = 744,                  // Korcsolya
        Skateboard = 745,                 // Gördeszka
        SwimGear = 746,                   // Úszófelszerelés
        FishingGear = 747,                // Horgászfelszerelés
        HuntingGear = 748,                // Vadászfelszerelés
        ClimbingGear = 749,               // Hegymászó felszerelés
        BicycleAccessory = 750,           // Kerékpár kiegészítő
        Helmet = 751,                     // Bukósisak
        Pump = 752,                       // Pumpa

        // Társasjáték és kirakó
        BicycleTire = 753,                // Kerékpárgumi
        BicycleTube = 754,                // Belső gumi
        BicycleChain = 755,               // Kerékpárlánc
        BicycleBrake = 756,               // Kerékpárfék
        BicycleLight = 757,               // Kerékpárlámpa
        BicycleLock = 758,                // Kerékpárzár
        BicycleRack = 759,                // Kerékpártartó
        BicycleSaddle = 760,              // Kerékpárülés
        BottleCage = 761,                 // Kulacstartó
        BicyclePannier = 762,             // Kerékpártáska
        BicycleComputer = 763,            // Kerékpáros computer
        BicycleRepairKit = 764,           // Kerékpárjavító készlet
        BicycleGrease = 765,              // Kerékpárzsír
        ChildBikeSeat = 766,              // Kerékpáros gyerekülés
        BikeTrailer = 767,                // Kerékpár-utánfutó
        EBikeBattery = 768,               // E-bike akkumulátor
        Scooter = 769,                    // Roller
        BoardGame = 800,                  // Társasjáték
        Puzzle = 801,                     // Kirakó

        // Iskolai felszerelés
        CardGame = 802,                   // Kártyajáték
        VideoGame = 803,                  // Videojáték
        GameConsole = 804,                // Játékkonzol
        GameController = 805,             // Kontroller
        BuildingBlocks = 806,             // Építőkocka
        Doll = 807,                       // Baba
        RideOnToy = 808,                  // Járgány
        OutdoorToy = 809,                 // Kültéri játék
        SchoolBag = 810,                  // Iskolatáska
        LunchBox = 811,                   // Uzsonnás doboz
        WaterBottle = 812,                // Kulacs

        // Kert bővített
        PencilCase = 813,                 // Tolltartó
        Textbook = 814,                   // Tankönyv
        GeometrySet = 815,                // Körző és vonalzó készlet
        Watercolor = 816,                 // Vízfesték
        Crayon = 817,                     // Zsírkréta
        Chalk = 818,                      // Kréta
        Globe = 819,                      // Földgömb
        GardenEdger = 820,                // Szegélyvágó
        WeedControl = 821,                // Gyomirtás kellékek
        WeedKiller = 822,                 // Gyomirtó szer
        Mulch = 823,                      // Mulcs
        Compost = 824,                    // Komposzt
        CompostBin = 825,                 // Komposztáló
        RainBarrel = 826,                 // Esővízgyűjtő hordó
        IrrigationTimer = 827,            // Öntözés időzítő
        HoseNozzle = 828,                 // Tömlőfej / pisztoly
        GardenSprayer = 829,              // Permetező
        Wheelbarrow = 830,                // Talicska
        GardenCart = 831,                 // Kerti kocsi
        Trellis = 832,                    // Rács, futónövény tartó
        PlantStake = 833,                 // Növénytámasz
        PlantNetting = 834,               // Növényháló
        GardenFencing = 835,              // Kerti kerítés
        LawnAerator = 836,                // Gyep szellőztető
        Dethatcher = 837,                 // Gyepfilc eltávolító
        SeedSpreader = 838,               // Szóró kocsi
        LeafBlower = 839,                 // Lombfúvó
        HedgeTrimmer = 840,               // Sövényvágó
        Chainsaw = 841,                   // Láncfűrész

        // Garázs és műhely
        WoodRouter = 842,                 // Felsőmaró
        Planer = 843,                     // Gyalu
        Jigsaw = 844,                     // Dekopírfűrész
        AngleGrinder = 845,               // Sarokcsiszoló
        MiterSaw = 846,                   // Gérvágó
        TableSaw = 847,                   // Asztali körfűrész
        Lathe = 848,                      // Eszterga
        CNCMachine = 849,                 // CNC gép
        Workbench = 850,                  // Munkapad
        ToolChest = 851,                  // Szerszámos láda / szekrény
        Toolbox = 852,                    // Szerszámos doboz
        PegboardHook = 853,               // Perforált fal kampó
        MagneticTray = 854,               // Mágneses tálca
        Ladder = 855,                     // Létra
        StepStool = 856,                  // Fellépő
        FloorMat = 857,                   // Padlóvédő szőnyeg
        AntiFatigueMat = 858,             // Fáradásgátló szőnyeg
        ShopVac = 859,                    // Ipari porszívó
        PressureWasher = 860,             // Magasnyomású mosó
        AirCompressor = 861,              // Kompresszor
        TireInflator = 862,               // Kerékfelfújó
        JumperCables = 863,               // Bikázó kábel
        BatteryCharger = 864,             // Akkumulátor töltő
        CarJack = 865,                    // Emelő
        JackStand = 866,                  // Bak
        GarageDoorOpener = 867,           // Garázskapu nyitó
        Padlock = 868,                    // Lakatt
        Chain = 869,                      // Lánc
        BungeeCord = 870,                 // Gumipók
        Rope = 871,                       // Kötél
        ZipTie = 872,                     // Kábelkötöző
        DuctTape = 873,                   // Szövetszalag
        ElectricalTape = 874,             // Szigetelőszalag
        MaskingTape = 875,                // Maszkoló szalag
        Fasteners = 876,                  // Kötőelemek általánosan
        Nails = 877,                      // Szögek
        Screws = 878,                     // Csavarok
        Bolts = 879,                      // Csavarok (hatlap)
        Nuts = 880,                       // Anyák
        Washers = 881,                    // Alátétek
        Sandpaper = 882,                  // Csiszolópapír
        SolderingIron = 883,              // Forrasztópáka
        WeldingEquipment = 884,           // Hegesztő eszköz
        EpoxyAdhesive = 885,              // Epoxi ragasztó
        SiliconeSealant = 886,            // Szilikon tömítő
        Degreaser = 887,                  // Zsírtalanító
        BrakeCleaner = 888,               // Féktisztító
        PenetratingOil = 889,             // Kenőanyag (WD-40)

        // Fürdőszoba
        BathroomFurniture = 900,          // Fürdőszobabútor
        BathroomMirror = 901,             // Fürdőszobatükör
        ShowerCurtain = 902,              // Zuhanyfüggöny
        ShowerEnclosure = 903,            // Zuhanykabin
        Bathtub = 904,                    // Kád
        ShowerTray = 905,                 // Zuhanytálca
        Toilet = 906,                     // WC
        ToiletSeat = 907,                 // WC-ülőke
        Bidet = 908,                      // Bidé
        WashBasin = 909,                  // Mosdókagyló
        BathMat = 910,                    // Fürdőszobaszőnyeg
        TowelRail = 911,                  // Törölközőtartó
        ToiletPaperHolder = 912,          // WC-papír tartó
        SoapDispenser = 913,              // Szappanadagoló
        ToothbrushHolder = 914,           // Fogkefetartó
        BathroomShelf = 915,              // Fürdőszobapolc
        ShowerCaddy = 916,                // Zuhanypolc
        BathPillow = 917,                 // Fürdőpárna
        BathSalt = 918,                   // Fürdősó
        BubbleBath = 919,                 // Habfürdő
        BathToy = 920,                    // Fürdőjáték
        ShowerHose = 921,                 // Zuhanycső
        FaucetAerator = 922,              // Perlátor
        DrainStrainer = 923,              // Lefolyószűrő
        ToiletFreshener = 924,            // WC-illatosító
        BathroomHeater = 925,             // Fürdőszobai fűtő
        HairDryer = 926,                  // Hajszárító
        HairStraightener = 927,           // Hajvasaló
        ElectricShaver = 928,             // Villanyborotva
        Epilator = 929,                   // Epilátor

        // Építőanyagok
        BuildingMaterial = 930,           // Építőanyag
        Cement = 931,                     // Cement
        Mortar = 932,                     // Habarcs
        Concrete = 933,                   // Beton
        Sand = 934,                       // Homok
        Gravel = 935,                     // Sóder
        Brick = 936,                      // Tégla
        ConcreteBlock = 937,              // Zsalukő
        Drywall = 938,                    // Gipszkarton
        DrywallCompound = 939,            // Gipszkarton glett
        DrywallTape = 940,                // Gipszkarton szalag
        DrywallAnchor = 941,              // Gipszkarton dűbel
        Lumber = 942,                     // Fűrészáru
        Plywood = 943,                    // Rétegelt lemez
        OSBBoard = 944,                   // OSB lap
        MDFBoard = 945,                   // MDF lap
        Beam = 946,                       // Gerenda
        MetalProfile = 947,               // Fémprofil
        RebarSteel = 948,                 // Betonvas
        WireMesh = 949,                   // Hegesztett háló
        RoofTile = 950,                   // Tetőcserép
        RoofingMembrane = 951,            // Tetőfólia
        Gutter = 952,                     // Ereszcsatorna
        Downspout = 953,                  // Lefolyócső
        Flashing = 954,                   // Szegélylemez
        Tile = 955,                       // Csempe
        TileAdhesive = 956,               // Csemperagasztó
        TileGrout = 957,                  // Fugázó
        TileSpacer = 958,                 // Fugakereszt
        Flooring = 959,                   // Padlóburkolat
        Laminate = 960,                   // Laminált padló
        Parquet = 961,                    // Parketta
        VinylFlooring = 962,              // Vinyl padló
        Carpeting = 963,                  // Padlószőnyeg
        FloorUnderlay = 964,              // Aljzatszigetelés
        Baseboard = 965,                  // Szegélyléc
        DecorativeTrim = 966,             // Díszléc
        Threshold = 967,                  // Küszöb
        ExpandingFoam = 968,              // Purhab
        Waterproofing = 969,              // Vízszigetelés

        // Nyílászárók és vasalatok
        Door = 970,                       // Ajtó
        Window = 971,                     // Ablak
        DoorHandle = 972,                 // Ajtókilincs
        LockCylinder = 973,               // Zárbetét
        Hinge = 974,                      // Zsanér
        DoorCloser = 975,                 // Ajtócsukó
        DoorStop = 976,                   // Ajtóütköző
        Peephole = 977,                   // Kitekintő
        WindowHandle = 978,               // Ablakkilincs
        WindowSill = 979,                 // Ablakpárkány
        GarageDoor = 980,                 // Garázskapu
        Gate = 981,                       // Kapu
        FencePost = 982,                  // Kerítésoszlop
        FencePanel = 983,                 // Kerítéselem
        Bracket = 984,                    // Konzol
        Hook = 985,                       // Kampó
        CabinetHinge = 986,               // Bútorpánt
        DrawerSlide = 987,                // Fióksín
        CabinetHandle = 988,              // Bútorfogantyú
        FurnitureLeg = 989,               // Bútorláb
        FurnitureConnector = 990,         // Bútorösszekötő
        CasterWheel = 991,                // Bútorgörgő
        WallPlug = 992,                   // Dűbel
        AnchorBolt = 993,                 // Betondűbel
        ThreadedRod = 994,                // Menetes szár
        Rivet = 995,                      // Szegecs
        ClevisPin = 996,                  // Csapszeg
        Spring = 997,                     // Rugó
        Bearing = 998,                    // Csapágy
        Gasket = 999,                     // Tömítés

        // Garázs és jármű
        GarageStorage = 1000,             // Garázstároló
        GarageShelving = 1001,            // Garázspolc
        CeilingRack = 1002,               // Mennyezeti tároló
        BikeHanger = 1003,                // Kerékpárakasztó
        TireRack = 1004,                  // Gumiabroncs-tartó
        OilDrainPan = 1005,               // Olajgyűjtő tálca
        Funnel = 1006,                    // Tölcsér
        GreaseGun = 1007,                 // Zsírzóprés
        SocketSet = 1008,                 // Dugókulcs készlet
        TorqueWrench = 1009,              // Nyomatékkulcs
        ImpactWrench = 1010,              // Ütvecsavarozó
        MechanicCreeper = 1011,           // Szerelőágy
        WheelChock = 1012,                // Kerékkitámasztó
        CarCover = 1013,                  // Autótakaró
        SnowChain = 1014,                 // Hólánc
        IceScraper = 1015,                // Jégkaparó
        SnowBrush = 1016,                 // Hókefe
        TowStrap = 1017,                  // Vontatókötél
        RoofRack = 1018,                  // Tetőcsomagtartó
        TrailerHitch = 1019,              // Vonóhorog
        CarMat = 1020,                    // Autószőnyeg
        SeatCover = 1021,                 // Üléshuzat
        CarVacuum = 1022,                 // Autós porszívó
        OBDScanner = 1023,                // OBD diagnosztika
        DashCam = 1024,                   // Menetrögzítő kamera
        PhoneMount = 1025,                // Autós telefontartó
        FuelCan = 1026,                   // Benzinkanna
        Antifreeze = 1027,                // Fagyálló
        AdBlueFluid = 1028,               // AdBlue
        SparkPlug = 1029,                 // Gyújtógyertya

        // Téli és szezonális kültéri
        SnowShovel = 1030,                // Hólapát
        SnowBlower = 1031,                // Hókotró gép
        DeIcingSalt = 1032,               // Útszóró só
        GritBin = 1033,                   // Sótároló
        RoofRake = 1034,                  // Hóeltávolító gereblye
        HeatingCable = 1035,              // Fűtőkábel
        PipeHeater = 1036,                // Csőfűtés
        WindowInsulationKit = 1037,       // Ablakszigetelő készlet
        Sled = 1038,                      // Szánkó
        WinterCarKit = 1039,              // Téli autós készlet

        // Iratok és értéktárgyak
        DocumentStorage = 1040,           // Irattároló
        CashBox = 1041,                   // Pénzkazetta
        KeyOrganizer = 1042,              // Kulcstartó
        SpareKey = 1043,                  // Pótkulcs
        Jewelry = 1044,                   // Ékszer
        Watch = 1045,                     // Karóra
        Collectible = 1046,               // Gyűjtői tárgy
        Souvenir = 1047,                  // Ajándéktárgy
        GiftCard = 1048                   // Ajándékkártya
    }
}
