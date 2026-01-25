namespace Homassy.API.Enums
{
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
        Kitchen = 110,                    // Konyhai eszközök
        Cookware = 111,                   // Edény és főzőedény
        Cutlery = 112,                    // Evőeszköz és villa
        Dinnerware = 113,                 // Tányér és csésze
        Glass = 114,                      // Pohár
        Utensil = 115,                    // Konyhai segédeszköz
        Appliance = 116,                  // Konyhai gépek
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
        Garden = 160,                     // Kertészeti szerszámok
        Shovel = 161,                     // Lapát
        Rake = 162,                       // Ról
        Hoe = 163,                        // Kapá
        Pruning = 164,                    // Metsző szerszám
        Watering = 165,                   // Öntözés

        // Elektromos termékek és alkatrészek
        Electronics = 170,                // Elektronika
        Battery = 171,                    // Akkumátor
        Charger = 172,                    // Töltő
        Cable = 173,                      // Kábel
        Adapter = 174,                    // Adapter
        Bulb = 175,                       // Izzó
        ExtensionCord = 176,              // Hosszabbítókábel
        PowerStrip = 177,                 // Elosztó

        // Autós és közlekedési termékek
        Automotive = 180,                 // Autóápolás
        EngineOil = 181,                  // Motorolaj
        Coolant = 182,                    // Hűtőfolyadék
        BrakeFl = 183,                    // Fékolaj
        Wiper = 184,                      // Utasszélvédő lemez
        TireCare = 185,                   // Gumiabroncs ápolás
        CarWash = 186,                    // Autómosó
        Wax = 187,                        // Viasz

        // Ruházat és lábbelik
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
        PetSupply = 220,                  // Háziállat termékek
        PetFood = 221,                    // Háziállat táp
        PetCare = 222,                    // Háziállat ápolás
        PetToy = 223,                     // Háziállat játékszer
        PetBed = 224,                     // Háziállat ágy és házzal

        // Iroda és papíráruk
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
        BabyDiaper = 250,                 // Pelenka
        BabyWipes = 251,                  // Baba törlőkendő
        BabyFormula = 252,                // Baba tápszer
        BabyFood = 253,                   // Bébiétel
        BabyCare = 254,                   // Baba ápolás
        Nursing = 255,                    // Szoptatási kellékek
        Stroller = 256,                   // Babakocsi
        CarSeat = 257,                    // Gyerek autósülés

        // Tisztító eszközök
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
        InsectRepellent = 290,            // Rovarriasztó
        InsectTrap = 291,                 // Rovarcsapda
        RodentTrap = 292,                 // Rágcsáló csapda
        Pesticide = 293,                  // Növényvédő szer

        // Okosotthon és biztonság
        SmartHome = 300,                  // Okosotthon eszközök
        SmartBulb = 301,                  // Okos izzó
        SmartPlug = 302,                  // Okos dugalj
        DoorLock = 303,                   // Okos zár
        SmokeDetector = 304,              // Füstérzékelő
        CarbonMonoxideDetector = 305,     // Szén-monoxid érzékelő
        Alarm = 306,                      // Riasztó
        Camera = 307,                     // Biztonsági kamera

        // Levegő és víz kezelése
        AirPurifier = 320,                // Légtisztító
        Humidifier = 321,                 // Párásító
        Dehumidifier = 322,               // Párátlanító
        Fan = 323,                        // Ventilátor
        Heater = 324,                     // Hősugárzó
        WaterFilter = 325,                // Vízszűrő

        // Kis háztartási gépek
        CoffeeMaker = 340,                // Kávéfőző
        Kettle = 341,                     // Vízforraló
        Toaster = 342,                    // Kenyérpirító
        Blender = 343,                    // Turmixgép
        Mixer = 344,                      // Konyhai robot, mixer
        FoodProcessor = 345,              // Aprító
        SlowCooker = 346,                 // Lassúfőző
        RiceCooker = 347,                 // Rizsfőző

        // Nagy háztartási gépek
        Refrigerator = 360,               // Hűtőszekrény
        Freezer = 361,                    // Fagyasztó
        Oven = 362,                       // Sütő
        Cooktop = 363,                    // Főzőlap
        Microwave = 364,                  // Mikrohullámú sütő
        Dishwasher = 365,                 // Mosogatógép
        WashingMachine = 366,             // Mosógép
        Dryer = 367,                      // Szárítógép

        // Számítástechnika és mobil
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
        CameraLens = 400,                 // Kamera objektív
        Tripod = 401,                     // Állvány
        MemoryCard = 402,                 // Memóriakártya
        ActionCamera = 403,               // Sportkamera

        // Parti és szezonális
        PartySupply = 440,                // Parti kellékek
        BirthdaySupply = 441,             // Szülinapi kellékek
        SeasonalDecoration = 442,         // Szezonális dekoráció
        ChristmasDecoration = 443,        // Karácsonyi dekoráció
        HalloweenDecoration = 444,        // Halloween dekoráció

        // Utazás és poggyász
        Luggage = 460,                    // Bőrönd
        TravelAccessory = 461,            // Utazási kiegészítők
        TravelBottle = 462,               // Utazó palackok
        PassportHolder = 463,             // Útlevél tok

        // Biztonság és vészhelyzeti
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
        Thermometer = 500,                // Lázmérő
        BloodPressureMonitor = 501,       // Vérnyomásmérő
        Glucometer = 502,                 // Vércukormérő
        PulseOximeter = 503,              // Pulzoximéter
        HeatingPad = 504,                 // Melegítő párna
        Massager = 505,                   // Masszírozó
        BraceSupport = 506,               // Támasztók, merevítők
        MobilityAid = 507,                // Járássegítő, kerekesszék

        // Szexuális jólét
        SexualWellness = 520,             // Szexuális egészség
        Condom = 521,                     // Óvszer
        Lubricant = 522,                  // Síkosító

        // Látás és hallás
        VisionCare = 530,                 // Látásápolás
        EyeglassCleaner = 531,            // Szemüvegtisztító
        ContactLens = 532,                // Kontaktlencse
        HearingAid = 533,                 // Hallókészülék

        // Lábápolás és alátétek
        FootCare = 540,                   // Lábápolás
        Insoles = 541,                    // Talpbetét

        // Varrás és kézműves
        SewingKit = 550,                  // Varrókészlet
        YarnAndKnitting = 551,            // Fonál és kötés
        CraftSupply = 552,                // Kézműves kellékek
        Fabric = 553,                     // Anyagok

        // Festés és dekorálás
        Paint = 570,                      // Festék
        Primer = 571,                     // Alapozó
        BrushRoller = 572,                // Ecset és henger
        PainterTape = 573,                // Maszkoló szalag

        // Vízvezeték és elektromos
        Plumbing = 590,                   // Vízvezeték szerelvények
        PipeFitting = 591,                // Idomok
        Faucet = 592,                     // Csaptelep
        ShowerHead = 593,                 // Zuhanyfej
        Electrical = 600,                 // Elektromos szerelvények
        Switch = 601,                     // Kapcsoló
        Outlet = 602,                     // Konnektor
        Fuse = 603,                       // Biztosíték
        Wire = 604,                       // Vezeték

        // Fűtés és hűtés
        AirConditioner = 610,             // Légkondicionáló
        Radiator = 611,                   // Radiátor
        Thermostat = 612,                 // Szobatermosztát

        // Kültér és terasz
        OutdoorFurniture = 630,           // Kerti bútor
        PatioUmbrella = 631,              // Napernyő
        GardenDecor = 632,                // Kerti dekoráció
        BirdFeeder = 633,                 // Madáretető

        // Grillezés és üzemanyag
        Grill = 650,                      // Grill
        GrillAccessory = 651,             // Grill kiegészítők
        Charcoal = 652,                   // Faszen
        Propane = 653,                    // Propán palack

        // Kert és pázsit bővítés
        Seeds = 670,                      // Magok
        Soil = 671,                       // Föld, virágföld
        Fertilizer = 672,                 // Műtrágya
        PlanterPot = 673,                 // Cserép
        Hose = 674,                       // Locsolótömlő
        Sprinkler = 675,                  // Permetező, szórófej
        LawnMower = 676,                  // Fűnyíró
        Trimmer = 677,                    // Szegélynyíró

        // Kemping és szabadidő
        Tent = 700,                       // Sátor
        SleepingBag = 701,                // Hálózsák
        CampingMat = 702,                 // Polifoam, matrac
        CampingCookware = 703,            // Kemping főzőeszköz
        Cooler = 704,                     // Hűtőláda

        // Sport és fitnesz
        FitnessEquipment = 720,           // Fitnesz eszközök
        YogaMat = 721,                    // Jóga szőnyeg
        Dumbbell = 722,                   // Súlyzó
        ResistanceBand = 723,             // Gumiszalag
        BicycleAccessory = 750,           // Kerékpár kiegészítő
        Helmet = 751,                     // Bukósisak
        Pump = 752,                       // Pumpa

        // Társasjáték és kirakó
        BoardGame = 800,                  // Társasjáték
        Puzzle = 801,                     // Kirakó

        // Iskolai felszerelés
        SchoolBag = 810,                  // Iskolatáska
        LunchBox = 811,                   // Uzsonnás doboz
        WaterBottle = 812,                // Kulacs

        // Kert bővített
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
    }
}
