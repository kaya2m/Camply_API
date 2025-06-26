using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Camply.Domain.Enums
{
    [Flags]
    public enum LocationFeature : long
    {
        None = 0,

        // Temel Tesisler
        Shower = 1L << 0,           // Duş
        Toilet = 1L << 1,           // WC
        Restaurant = 1L << 2,       // Restoran
        Market = 1L << 3,           // Market
        Cafe = 1L << 4,             // Kafe
        Bar = 1L << 5,              // Bar

        // İnternet & Bağlantı
        WiFi = 1L << 6,             // WiFi
        CellSignal = 1L << 7,       // Cep telefonu sinyali

        // Güvenlik
        Security = 1L << 8,         // Güvenlik
        SafeBox = 1L << 9,          // Kasa
        Lighting = 1L << 10,        // Aydınlatma

        // Elektrik & Su
        Electricity = 1L << 11,     // Elektrik
        WaterSupply = 1L << 12,     // Su temini
        HotWater = 1L << 13,        // Sıcak su

        // Temizlik
        Laundry = 1L << 14,         // Çamaşırhane
        Cleaning = 1L << 15,        // Temizlik servisi

        // Park & Ulaşım
        Parking = 1L << 16,         // Otopark
        PublicTransport = 1L << 17, // Toplu taşıma
        CarRental = 1L << 18,       // Araç kiralama

        // Eğlence & Aktivite
        Pool = 1L << 19,            // Havuz
        Playground = 1L << 20,      // Oyun alanı
        Sports = 1L << 21,          // Spor tesisleri
        BikeRental = 1L << 22,      // Bisiklet kiralama

        // Doğa & Manzara
        BeachAccess = 1L << 23,     // Plaj erişimi
        HikingTrails = 1L << 24,    // Yürüyüş yolları
        NatureView = 1L << 25,      // Doğa manzarası

        // Pet & Aile
        PetFriendly = 1L << 26,     // Evcil hayvan dostu
        FamilyFriendly = 1L << 27,  // Aile dostu
        BabyFacilities = 1L << 28,  // Bebek tesisleri

        // Yemek & İçecek
        Kitchen = 1L << 29,         // Mutfak
        Barbecue = 1L << 30,        // Barbekü
        Minibar = 1L << 31,         // Minibar

        // Diğer
        Conference = 1L << 32,      // Konferans salonu
        Medical = 1L << 33,         // Sağlık hizmetleri
        Shop = 1L << 34,            // Mağaza
        ATM = 1L << 35,             // ATM
        Currency = 1L << 36,        // Döviz
        Spa = 1L << 37,             // SPA
        Gym = 1L << 38,             // Spor salonu
        AirConditioning = 1L << 39, // Klima
        Heating = 1L << 40,         // Isıtma
        Fireplace = 1L << 41,       // Şömine
        Garden = 1L << 42,          // Bahçe
        Terrace = 1L << 43,         // Teras
        Balcony = 1L << 44,         // Balkon
        SeaView = 1L << 45,         // Deniz manzarası
        MountainView = 1L << 46,    // Dağ manzarası
        CityView = 1L << 47,        // Şehir manzarası
        Accessible = 1L << 48,      // Engelli erişimi
        Quiet = 1L << 49,           // Sessiz ortam
        Romantic = 1L << 50,        // Romantik
        BusinessFriendly = 1L << 51 // İş dostu
    }
}
