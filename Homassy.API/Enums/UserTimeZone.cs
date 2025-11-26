namespace Homassy.API.Enums
{
    public enum UserTimeZone
    {
        // UTC
        UTC = 0,

        // Americas
        EasternStandardTime = 1, // UTC-5 (New York, Toronto, Miami)
        CentralStandardTime = 2, // UTC-6 (Chicago, Mexico City, Dallas)
        MountainStandardTime = 3, // UTC-7 (Denver, Phoenix, Calgary)
        PacificStandardTime = 4, // UTC-8 (Los Angeles, Vancouver, Seattle)
        AlaskanStandardTime = 5, // UTC-9 (Anchorage)
        HawaiianStandardTime = 6, // UTC-10 (Honolulu)
        AtlanticStandardTime = 7, // UTC-4 (Halifax, Caracas, Santiago)
        ArgentinaStandardTime = 8, // UTC-3 (Buenos Aires)
        BrazilianStandardTime = 9, // UTC-3 (Sao Paulo, Rio de Janeiro)

        // Europe
        GreenwichStandardTime = 10, // UTC+0 (London, Dublin, Lisbon)
        CentralEuropeStandardTime = 11, // UTC+1 (Budapest, Prague, Vienna, Berlin, Paris, Rome)
        EasternEuropeStandardTime = 12, // UTC+2 (Athens, Bucharest, Helsinki, Kiev)
        RussianStandardTime = 13, // UTC+3 (Moscow, St. Petersburg)
        TurkeyStandardTime = 14, // UTC+3 (Istanbul, Ankara)

        // Asia
        ArabianStandardTime = 15, // UTC+3 (Dubai, Abu Dhabi)
        PakistanStandardTime = 16, // UTC+5 (Karachi, Islamabad)
        IndiaStandardTime = 17, // UTC+5:30 (Mumbai, Delhi, Bangalore)
        BangladeshStandardTime = 18, // UTC+6 (Dhaka)
        ChinaStandardTime = 19, // UTC+8 (Beijing, Shanghai, Hong Kong)
        SingaporeStandardTime = 20, // UTC+8 (Singapore, Kuala Lumpur)
        TokyoStandardTime = 21, // UTC+9 (Tokyo, Osaka, Seoul)
        KoreaStandardTime = 22, // UTC+9 (Seoul, Pyongyang)

        // Australia & Pacific
        AustralianWesternStandardTime = 23, // UTC+8 (Perth)
        AustralianCentralStandardTime = 24, // UTC+9:30 (Adelaide, Darwin)
        AustralianEasternStandardTime = 25, // UTC+10 (Sydney, Melbourne, Brisbane)
        NewZealandStandardTime = 26, // UTC+12 (Auckland, Wellington)

        // Africa
        SouthAfricaStandardTime = 27, // UTC+2 (Johannesburg, Cape Town)
        EgyptStandardTime = 28, // UTC+2 (Cairo)
        WestAfricaStandardTime = 29, // UTC+1 (Lagos, Algiers)

        // Middle East
        IsraelStandardTime = 30, // UTC+2 (Jerusalem, Tel Aviv)
        SaudiArabiaStandardTime = 31, // UTC+3 (Riyadh, Jeddah)
        IranStandardTime = 32 // UTC+3:30 (Tehran)
    }
}
