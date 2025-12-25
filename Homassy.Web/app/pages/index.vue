<template>
  <div>
    <UPageHero
      title="Homassy"
      description="Segít átlátni a háztartási készleteket, bevásárlólistákat és megosztani mindezt a családdal."
    />

    <UPageSection
      id="features"
      title="Fő funkciók"
      :features="[
        {
          icon: 'i-lucide-user-check',
          title: 'Felhasználó és hitelesítés',
          description: 'Jelszó nélküli, email-kódos belépés. Biztonságos tokenekkel jelentkezel be és ki.'
        },
        {
          icon: 'i-lucide-users',
          title: 'Család kezelés',
          description: 'Hozz létre családot, hívj meg tagokat kóddal, és kezeljétek együtt az adatokat.'
        },
        {
          icon: 'i-lucide-package-check',
          title: 'Termékek és készlet',
          description: 'Vezesd a termékeket és a készletet: felvétel, mozgatás, fogyasztás, kedvencek.'
        },
        {
          icon: 'i-lucide-shopping-cart',
          title: 'Bevásárlólisták',
          description: 'Közös bevásárlólisták tételekkel, gyors vásárlással, ami azonnal a készletbe kerül.'
        },
        {
          icon: 'i-lucide-map-pin',
          title: 'Helyszínek',
          description: 'Tárold a boltokat és az otthoni tárolóhelyeket; egyszerre több tételt is módosíthatsz.'
        },
        {
          icon: 'i-lucide-barcode',
          title: 'Vonalkód és adatminőség',
          description: 'Vonalkód olvasás és ellenőrzés, Open Food Facts adatok, biztonságos képfeltöltés.'
        }
      ]"
    />
  </div>
</template>

<script setup>
definePageMeta({
  layout: 'public'
})
</script>
