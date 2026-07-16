namespace Homassy.API.Enums
{
    /// <summary>
    /// Type of a shopping location. A location may carry several of these at once
    /// (e.g. an OBI is both a <see cref="HardwareStore"/> and a <see cref="GardenCenter"/>),
    /// so the entity stores a set of values. Used by the shopping-list "similar store here"
    /// proximity highlight. Values are grouped in gapped ranges to leave room for later
    /// additions without a migration.
    /// </summary>
    public enum StoreType
    {
        // Egyéb
        Other = 0,                    // Egyéb

        // Élelmiszer és általános bolt
        Supermarket = 10,             // Szupermarket
        Hypermarket = 11,             // Hipermarket
        DiscountStore = 12,           // Diszkont
        GroceryStore = 13,            // Élelmiszerbolt
        ConvenienceStore = 14,        // Kisbolt
        Market = 15,                  // Piac
        FarmersMarket = 16,           // Termelői piac
        OrganicStore = 17,            // Bio- és reformbolt
        WholesaleStore = 18,          // Nagykereskedés

        // Élelmiszer szaküzletek
        Bakery = 20,                  // Pékség
        Confectionery = 21,           // Cukrászda
        Butcher = 22,                 // Hentesbolt
        Fishmonger = 23,              // Halbolt
        Greengrocer = 24,             // Zöldséges
        Delicatessen = 25,            // Csemegebolt
        BeverageStore = 26,           // Ital- és borszaküzlet
        TobaccoShop = 27,             // Dohánybolt
        CandyStore = 28,              // Édességbolt
        CoffeeTeaShop = 29,           // Kávé- és teabolt

        // Egészség és szépség
        Drugstore = 30,               // Drogéria
        Pharmacy = 31,                // Gyógyszertár
        Perfumery = 32,               // Parfüméria
        Optician = 33,                // Optika
        MedicalSupply = 34,           // Gyógyászati segédeszköz

        // Otthon, barkács és kert
        HardwareStore = 40,           // Barkácsáruház
        BuildingMaterials = 41,       // Építőanyag-kereskedés
        PaintStore = 42,              // Festékbolt
        GardenCenter = 43,            // Kertészet
        FurnitureStore = 44,          // Bútorbolt
        HomeDecorStore = 45,          // Lakberendezési bolt
        HouseholdGoods = 46,          // Háztartási bolt

        // Elektronika és média
        ElectronicsStore = 50,        // Műszaki bolt
        ComputerStore = 51,           // Számítástechnikai bolt
        MobilePhoneShop = 52,         // Mobiltelefon-üzlet
        Bookstore = 53,               // Könyvesbolt
        StationeryStore = 54,         // Papír- és írószerbolt
        MusicStore = 55,              // Hangszerbolt
        PhotoStore = 56,              // Fotó szaküzlet

        // Ruházat és kiegészítők
        ClothingStore = 60,           // Ruházati bolt
        ShoeStore = 61,               // Cipőbolt
        SportsStore = 62,             // Sportbolt
        JewelryStore = 63,            // Ékszerbolt
        LeatherGoodsStore = 64,       // Táska- és bőrdíszmű
        FabricStore = 65,             // Méteráru- és rövidárubolt
        SecondHandStore = 66,         // Használtruha-üzlet
        BabyStore = 67,               // Baba-mama bolt

        // Hobbi, gyerek, állat és szabadidő
        ToyStore = 70,                // Játékbolt
        HobbyCraftStore = 71,         // Hobbi- és kézművesbolt
        PetStore = 72,                // Állateledel-bolt
        BicycleShop = 73,             // Kerékpárbolt
        OutdoorStore = 74,            // Outdoor és túrabolt
        GiftShop = 75,                // Ajándékbolt
        ArtSupplies = 76,             // Művészellátó

        // Autó és üzemanyag
        PetrolStation = 80,           // Benzinkút
        AutoPartsStore = 81,          // Autóalkatrész-bolt

        // Nagy alapterületű és vegyes
        DepartmentStore = 90,         // Áruház
        ShoppingMall = 91,            // Bevásárlóközpont
        OutletStore = 92,             // Outlet
        VarietyStore = 93,            // Vegyes diszkontáruház
        FleaMarket = 94,              // Bolhapiac

        // Online és egyéb
        OnlineStore = 100,            // Webáruház
        OnlineMarketplace = 101,      // Online piactér
        Kiosk = 102,                  // Újságos / trafik
        PostOffice = 103              // Posta
    }
}
