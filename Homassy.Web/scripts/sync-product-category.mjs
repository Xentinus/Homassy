/**
 * Regenerates everything on the web side that is derived from the canonical
 * ProductCategory enum in Homassy.API/Enums/ProductCategory.cs:
 *
 *   - app/types/enums.ts              -> `export enum ProductCategory`
 *   - app/types/enums.ts              -> `export enum ProductCategoryGroup`
 *   - app/utils/productCategoryGroups.ts (whole file)
 *   - i18n/locales/{en,hu,de}.json    -> `enums.productCategory`
 *   - i18n/locales/{en,hu,de}.json    -> `enums.productCategoryGroup`
 *
 * The API enum is canonical because the numeric value is what `Product.Category`
 * persists, and the option list the frontend renders comes from the API
 * (`GET /selectvalues?type=ProductCategory` -> `SelectValueFunctions`), which
 * enumerates that enum. Nothing on the web side may invent its own numbering.
 *
 * Labels live in LABELS below, keyed by enum member name (never by number), so a
 * renumbering in the API enum can never silently shift the translations. The
 * script fails if the enum and LABELS disagree, or if a language reuses a label.
 *
 * GROUPS is presentation-only: it buckets the enum's numeric blocks so the
 * category pickers can render a grouped, searchable list instead of ~950 flat
 * options. Groups exist only on the web side -- the API contract is unchanged.
 *
 * Run: npm run sync:product-category
 */

import fs from 'node:fs'
import path from 'node:path'
import { fileURLToPath } from 'node:url'

const webRoot = path.resolve(path.dirname(fileURLToPath(import.meta.url)), '..')
const repoRoot = path.resolve(webRoot, '..')
const enumSourcePath = path.join(repoRoot, 'Homassy.API', 'Enums', 'ProductCategory.cs')
const enumsTsPath = path.join(webRoot, 'app', 'types', 'enums.ts')
const groupsTsPath = path.join(webRoot, 'app', 'utils', 'productCategoryGroups.ts')
const localeDir = path.join(webRoot, 'i18n', 'locales')

const LANGUAGES = ['en', 'hu', 'de']

/**
 * Presentation buckets over the enum's numeric blocks, in the order the pickers
 * should show them. Each group's enum value is its index here, so reordering this
 * list renumbers ProductCategoryGroup -- harmless, since the group is never
 * persisted, but it does change the locale keys, so regenerate after editing.
 *
 * Every ProductCategory member must fall in exactly one range. Ranges may cover
 * unused numbers (headroom for new members inside the same block).
 */
const GROUPS = [
  { name: 'Other', ranges: [[0, 0]], en: 'Other', hu: 'Egyéb', de: 'Sonstiges' },
  { name: 'Food', ranges: [[1, 49]], en: 'Food and Drink', hu: 'Élelmiszer és ital', de: 'Lebensmittel und Getränke' },
  { name: 'Health', ranges: [[50, 69], [500, 549]], en: 'Health and Medicine', hu: 'Egészség és gyógyszer', de: 'Gesundheit und Medizin' },
  { name: 'PersonalCare', ranges: [[70, 89], [926, 929]], en: 'Personal Care', hu: 'Testápolás', de: 'Körperpflege' },
  { name: 'Bathroom', ranges: [[900, 925]], en: 'Bathroom', hu: 'Fürdőszoba', de: 'Badezimmer' },
  { name: 'Cleaning', ranges: [[90, 109], [270, 289]], en: 'Cleaning and Household', hu: 'Tisztítás és háztartás', de: 'Reinigung und Haushalt' },
  { name: 'PestControl', ranges: [[290, 299]], en: 'Pest Control', hu: 'Kártevőirtás', de: 'Schädlingsbekämpfung' },
  { name: 'Kitchen', ranges: [[110, 119], [340, 359]], en: 'Kitchen', hu: 'Konyha', de: 'Küche' },
  { name: 'Appliances', ranges: [[320, 331], [360, 379]], en: 'Appliances', hu: 'Háztartási gépek', de: 'Haushaltsgeräte' },
  { name: 'Home', ranges: [[120, 139]], en: 'Home and Furnishing', hu: 'Lakberendezés', de: 'Wohnen und Einrichtung' },
  { name: 'HomeImprovement', ranges: [[570, 629], [930, 999]], en: 'Renovation and Building', hu: 'Felújítás és építés', de: 'Renovierung und Bau' },
  { name: 'Tools', ranges: [[140, 159], [842, 889]], en: 'Tools and DIY', hu: 'Szerszám és barkácsolás', de: 'Werkzeug und Heimwerken' },
  { name: 'Garden', ranges: [[160, 169], [630, 699], [820, 841]], en: 'Garden and Outdoor', hu: 'Kert és szabadtér', de: 'Garten und Außenbereich' },
  { name: 'Automotive', ranges: [[180, 189], [1000, 1029]], en: 'Car and Garage', hu: 'Autó és garázs', de: 'Auto und Garage' },
  { name: 'Winter', ranges: [[1030, 1039]], en: 'Winter and Seasonal', hu: 'Téli és szezonális', de: 'Winter und Saison' },
  { name: 'Electronics', ranges: [[170, 179], [380, 409]], en: 'Electronics', hu: 'Elektronika', de: 'Elektronik' },
  { name: 'SmartHome', ranges: [[300, 319]], en: 'Smart Home and Security', hu: 'Okosotthon és biztonság', de: 'Smart Home und Sicherheit' },
  { name: 'Clothing', ranges: [[190, 209]], en: 'Clothing', hu: 'Ruházat', de: 'Kleidung' },
  { name: 'Entertainment', ranges: [[210, 219], [550, 569], [800, 809]], en: 'Entertainment and Hobby', hu: 'Szórakozás és hobbi', de: 'Unterhaltung und Hobby' },
  { name: 'SportsAndLeisure', ranges: [[700, 799]], en: 'Sports and Leisure', hu: 'Sport és szabadidő', de: 'Sport und Freizeit' },
  { name: 'Pets', ranges: [[220, 229]], en: 'Pets', hu: 'Háziállat', de: 'Haustiere' },
  { name: 'Office', ranges: [[230, 249]], en: 'Office and Stationery', hu: 'Iroda és papíráru', de: 'Büro und Schreibwaren' },
  { name: 'BabyAndKids', ranges: [[250, 269], [810, 819]], en: 'Baby and Kids', hu: 'Baba és gyermek', de: 'Baby und Kind' },
  { name: 'PartyAndSeasonal', ranges: [[440, 459]], en: 'Party and Celebration', hu: 'Parti és ünnep', de: 'Party und Feier' },
  { name: 'Travel', ranges: [[460, 479]], en: 'Travel', hu: 'Utazás', de: 'Reisen' },
  { name: 'Safety', ranges: [[480, 499]], en: 'Safety and Emergency', hu: 'Biztonság és vészhelyzet', de: 'Sicherheit und Notfall' },
  { name: 'Valuables', ranges: [[1040, 1049]], en: 'Documents and Valuables', hu: 'Iratok és értéktárgyak', de: 'Dokumente und Wertsachen' }
]

/**
 * Display labels per enum member. `en` is the label, not a mechanical
 * de-camel-casing of the member name -- a few members are named imprecisely
 * (`MentalCare` is men's care, `BrakeFl` is brake fluid) and the label follows
 * the intent documented in the C# comment.
 */
const LABELS = {
  // --- Other ---
  Other: { en: 'Other', hu: 'Egyéb', de: 'Sonstiges' },

  // --- Food ---
  Grain: { en: 'Grain', hu: 'Gabonafélék', de: 'Getreide' },
  Bread: { en: 'Bread and Bakery', hu: 'Kenyér és pékáru', de: 'Brot und Backwaren' },
  CerealAndBreakfast: { en: 'Cereal and Breakfast', hu: 'Müzli és reggelizőpehely', de: 'Müsli und Frühstückscerealien' },
  Pasta: { en: 'Pasta', hu: 'Tészta', de: 'Nudeln' },
  Rice: { en: 'Rice', hu: 'Rizs', de: 'Reis' },
  Flour: { en: 'Flour', hu: 'Liszt', de: 'Mehl' },
  Sugar: { en: 'Sugar and Sweeteners', hu: 'Cukor és édesítőszer', de: 'Zucker und Süßungsmittel' },
  Salt: { en: 'Salt', hu: 'Só', de: 'Salz' },
  Spices: { en: 'Spices', hu: 'Fűszerek és fűszerkeverékek', de: 'Gewürze und Gewürzmischungen' },
  Oil: { en: 'Cooking Oil', hu: 'Étolaj', de: 'Speiseöl' },
  Butter: { en: 'Butter', hu: 'Vaj', de: 'Butter' },
  Margarine: { en: 'Margarine', hu: 'Margarin', de: 'Margarine' },
  Cheese: { en: 'Cheese', hu: 'Sajt', de: 'Käse' },
  Milk: { en: 'Milk and Dairy', hu: 'Tej és tejtermék', de: 'Milch und Milchprodukte' },
  Yogurt: { en: 'Yogurt and Kefir', hu: 'Joghurt és kefir', de: 'Joghurt und Kefir' },
  Cream: { en: 'Cream', hu: 'Tejszín', de: 'Sahne' },
  Egg: { en: 'Egg', hu: 'Tojás', de: 'Eier' },
  Meat: { en: 'Fresh Meat', hu: 'Friss hús', de: 'Frischfleisch' },
  Poultry: { en: 'Poultry', hu: 'Baromfihús', de: 'Geflügel' },
  Fish: { en: 'Fish', hu: 'Hal', de: 'Fisch' },
  Seafood: { en: 'Seafood', hu: 'Tenger gyümölcsei', de: 'Meeresfrüchte' },
  Sausage: { en: 'Sausage and Cured Meat', hu: 'Kolbász és húskészítmény', de: 'Wurst und Fleischwaren' },
  Vegetable: { en: 'Fresh Vegetables', hu: 'Friss zöldség', de: 'Frisches Gemüse' },
  Fruit: { en: 'Fresh Fruit', hu: 'Friss gyümölcs', de: 'Frisches Obst' },
  BerryAndNuts: { en: 'Berries and Nuts', hu: 'Bogyós gyümölcs és olajos mag', de: 'Beeren und Nüsse' },
  Legume: { en: 'Legumes', hu: 'Hüvelyesek', de: 'Hülsenfrüchte' },
  Canned: { en: 'Canned Food', hu: 'Konzerv', de: 'Konserven' },
  FrozenFood: { en: 'Frozen Food', hu: 'Fagyasztott étel', de: 'Tiefkühlkost' },
  Jam: { en: 'Jam, Honey and Syrup', hu: 'Lekvár, méz és szirup', de: 'Marmelade, Honig und Sirup' },
  Peanut: { en: 'Peanut and Peanut Butter', hu: 'Mogyoró és mogyoróvaj', de: 'Erdnüsse und Erdnussbutter' },
  Chocolate: { en: 'Chocolate and Cocoa', hu: 'Csokoládé és kakaó', de: 'Schokolade und Kakao' },
  Candy: { en: 'Candy and Sweets', hu: 'Cukorka és édesség', de: 'Bonbons und Süßigkeiten' },
  Snack: { en: 'Snacks and Chips', hu: 'Snack és chips', de: 'Snacks und Chips' },
  Cookie: { en: 'Cookies and Pastries', hu: 'Süti és aprósütemény', de: 'Kekse und Feingebäck' },
  Beverage: { en: 'Soft Drinks', hu: 'Alkoholmentes ital', de: 'Alkoholfreie Getränke' },
  Coffee: { en: 'Coffee', hu: 'Kávé', de: 'Kaffee' },
  Tea: { en: 'Tea', hu: 'Tea', de: 'Tee' },
  AlcoholicBeverage: { en: 'Alcoholic Beverages', hu: 'Alkoholos ital', de: 'Alkoholische Getränke' },
  Wine: { en: 'Wine', hu: 'Bor', de: 'Wein' },
  Beer: { en: 'Beer', hu: 'Sör', de: 'Bier' },
  Sauce: { en: 'Sauce', hu: 'Szósz és mártás', de: 'Sauce' },
  Condiment: { en: 'Condiments', hu: 'Fűszerpaszta és ízesítő', de: 'Würzmittel' },
  Seasoning: { en: 'Seasoning Mix', hu: 'Kevert fűszer', de: 'Gewürzzubereitung' },
  VitaminAndSupplement: { en: 'Vitamins and Supplements', hu: 'Vitamin és étrend-kiegészítő', de: 'Vitamine und Nahrungsergänzung' },
  BakingIngredient: { en: 'Baking Ingredient', hu: 'Sütési alapanyag', de: 'Backzutat' },
  ReadyMeal: { en: 'Ready Meal', hu: 'Készétel', de: 'Fertiggericht' },
  SoupAndBroth: { en: 'Soup and Broth', hu: 'Leves és alaplé', de: 'Suppe und Brühe' },
  DriedFood: { en: 'Dried Food', hu: 'Szárított élelmiszer', de: 'Trockenware' },
  Pickles: { en: 'Pickles', hu: 'Savanyúság', de: 'Eingelegtes' },

  // --- Health ---
  Medicine: { en: 'Medicine', hu: 'Gyógyszer', de: 'Medikament' },
  PainRelief: { en: 'Pain Relief', hu: 'Fájdalomcsillapító', de: 'Schmerzmittel' },
  Antibiotic: { en: 'Antibiotic', hu: 'Antibiotikum', de: 'Antibiotikum' },
  AntiInflammatory: { en: 'Anti-Inflammatory', hu: 'Gyulladáscsökkentő', de: 'Entzündungshemmer' },
  ColdAndFlu: { en: 'Cold and Flu', hu: 'Megfázás és influenza elleni', de: 'Erkältung und Grippe' },
  Allergy: { en: 'Allergy Relief', hu: 'Allergia elleni', de: 'Allergiemittel' },
  Digestive: { en: 'Digestive Aid', hu: 'Emésztést segítő', de: 'Verdauungsmittel' },
  Cough: { en: 'Cough Remedy', hu: 'Köhögés elleni', de: 'Hustenmittel' },
  Vitamin: { en: 'Vitamin', hu: 'Vitaminkészítmény', de: 'Vitaminpräparat' },
  Supplement: { en: 'Dietary Supplement', hu: 'Étrend-kiegészítő', de: 'Nahrungsergänzungsmittel' },
  Probiotic: { en: 'Probiotic', hu: 'Probiotikum', de: 'Probiotikum' },
  FirstAid: { en: 'First Aid', hu: 'Elsősegély termék', de: 'Erste-Hilfe-Produkt' },
  Bandage: { en: 'Bandage', hu: 'Kötszer', de: 'Verbandmaterial' },
  Plaster: { en: 'Plaster', hu: 'Sebtapasz', de: 'Pflaster' },
  Cotton: { en: 'Cotton Wool', hu: 'Vatta', de: 'Watte' },
  Gauze: { en: 'Gauze', hu: 'Gézlap', de: 'Gaze' },
  Ointment: { en: 'Cream and Ointment', hu: 'Krém és kenőcs', de: 'Creme und Salbe' },
  Syrup: { en: 'Syrup', hu: 'Szirup és oldat', de: 'Sirup und Lösung' },
  Tablet: { en: 'Tablets and Capsules', hu: 'Tabletta és kapszula', de: 'Tabletten und Kapseln' },
  Injection: { en: 'Injection', hu: 'Injekció', de: 'Injektion' },

  // --- PersonalCare ---
  Soap: { en: 'Soap', hu: 'Szappan', de: 'Seife' },
  Shampoo: { en: 'Shampoo', hu: 'Sampon', de: 'Shampoo' },
  Conditioner: { en: 'Conditioner', hu: 'Hajbalzsam', de: 'Haarspülung' },
  BodyWash: { en: 'Body Wash', hu: 'Tusfürdő', de: 'Duschgel' },
  ToothPaste: { en: 'Toothpaste', hu: 'Fogkrém', de: 'Zahnpasta' },
  ToothBrush: { en: 'Toothbrush', hu: 'Fogkefe', de: 'Zahnbürste' },
  Deodorant: { en: 'Deodorant', hu: 'Dezodor', de: 'Deodorant' },
  Perfume: { en: 'Perfume', hu: 'Parfüm', de: 'Parfum' },
  ToiletPaper: { en: 'Toilet Paper', hu: 'Toalettpapír', de: 'Toilettenpapier' },
  FacialCare: { en: 'Facial Care', hu: 'Arcápolás', de: 'Gesichtspflege' },
  SkinCare: { en: 'Skin Care', hu: 'Bőrápolás', de: 'Hautpflege' },
  Moisturizer: { en: 'Moisturizer', hu: 'Hidratáló krém', de: 'Feuchtigkeitscreme' },
  Sunscreen: { en: 'Sunscreen', hu: 'Napozókrém', de: 'Sonnenschutz' },
  MakeUp: { en: 'Make-Up', hu: 'Smink', de: 'Make-up' },
  // `MentalCare` is a misnomer in the API enum; the C# comment reads
  // "Férfi ápolási termékek" (men's care), which is what the label follows.
  MentalCare: { en: 'Men\'s Care', hu: 'Férfi ápolás', de: 'Männerpflege' },
  WomensCare: { en: 'Women\'s Care', hu: 'Női higiénia', de: 'Damenhygiene' },
  HairColor: { en: 'Hair Color', hu: 'Hajfesték', de: 'Haarfarbe' },
  Razor: { en: 'Razor and Hair Removal', hu: 'Borotva és szőrtelenítés', de: 'Rasierer und Haarentfernung' },
  HairStyling: { en: 'Hair Styling', hu: 'Hajformázó', de: 'Haarstyling' },
  NailCare: { en: 'Nail Care', hu: 'Kézápolás és köröm', de: 'Nagelpflege' },

  // --- Cleaning ---
  Detergent: { en: 'Laundry Detergent', hu: 'Mosószer', de: 'Waschmittel' },
  SoftenerAndRinse: { en: 'Softener and Rinse Aid', hu: 'Öblítő és általános tisztítószer', de: 'Weichspüler und Klarspüler' },
  Bleach: { en: 'Bleach', hu: 'Fehérítő', de: 'Bleichmittel' },
  DishSoap: { en: 'Dish Soap', hu: 'Mosogatószer', de: 'Geschirrspülmittel' },
  WindowCleaner: { en: 'Window Cleaner', hu: 'Ablaktisztító', de: 'Glasreiniger' },
  Disinfectant: { en: 'Disinfectant', hu: 'Fertőtlenítőszer', de: 'Desinfektionsmittel' },
  AirFreshener: { en: 'Air Freshener', hu: 'Légfrissítő', de: 'Lufterfrischer' },
  Candle: { en: 'Scented Candle', hu: 'Illatgyertya', de: 'Duftkerze' },
  Deodorant_Home: { en: 'Room Deodorizer', hu: 'Szobadezodor', de: 'Raumdeodorant' },
  Laundry: { en: 'Laundry Accessories', hu: 'Mosási segédeszköz', de: 'Waschzubehör' },
  TrashBag: { en: 'Trash Bag', hu: 'Szemetes zsák', de: 'Müllbeutel' },
  AluminumFoil: { en: 'Aluminum Foil', hu: 'Alufólia', de: 'Alufolie' },
  PlasticWrap: { en: 'Plastic Wrap', hu: 'Frissentartó fólia', de: 'Frischhaltefolie' },
  FoodStorage: { en: 'Food Storage', hu: 'Ételtároló', de: 'Lebensmittelaufbewahrung' },
  PaperTowel: { en: 'Paper Towel', hu: 'Papírtörlő', de: 'Küchenrolle' },
  FloorCleaner: { en: 'Floor Cleaner', hu: 'Padlótisztító', de: 'Bodenreiniger' },
  BathroomCleaner: { en: 'Bathroom Cleaner', hu: 'Fürdőszobai tisztítószer', de: 'Badreiniger' },
  OvenCleaner: { en: 'Oven Cleaner', hu: 'Sütőtisztító', de: 'Backofenreiniger' },
  DrainCleaner: { en: 'Drain Cleaner', hu: 'Lefolyótisztító', de: 'Abflussreiniger' },
  Descaler: { en: 'Descaler', hu: 'Vízkőoldó', de: 'Entkalker' },

  // --- Kitchen ---
  Kitchen: { en: 'Kitchen Supplies', hu: 'Konyhai eszköz', de: 'Küchenbedarf' },
  Cookware: { en: 'Cookware', hu: 'Edény és főzőedény', de: 'Kochgeschirr' },
  Cutlery: { en: 'Cutlery', hu: 'Evőeszköz', de: 'Besteck' },
  Dinnerware: { en: 'Dinnerware', hu: 'Tányér és csésze', de: 'Geschirr' },
  Glass: { en: 'Drinking Glass', hu: 'Pohár', de: 'Trinkglas' },
  Utensil: { en: 'Kitchen Utensil', hu: 'Konyhai segédeszköz', de: 'Küchenhelfer' },
  Appliance: { en: 'Kitchen Appliance', hu: 'Konyhai gép', de: 'Küchengerät' },
  Bakeware: { en: 'Bakeware', hu: 'Sütőforma', de: 'Backform' },
  CuttingBoard: { en: 'Cutting Board', hu: 'Vágódeszka', de: 'Schneidebrett' },
  KitchenScale: { en: 'Kitchen Scale', hu: 'Konyhai mérleg', de: 'Küchenwaage' },

  // --- Home ---
  Furniture: { en: 'Furniture', hu: 'Bútor', de: 'Möbel' },
  Lighting: { en: 'Lighting', hu: 'Világítás', de: 'Beleuchtung' },
  Bedding: { en: 'Bedding', hu: 'Ágynemű', de: 'Bettwäsche' },
  Pillow: { en: 'Pillow', hu: 'Párna', de: 'Kopfkissen' },
  Blanket: { en: 'Blanket', hu: 'Takaró', de: 'Decke' },
  Sheet: { en: 'Bed Sheet', hu: 'Lepedő', de: 'Bettlaken' },
  Towel: { en: 'Towel', hu: 'Törölköző', de: 'Handtuch' },
  Curtain: { en: 'Curtain', hu: 'Függöny', de: 'Vorhang' },
  Rug: { en: 'Rug', hu: 'Szőnyeg', de: 'Teppich' },
  Storage: { en: 'Storage and Wardrobe', hu: 'Tárolás és szekrény', de: 'Aufbewahrung und Schrank' },
  Shelf: { en: 'Shelf', hu: 'Polc', de: 'Regal' },
  Decoration: { en: 'Decoration', hu: 'Dekoráció', de: 'Dekoration' },
  Mirror: { en: 'Mirror', hu: 'Tükör', de: 'Spiegel' },
  Artwork: { en: 'Artwork and Wall Decor', hu: 'Kép és faldekoráció', de: 'Bilder und Wanddeko' },
  Plant: { en: 'House Plant', hu: 'Házinövény', de: 'Zimmerpflanze' },
  Vase: { en: 'Vase', hu: 'Váza', de: 'Vase' },
  PictureFrame: { en: 'Picture Frame', hu: 'Képkeret', de: 'Bilderrahmen' },
  Clock: { en: 'Clock', hu: 'Óra', de: 'Uhr' },
  Doormat: { en: 'Doormat', hu: 'Lábtörlő', de: 'Fußmatte' },
  LaundryBasket: { en: 'Laundry Basket', hu: 'Szennyestartó', de: 'Wäschekorb' },

  // --- Tools ---
  Tool: { en: 'General Tool', hu: 'Általános szerszám', de: 'Allgemeines Werkzeug' },
  Hammer: { en: 'Hammer', hu: 'Kalapács', de: 'Hammer' },
  Screwdriver: { en: 'Screwdriver', hu: 'Csavarhúzó', de: 'Schraubendreher' },
  Wrench: { en: 'Wrench', hu: 'Csavarkulcs', de: 'Schraubenschlüssel' },
  Saw: { en: 'Saw', hu: 'Fűrész', de: 'Säge' },
  Drill: { en: 'Drill', hu: 'Fúró', de: 'Bohrmaschine' },
  Pliers: { en: 'Pliers', hu: 'Fogó', de: 'Zange' },
  Measuring: { en: 'Measuring Tool', hu: 'Mérőszerszám', de: 'Messwerkzeug' },
  LevelAndSquare: { en: 'Level and Square', hu: 'Vízmérték és derékszög', de: 'Wasserwaage und Winkel' },
  Clamp: { en: 'Clamp', hu: 'Szorító', de: 'Schraubzwinge' },
  ViseAndJig: { en: 'Vise and Jig', hu: 'Satu és befogó', de: 'Schraubstock und Spannvorrichtung' },
  PowerTool: { en: 'Power Tool', hu: 'Elektromos szerszám', de: 'Elektrowerkzeug' },
  Chisel: { en: 'Chisel', hu: 'Véső', de: 'Meißel' },
  FileTool: { en: 'File', hu: 'Reszelő', de: 'Feile' },
  UtilityKnife: { en: 'Utility Knife', hu: 'Sniccer', de: 'Cuttermesser' },
  StudFinder: { en: 'Stud Finder', hu: 'Falszkenner', de: 'Leitungssucher' },
  HeatGun: { en: 'Heat Gun', hu: 'Hőlégfúvó', de: 'Heißluftpistole' },
  Multimeter: { en: 'Multimeter', hu: 'Multiméter', de: 'Multimeter' },
  DrillBit: { en: 'Drill Bit', hu: 'Fúrószár', de: 'Bohrer' },
  SawBlade: { en: 'Saw Blade', hu: 'Fűrészlap', de: 'Sägeblatt' },

  // --- Garden ---
  Garden: { en: 'Garden Tools', hu: 'Kertészeti szerszám', de: 'Gartenwerkzeug' },
  Shovel: { en: 'Shovel', hu: 'Lapát', de: 'Schaufel' },
  Rake: { en: 'Rake', hu: 'Gereblye', de: 'Rechen' },
  Hoe: { en: 'Hoe', hu: 'Kapa', de: 'Hacke' },
  Pruning: { en: 'Pruning Tool', hu: 'Metszőszerszám', de: 'Schneidwerkzeug' },
  Watering: { en: 'Watering', hu: 'Öntözés', de: 'Bewässerung' },
  GardenGloves: { en: 'Garden Gloves', hu: 'Kertészkesztyű', de: 'Gartenhandschuhe' },
  Spade: { en: 'Spade', hu: 'Ásó', de: 'Spaten' },
  WateringCan: { en: 'Watering Can', hu: 'Öntözőkanna', de: 'Gießkanne' },
  GardenTwine: { en: 'Garden Twine', hu: 'Kertészzsineg', de: 'Gartenschnur' },

  // --- Electronics ---
  Electronics: { en: 'Electronics', hu: 'Elektronika', de: 'Elektronik' },
  Battery: { en: 'Battery', hu: 'Akkumulátor', de: 'Batterie' },
  Charger: { en: 'Charger', hu: 'Töltő', de: 'Ladegerät' },
  Cable: { en: 'Cable', hu: 'Kábel', de: 'Kabel' },
  Adapter: { en: 'Adapter', hu: 'Adapter', de: 'Adapter' },
  Bulb: { en: 'Light Bulb', hu: 'Izzó', de: 'Glühbirne' },
  ExtensionCord: { en: 'Extension Cord', hu: 'Hosszabbító', de: 'Verlängerungskabel' },
  PowerStrip: { en: 'Power Strip', hu: 'Elosztó', de: 'Steckdosenleiste' },
  Headphones: { en: 'Headphones', hu: 'Fejhallgató', de: 'Kopfhörer' },
  Speaker: { en: 'Speaker', hu: 'Hangszóró', de: 'Lautsprecher' },

  // --- Automotive ---
  Automotive: { en: 'Car Care', hu: 'Autóápolás', de: 'Autopflege' },
  EngineOil: { en: 'Engine Oil', hu: 'Motorolaj', de: 'Motoröl' },
  Coolant: { en: 'Coolant', hu: 'Hűtőfolyadék', de: 'Kühlflüssigkeit' },
  // `BrakeFl` is a truncated member name; the C# comment reads "Fékolaj".
  BrakeFl: { en: 'Brake Fluid', hu: 'Fékolaj', de: 'Bremsflüssigkeit' },
  Wiper: { en: 'Windshield Wiper', hu: 'Szélvédőtörlő lapát', de: 'Scheibenwischer' },
  TireCare: { en: 'Tire Care', hu: 'Gumiabroncs-ápolás', de: 'Reifenpflege' },
  CarWash: { en: 'Car Wash', hu: 'Autómosó', de: 'Autowäsche' },
  Wax: { en: 'Car Wax', hu: 'Viasz', de: 'Wachs' },
  WasherFluid: { en: 'Windshield Washer Fluid', hu: 'Szélvédőmosó folyadék', de: 'Scheibenwaschmittel' },
  CarAirFreshener: { en: 'Car Air Freshener', hu: 'Autóillatosító', de: 'Autoduft' },

  // --- Clothing ---
  Clothing: { en: 'Clothing', hu: 'Ruházat', de: 'Kleidung' },
  Shirt: { en: 'Shirt', hu: 'Ing', de: 'Hemd' },
  Pants: { en: 'Pants', hu: 'Nadrág', de: 'Hose' },
  Jacket: { en: 'Jacket', hu: 'Kabát', de: 'Jacke' },
  Sweater: { en: 'Sweater', hu: 'Pulóver', de: 'Pullover' },
  Dress: { en: 'Dress', hu: 'Ruha', de: 'Kleid' },
  Underwear: { en: 'Underwear', hu: 'Fehérnemű', de: 'Unterwäsche' },
  Socks: { en: 'Socks', hu: 'Zokni', de: 'Socken' },
  Shoes: { en: 'Shoes', hu: 'Cipő', de: 'Schuhe' },
  Hat: { en: 'Hat', hu: 'Kalap', de: 'Hut' },
  Scarf: { en: 'Scarf', hu: 'Sál', de: 'Schal' },
  Gloves: { en: 'Gloves', hu: 'Kesztyű', de: 'Handschuhe' },
  Belt: { en: 'Belt', hu: 'Öv', de: 'Gürtel' },
  Accessories: { en: 'Accessories', hu: 'Kiegészítők', de: 'Accessoires' },
  Suit: { en: 'Suit', hu: 'Öltöny', de: 'Anzug' },
  Skirt: { en: 'Skirt', hu: 'Szoknya', de: 'Rock' },
  Shorts: { en: 'Shorts', hu: 'Rövidnadrág', de: 'Shorts' },
  Swimwear: { en: 'Swimwear', hu: 'Fürdőruha', de: 'Badebekleidung' },
  Sportswear: { en: 'Sportswear', hu: 'Sportruha', de: 'Sportbekleidung' },
  Bag: { en: 'Bag', hu: 'Táska', de: 'Tasche' },

  // --- Entertainment ---
  Entertainment: { en: 'Entertainment', hu: 'Szórakoztatás', de: 'Unterhaltung' },
  Book: { en: 'Book', hu: 'Könyv', de: 'Buch' },
  Magazine: { en: 'Magazine', hu: 'Folyóirat', de: 'Zeitschrift' },
  Game: { en: 'Game', hu: 'Játék', de: 'Spiel' },
  Sport: { en: 'Sports Equipment', hu: 'Sporteszköz', de: 'Sportgerät' },
  Hobby: { en: 'Hobby Supplies', hu: 'Hobbianyag', de: 'Hobbybedarf' },
  Art: { en: 'Art Supplies', hu: 'Művészeti kellék', de: 'Künstlerbedarf' },
  Music: { en: 'Music and Audio', hu: 'Zene és hangrendszer', de: 'Musik und Audio' },
  Toy: { en: 'Children\'s Toy', hu: 'Gyermekjáték', de: 'Kinderspielzeug' },
  MusicalInstrument: { en: 'Musical Instrument', hu: 'Hangszer', de: 'Musikinstrument' },

  // --- Pets ---
  PetSupply: { en: 'Pet Supplies', hu: 'Háziállat-termék', de: 'Tierbedarf' },
  PetFood: { en: 'Pet Food', hu: 'Háziállat-táp', de: 'Tierfutter' },
  PetCare: { en: 'Pet Care', hu: 'Háziállat-ápolás', de: 'Tierpflege' },
  PetToy: { en: 'Pet Toy', hu: 'Háziállat-játék', de: 'Tierspielzeug' },
  PetBed: { en: 'Pet Bed', hu: 'Háziállat-fekhely', de: 'Tierbett' },
  PetLeash: { en: 'Pet Leash and Collar', hu: 'Póráz és nyakörv', de: 'Leine und Halsband' },
  PetLitter: { en: 'Cat Litter', hu: 'Alom', de: 'Katzenstreu' },
  PetBowl: { en: 'Pet Bowl', hu: 'Etetőtál', de: 'Futterschale' },
  PetCarrier: { en: 'Pet Carrier', hu: 'Szállítóbox', de: 'Transportbox' },
  AquariumSupply: { en: 'Aquarium Supplies', hu: 'Akváriumkellék', de: 'Aquarienbedarf' },

  // --- Office ---
  OfficeSupply: { en: 'Office Supplies', hu: 'Irodai kellék', de: 'Bürobedarf' },
  Paper: { en: 'Paper', hu: 'Papír', de: 'Papier' },
  Notebook: { en: 'Notebook', hu: 'Füzet', de: 'Notizbuch' },
  Pen: { en: 'Pen', hu: 'Toll', de: 'Kugelschreiber' },
  Pencil: { en: 'Pencil', hu: 'Ceruza', de: 'Bleistift' },
  Marker: { en: 'Marker', hu: 'Filctoll', de: 'Marker' },
  Eraser: { en: 'Eraser', hu: 'Radír', de: 'Radiergummi' },
  Tape: { en: 'Adhesive Tape', hu: 'Ragasztószalag', de: 'Klebeband' },
  Glue: { en: 'Glue', hu: 'Ragasztó', de: 'Klebstoff' },
  Stapler: { en: 'Stapler', hu: 'Tűzőgép', de: 'Hefter' },
  Scissors: { en: 'Scissors', hu: 'Olló', de: 'Schere' },
  Envelope: { en: 'Envelope', hu: 'Boríték', de: 'Briefumschlag' },
  PrinterInk: { en: 'Printer Ink', hu: 'Nyomtatótinta', de: 'Druckertinte' },
  PrinterToner: { en: 'Printer Toner', hu: 'Nyomtatótoner', de: 'Druckertoner' },
  Folder: { en: 'Folder', hu: 'Iratgyűjtő', de: 'Sammelmappe' },
  Binder: { en: 'Binder', hu: 'Gyűrűs mappa', de: 'Ringbuch' },
  StickyNote: { en: 'Sticky Note', hu: 'Öntapadó jegyzetlap', de: 'Haftnotiz' },
  Calculator: { en: 'Calculator', hu: 'Számológép', de: 'Taschenrechner' },
  Ruler: { en: 'Ruler', hu: 'Vonalzó', de: 'Lineal' },
  LabelMaker: { en: 'Label Maker', hu: 'Feliratozó', de: 'Beschriftungsgerät' },

  // --- BabyAndKids ---
  BabyDiaper: { en: 'Diaper', hu: 'Pelenka', de: 'Windel' },
  BabyWipes: { en: 'Baby Wipes', hu: 'Babatörlőkendő', de: 'Feuchttücher' },
  BabyFormula: { en: 'Baby Formula', hu: 'Tápszer', de: 'Babymilchnahrung' },
  BabyFood: { en: 'Baby Food', hu: 'Bébiétel', de: 'Babynahrung' },
  BabyCare: { en: 'Baby Care', hu: 'Babaápolás', de: 'Babypflege' },
  Nursing: { en: 'Nursing Supplies', hu: 'Szoptatási kellék', de: 'Stillzubehör' },
  Stroller: { en: 'Stroller', hu: 'Babakocsi', de: 'Kinderwagen' },
  CarSeat: { en: 'Car Seat', hu: 'Gyerekülés', de: 'Kindersitz' },
  BabyMonitor: { en: 'Baby Monitor', hu: 'Babaőrző', de: 'Babyphone' },
  BabyBottle: { en: 'Baby Bottle', hu: 'Cumisüveg', de: 'Babyflasche' },
  Pacifier: { en: 'Pacifier', hu: 'Cumi', de: 'Schnuller' },
  BabyBathTub: { en: 'Baby Bath Tub', hu: 'Babakád', de: 'Babybadewanne' },
  HighChair: { en: 'High Chair', hu: 'Etetőszék', de: 'Hochstuhl' },
  BabyCarrier: { en: 'Baby Carrier', hu: 'Babahordozó', de: 'Babytrage' },
  Crib: { en: 'Crib', hu: 'Kiságy', de: 'Kinderbett' },
  ChangingMat: { en: 'Changing Mat', hu: 'Pelenkázó lap', de: 'Wickelauflage' },
  BabyToy: { en: 'Baby Toy', hu: 'Babajáték', de: 'Babyspielzeug' },
  BabyClothing: { en: 'Baby Clothing', hu: 'Babaruha', de: 'Babykleidung' },
  Potty: { en: 'Potty', hu: 'Bili', de: 'Töpfchen' },
  SafetyGate: { en: 'Safety Gate', hu: 'Biztonsági rács', de: 'Türschutzgitter' },

  // --- Cleaning ---
  Broom: { en: 'Broom', hu: 'Seprű', de: 'Besen' },
  Mop: { en: 'Mop', hu: 'Felmosó', de: 'Wischmopp' },
  Bucket: { en: 'Bucket', hu: 'Vödör', de: 'Eimer' },
  Dustpan: { en: 'Dustpan', hu: 'Szemétlapát', de: 'Kehrschaufel' },
  Squeegee: { en: 'Squeegee', hu: 'Lehúzó', de: 'Abzieher' },
  Sponge: { en: 'Sponge', hu: 'Szivacs', de: 'Schwamm' },
  Brush: { en: 'Brush', hu: 'Kefe', de: 'Bürste' },
  Scrubber: { en: 'Scrubber', hu: 'Súroló', de: 'Scheuerschwamm' },
  Vacuum: { en: 'Vacuum Cleaner', hu: 'Porszívó', de: 'Staubsauger' },
  VacuumBag: { en: 'Vacuum Bag', hu: 'Porszívózsák', de: 'Staubsaugerbeutel' },
  Duster: { en: 'Duster', hu: 'Portörlő', de: 'Staubwedel' },
  MicrofiberCloth: { en: 'Microfiber Cloth', hu: 'Mikroszálas kendő', de: 'Mikrofasertuch' },
  CleaningGloves: { en: 'Cleaning Gloves', hu: 'Gumikesztyű', de: 'Putzhandschuhe' },
  ToiletBrush: { en: 'Toilet Brush', hu: 'WC-kefe', de: 'Toilettenbürste' },
  Plunger: { en: 'Plunger', hu: 'Duguláselhárító pumpa', de: 'Saugglocke' },
  CarpetCleaner: { en: 'Carpet Cleaner', hu: 'Szőnyegtisztító', de: 'Teppichreiniger' },
  SteamCleaner: { en: 'Steam Cleaner', hu: 'Gőztisztító', de: 'Dampfreiniger' },
  LintRoller: { en: 'Lint Roller', hu: 'Szöszroller', de: 'Fusselrolle' },
  Clothesline: { en: 'Clothesline', hu: 'Szárítókötél', de: 'Wäscheleine' },
  ClothesPeg: { en: 'Clothes Peg', hu: 'Ruhacsipesz', de: 'Wäscheklammer' },

  // --- PestControl ---
  InsectRepellent: { en: 'Insect Repellent', hu: 'Rovarriasztó', de: 'Insektenschutzmittel' },
  InsectTrap: { en: 'Insect Trap', hu: 'Rovarcsapda', de: 'Insektenfalle' },
  RodentTrap: { en: 'Rodent Trap', hu: 'Rágcsálócsapda', de: 'Nagerfalle' },
  Pesticide: { en: 'Pesticide', hu: 'Növényvédő szer', de: 'Pflanzenschutzmittel' },
  MothRepellent: { en: 'Moth Repellent', hu: 'Molyirtó', de: 'Mottenschutz' },
  AntBait: { en: 'Ant Bait', hu: 'Hangyacsalétek', de: 'Ameisenköder' },
  WaspTrap: { en: 'Wasp Trap', hu: 'Darázscsapda', de: 'Wespenfalle' },
  MoleRepellent: { en: 'Mole Repellent', hu: 'Vakondriasztó', de: 'Maulwurfabwehr' },
  BirdRepellent: { en: 'Bird Repellent', hu: 'Madárriasztó', de: 'Vogelabwehr' },
  SlugPellet: { en: 'Slug Pellets', hu: 'Csigaölő granulátum', de: 'Schneckenkorn' },

  // --- SmartHome ---
  SmartHome: { en: 'Smart Home Devices', hu: 'Okosotthon-eszköz', de: 'Smart-Home-Geräte' },
  SmartBulb: { en: 'Smart Bulb', hu: 'Okos izzó', de: 'Smarte Glühbirne' },
  SmartPlug: { en: 'Smart Plug', hu: 'Okos dugalj', de: 'Smarte Steckdose' },
  DoorLock: { en: 'Smart Door Lock', hu: 'Okos zár', de: 'Smartes Türschloss' },
  SmokeDetector: { en: 'Smoke Detector', hu: 'Füstérzékelő', de: 'Rauchmelder' },
  CarbonMonoxideDetector: { en: 'Carbon Monoxide Detector', hu: 'Szén-monoxid-érzékelő', de: 'Kohlenmonoxidmelder' },
  Alarm: { en: 'Alarm', hu: 'Riasztó', de: 'Alarmanlage' },
  Camera: { en: 'Security Camera', hu: 'Biztonsági kamera', de: 'Sicherheitskamera' },
  SmartDoorbell: { en: 'Smart Doorbell', hu: 'Okos ajtócsengő', de: 'Smarte Türklingel' },
  MotionSensor: { en: 'Motion Sensor', hu: 'Mozgásérzékelő', de: 'Bewegungsmelder' },
  OpeningSensor: { en: 'Door and Window Sensor', hu: 'Nyitásérzékelő', de: 'Tür-Fenster-Sensor' },
  WaterLeakSensor: { en: 'Water Leak Sensor', hu: 'Vízszivárgás-érzékelő', de: 'Wassersensor' },
  SmartRadiatorValve: { en: 'Smart Radiator Valve', hu: 'Okos radiátorszelep', de: 'Smartes Heizkörperthermostat' },
  SmartHub: { en: 'Smart Hub', hu: 'Okos központ', de: 'Smart-Home-Zentrale' },
  SmartSpeaker: { en: 'Smart Speaker', hu: 'Okos hangszóró', de: 'Smarter Lautsprecher' },
  SmartSwitch: { en: 'Smart Switch', hu: 'Okos kapcsoló', de: 'Smarter Schalter' },
  SmartBlind: { en: 'Smart Blind', hu: 'Okos árnyékoló', de: 'Smarter Rollladen' },
  SmartIrrigationController: { en: 'Smart Irrigation Controller', hu: 'Okos öntözésvezérlő', de: 'Smarte Bewässerungssteuerung' },
  SmartScale: { en: 'Smart Scale', hu: 'Okos mérleg', de: 'Smarte Waage' },
  SafeBox: { en: 'Safe', hu: 'Széf', de: 'Safe' },

  // --- Appliances ---
  AirPurifier: { en: 'Air Purifier', hu: 'Légtisztító', de: 'Luftreiniger' },
  Humidifier: { en: 'Humidifier', hu: 'Párásító', de: 'Luftbefeuchter' },
  Dehumidifier: { en: 'Dehumidifier', hu: 'Párátlanító', de: 'Luftentfeuchter' },
  Fan: { en: 'Fan', hu: 'Ventilátor', de: 'Ventilator' },
  Heater: { en: 'Heater', hu: 'Hősugárzó', de: 'Heizgerät' },
  WaterFilter: { en: 'Water Filter', hu: 'Vízszűrő', de: 'Wasserfilter' },
  WaterSoftener: { en: 'Water Softener', hu: 'Vízlágyító', de: 'Wasserentkalkungsanlage' },
  AirFilter: { en: 'Air Filter', hu: 'Légszűrő', de: 'Luftfilter' },
  AromaDiffuser: { en: 'Aroma Diffuser', hu: 'Aroma diffúzor', de: 'Aromadiffusor' },
  Hygrometer: { en: 'Hygrometer', hu: 'Páratartalom-mérő', de: 'Hygrometer' },
  AirQualityMonitor: { en: 'Air Quality Monitor', hu: 'Levegőminőség-mérő', de: 'Luftqualitätsmessgerät' },
  WaterTestKit: { en: 'Water Test Kit', hu: 'Vízteszt készlet', de: 'Wassertestset' },

  // --- Kitchen ---
  CoffeeMaker: { en: 'Coffee Maker', hu: 'Kávéfőző', de: 'Kaffeemaschine' },
  Kettle: { en: 'Kettle', hu: 'Vízforraló', de: 'Wasserkocher' },
  Toaster: { en: 'Toaster', hu: 'Kenyérpirító', de: 'Toaster' },
  Blender: { en: 'Blender', hu: 'Turmixgép', de: 'Standmixer' },
  Mixer: { en: 'Stand Mixer', hu: 'Konyhai robotgép', de: 'Küchenmaschine' },
  FoodProcessor: { en: 'Food Processor', hu: 'Aprítógép', de: 'Zerkleinerer' },
  SlowCooker: { en: 'Slow Cooker', hu: 'Lassúfőző', de: 'Schongarer' },
  RiceCooker: { en: 'Rice Cooker', hu: 'Rizsfőző', de: 'Reiskocher' },
  AirFryer: { en: 'Air Fryer', hu: 'Forrólevegős fritőz', de: 'Heißluftfritteuse' },
  DeepFryer: { en: 'Deep Fryer', hu: 'Olajsütő', de: 'Fritteuse' },
  SandwichMaker: { en: 'Sandwich Maker', hu: 'Szendvicssütő', de: 'Sandwichmaker' },
  WaffleMaker: { en: 'Waffle Maker', hu: 'Gofrisütő', de: 'Waffeleisen' },
  EggCooker: { en: 'Egg Cooker', hu: 'Tojásfőző', de: 'Eierkocher' },
  ElectricGrill: { en: 'Electric Grill', hu: 'Elektromos grill', de: 'Elektrogrill' },
  Juicer: { en: 'Juicer', hu: 'Gyümölcscentrifuga', de: 'Entsafter' },
  CoffeeGrinder: { en: 'Coffee Grinder', hu: 'Kávédaráló', de: 'Kaffeemühle' },
  MilkFrother: { en: 'Milk Frother', hu: 'Habosító', de: 'Milchaufschäumer' },
  SodaMaker: { en: 'Soda Maker', hu: 'Szódagép', de: 'Wassersprudler' },
  BreadMaker: { en: 'Bread Maker', hu: 'Kenyérsütő gép', de: 'Brotbackautomat' },
  IceCreamMaker: { en: 'Ice Cream Maker', hu: 'Jégkrémkészítő', de: 'Eismaschine' },

  // --- Appliances ---
  Refrigerator: { en: 'Refrigerator', hu: 'Hűtőszekrény', de: 'Kühlschrank' },
  Freezer: { en: 'Freezer', hu: 'Fagyasztó', de: 'Gefriertruhe' },
  Oven: { en: 'Oven', hu: 'Sütő', de: 'Backofen' },
  Cooktop: { en: 'Cooktop', hu: 'Főzőlap', de: 'Kochfeld' },
  Microwave: { en: 'Microwave', hu: 'Mikrohullámú sütő', de: 'Mikrowelle' },
  Dishwasher: { en: 'Dishwasher', hu: 'Mosogatógép', de: 'Geschirrspüler' },
  WashingMachine: { en: 'Washing Machine', hu: 'Mosógép', de: 'Waschmaschine' },
  Dryer: { en: 'Dryer', hu: 'Szárítógép', de: 'Wäschetrockner' },
  WasherDryer: { en: 'Washer Dryer', hu: 'Mosó-szárítógép', de: 'Waschtrockner' },
  Stove: { en: 'Stove', hu: 'Tűzhely', de: 'Herd' },
  RangeHood: { en: 'Range Hood', hu: 'Páraelszívó', de: 'Dunstabzugshaube' },
  WineCooler: { en: 'Wine Cooler', hu: 'Borhűtő', de: 'Weinkühlschrank' },
  WaterHeater: { en: 'Water Heater', hu: 'Vízmelegítő', de: 'Warmwasserbereiter' },
  Boiler: { en: 'Boiler', hu: 'Kazán', de: 'Heizkessel' },
  HeatPump: { en: 'Heat Pump', hu: 'Hőpumpa', de: 'Wärmepumpe' },
  SolarPanel: { en: 'Solar Panel', hu: 'Napelem', de: 'Solarmodul' },
  Generator: { en: 'Generator', hu: 'Áramfejlesztő', de: 'Stromerzeuger' },
  SumpPump: { en: 'Sump Pump', hu: 'Búvárszivattyú', de: 'Tauchpumpe' },
  SepticTreatment: { en: 'Septic Treatment', hu: 'Szennyvízkezelő adalék', de: 'Kläranlagenzusatz' },
  SoftenerSalt: { en: 'Softener Salt', hu: 'Regeneráló só', de: 'Regeneriersalz' },

  // --- Electronics ---
  ComputerAccessory: { en: 'Computer Accessory', hu: 'Számítógép-kiegészítő', de: 'Computerzubehör' },
  Keyboard: { en: 'Keyboard', hu: 'Billentyűzet', de: 'Tastatur' },
  Mouse: { en: 'Mouse', hu: 'Egér', de: 'Maus' },
  Monitor: { en: 'Monitor', hu: 'Monitor', de: 'Monitor' },
  USBDrive: { en: 'USB Drive', hu: 'USB-meghajtó', de: 'USB-Stick' },
  ExternalHDD: { en: 'External Hard Drive', hu: 'Külső merevlemez', de: 'Externe Festplatte' },
  PhoneAccessory: { en: 'Phone Accessory', hu: 'Telefon-kiegészítő', de: 'Handyzubehör' },
  ScreenProtector: { en: 'Screen Protector', hu: 'Kijelzővédő', de: 'Displayschutz' },
  PhoneCase: { en: 'Phone Case', hu: 'Telefontok', de: 'Handyhülle' },
  ChargerCable: { en: 'Charger Cable', hu: 'Töltőkábel', de: 'Ladekabel' },
  Laptop: { en: 'Laptop', hu: 'Laptop', de: 'Laptop' },
  TabletDevice: { en: 'Tablet', hu: 'Táblagép', de: 'Tablet' },
  DesktopComputer: { en: 'Desktop Computer', hu: 'Asztali számítógép', de: 'Desktop-PC' },
  Printer: { en: 'Printer', hu: 'Nyomtató', de: 'Drucker' },
  Scanner: { en: 'Scanner', hu: 'Lapolvasó', de: 'Scanner' },
  Router: { en: 'Router', hu: 'Router', de: 'Router' },
  NetworkSwitch: { en: 'Network Switch', hu: 'Hálózati switch', de: 'Netzwerk-Switch' },
  NetworkStorage: { en: 'Network Storage', hu: 'Hálózati tároló', de: 'Netzwerkspeicher' },
  UPSDevice: { en: 'Uninterruptible Power Supply', hu: 'Szünetmentes tápegység', de: 'USV-Gerät' },
  Webcam: { en: 'Webcam', hu: 'Webkamera', de: 'Webcam' },
  CameraLens: { en: 'Camera Lens', hu: 'Objektív', de: 'Objektiv' },
  Tripod: { en: 'Tripod', hu: 'Állvány', de: 'Stativ' },
  MemoryCard: { en: 'Memory Card', hu: 'Memóriakártya', de: 'Speicherkarte' },
  ActionCamera: { en: 'Action Camera', hu: 'Sportkamera', de: 'Actionkamera' },
  CameraBag: { en: 'Camera Bag', hu: 'Fotós táska', de: 'Kameratasche' },
  CameraBattery: { en: 'Camera Battery', hu: 'Kamera akkumulátor', de: 'Kameraakku' },
  StudioLighting: { en: 'Studio Lighting', hu: 'Fotós világítás', de: 'Studiobeleuchtung' },
  Drone: { en: 'Drone', hu: 'Drón', de: 'Drohne' },
  Gimbal: { en: 'Gimbal', hu: 'Gimbal', de: 'Gimbal' },
  LensFilter: { en: 'Lens Filter', hu: 'Objektívszűrő', de: 'Objektivfilter' },

  // --- PartyAndSeasonal ---
  PartySupply: { en: 'Party Supplies', hu: 'Partikellék', de: 'Partybedarf' },
  BirthdaySupply: { en: 'Birthday Supplies', hu: 'Szülinapi kellék', de: 'Geburtstagsbedarf' },
  SeasonalDecoration: { en: 'Seasonal Decoration', hu: 'Szezonális dekoráció', de: 'Saisonale Dekoration' },
  ChristmasDecoration: { en: 'Christmas Decoration', hu: 'Karácsonyi dekoráció', de: 'Weihnachtsdekoration' },
  HalloweenDecoration: { en: 'Halloween Decoration', hu: 'Halloween-dekoráció', de: 'Halloween-Dekoration' },
  Balloon: { en: 'Balloon', hu: 'Ballon', de: 'Luftballon' },
  GiftWrap: { en: 'Gift Wrap', hu: 'Csomagolópapír', de: 'Geschenkpapier' },
  GreetingCard: { en: 'Greeting Card', hu: 'Üdvözlőkártya', de: 'Grußkarte' },
  DisposableTableware: { en: 'Disposable Tableware', hu: 'Egyszer használatos étkészlet', de: 'Einweggeschirr' },
  Fireworks: { en: 'Fireworks', hu: 'Tűzijáték', de: 'Feuerwerk' },

  // --- Travel ---
  Luggage: { en: 'Luggage', hu: 'Bőrönd', de: 'Koffer' },
  TravelAccessory: { en: 'Travel Accessory', hu: 'Utazási kiegészítő', de: 'Reisezubehör' },
  TravelBottle: { en: 'Travel Bottle', hu: 'Utazópalack', de: 'Reiseflasche' },
  PassportHolder: { en: 'Passport Holder', hu: 'Útlevéltok', de: 'Passhülle' },
  Backpack: { en: 'Backpack', hu: 'Hátizsák', de: 'Rucksack' },
  TravelPillow: { en: 'Travel Pillow', hu: 'Utazópárna', de: 'Reisekissen' },
  PackingCube: { en: 'Packing Cube', hu: 'Bőröndrendező', de: 'Packtasche' },
  TravelAdapter: { en: 'Travel Adapter', hu: 'Utazó adapter', de: 'Reiseadapter' },
  LuggageScale: { en: 'Luggage Scale', hu: 'Bőröndmérleg', de: 'Kofferwaage' },
  TravelLock: { en: 'Travel Lock', hu: 'Bőröndzár', de: 'Kofferschloss' },

  // --- Safety ---
  Flashlight: { en: 'Flashlight', hu: 'Zseblámpa', de: 'Taschenlampe' },
  Headlamp: { en: 'Headlamp', hu: 'Fejlámpa', de: 'Stirnlampe' },
  Matches: { en: 'Matches', hu: 'Gyufa', de: 'Streichhölzer' },
  Lighter: { en: 'Lighter', hu: 'Öngyújtó', de: 'Feuerzeug' },
  FireExtinguisher: { en: 'Fire Extinguisher', hu: 'Tűzoltó készülék', de: 'Feuerlöscher' },
  FirstAidKit: { en: 'First Aid Kit', hu: 'Elsősegélycsomag', de: 'Erste-Hilfe-Kasten' },
  PPEMask: { en: 'Protective Mask', hu: 'Védőmaszk', de: 'Schutzmaske' },
  SafetyGlasses: { en: 'Safety Glasses', hu: 'Védőszemüveg', de: 'Schutzbrille' },
  WorkGloves: { en: 'Work Gloves', hu: 'Munkavédelmi kesztyű', de: 'Arbeitshandschuhe' },
  EarProtection: { en: 'Ear Protection', hu: 'Fülvédő', de: 'Gehörschutz' },
  HardHat: { en: 'Hard Hat', hu: 'Védősisak', de: 'Schutzhelm' },
  SafetyVest: { en: 'Safety Vest', hu: 'Láthatósági mellény', de: 'Warnweste' },
  Respirator: { en: 'Respirator', hu: 'Légzésvédő maszk', de: 'Atemschutzmaske' },
  KneePad: { en: 'Knee Pad', hu: 'Térdvédő', de: 'Knieschoner' },
  FallProtection: { en: 'Fall Protection', hu: 'Zuhanásgátló', de: 'Absturzsicherung' },
  EmergencyRadio: { en: 'Emergency Radio', hu: 'Vészhelyzeti rádió', de: 'Notfallradio' },
  EmergencyBlanket: { en: 'Emergency Blanket', hu: 'Hőtakaró', de: 'Rettungsdecke' },
  WaterPurificationTablet: { en: 'Water Purification Tablet', hu: 'Víztisztító tabletta', de: 'Wasserentkeimungstablette' },
  EmergencyFood: { en: 'Emergency Food', hu: 'Tartalék élelmiszer', de: 'Notvorrat' },
  FireBlanket: { en: 'Fire Blanket', hu: 'Tűzoltó takaró', de: 'Löschdecke' },

  // --- Health ---
  Thermometer: { en: 'Thermometer', hu: 'Lázmérő', de: 'Fieberthermometer' },
  BloodPressureMonitor: { en: 'Blood Pressure Monitor', hu: 'Vérnyomásmérő', de: 'Blutdruckmessgerät' },
  Glucometer: { en: 'Glucometer', hu: 'Vércukormérő', de: 'Blutzuckermessgerät' },
  PulseOximeter: { en: 'Pulse Oximeter', hu: 'Pulzoximéter', de: 'Pulsoximeter' },
  HeatingPad: { en: 'Heating Pad', hu: 'Melegítő párna', de: 'Heizkissen' },
  Massager: { en: 'Massager', hu: 'Masszírozó', de: 'Massagegerät' },
  BraceSupport: { en: 'Brace and Support', hu: 'Támasztó és merevítő', de: 'Bandage und Stütze' },
  // Umbrella category; the specific Crutch/Walker/Wheelchair members came later.
  MobilityAid: { en: 'Mobility Aid', hu: 'Mozgássegítő', de: 'Mobilitätshilfe' },
  Nebulizer: { en: 'Nebulizer', hu: 'Inhalátor', de: 'Inhalator' },
  Crutch: { en: 'Crutch', hu: 'Mankó', de: 'Krücke' },
  Walker: { en: 'Walker', hu: 'Járókeret', de: 'Gehgestell' },
  Wheelchair: { en: 'Wheelchair', hu: 'Kerekesszék', de: 'Rollstuhl' },
  HospitalBed: { en: 'Hospital Bed', hu: 'Betegágy', de: 'Pflegebett' },
  PillOrganizer: { en: 'Pill Organizer', hu: 'Tablettatartó', de: 'Tablettenbox' },
  MedicalGloves: { en: 'Medical Gloves', hu: 'Vizsgálókesztyű', de: 'Untersuchungshandschuhe' },
  Syringe: { en: 'Syringe', hu: 'Fecskendő', de: 'Spritze' },
  IcePack: { en: 'Ice Pack', hu: 'Hűtőtasak', de: 'Kühlkompresse' },
  CompressionStocking: { en: 'Compression Stocking', hu: 'Kompressziós harisnya', de: 'Kompressionsstrumpf' },
  BodyScale: { en: 'Body Scale', hu: 'Személymérleg', de: 'Personenwaage' },
  SharpsContainer: { en: 'Sharps Container', hu: 'Éles hulladék gyűjtő', de: 'Abwurfbehälter' },
  SexualWellness: { en: 'Sexual Wellness', hu: 'Szexuális egészség', de: 'Sexuelle Gesundheit' },
  Condom: { en: 'Condom', hu: 'Óvszer', de: 'Kondom' },
  Lubricant: { en: 'Lubricant', hu: 'Síkosító', de: 'Gleitmittel' },
  IntimateWash: { en: 'Intimate Wash', hu: 'Intim mosakodó', de: 'Intimwaschlotion' },
  PregnancyTest: { en: 'Pregnancy Test', hu: 'Terhességi teszt', de: 'Schwangerschaftstest' },
  Contraceptive: { en: 'Contraceptive', hu: 'Fogamzásgátló', de: 'Kontrazeptivum' },
  VisionCare: { en: 'Vision Care', hu: 'Látásápolás', de: 'Sehhilfenpflege' },
  EyeglassCleaner: { en: 'Eyeglass Cleaner', hu: 'Szemüvegtisztító', de: 'Brillenreiniger' },
  ContactLens: { en: 'Contact Lens', hu: 'Kontaktlencse', de: 'Kontaktlinse' },
  HearingAid: { en: 'Hearing Aid', hu: 'Hallókészülék', de: 'Hörgerät' },
  Eyeglasses: { en: 'Eyeglasses', hu: 'Szemüveg', de: 'Brille' },
  Sunglasses: { en: 'Sunglasses', hu: 'Napszemüveg', de: 'Sonnenbrille' },
  LensSolution: { en: 'Contact Lens Solution', hu: 'Kontaktlencse-folyadék', de: 'Kontaktlinsenlösung' },
  EyeDrops: { en: 'Eye Drops', hu: 'Szemcsepp', de: 'Augentropfen' },
  HearingAidBattery: { en: 'Hearing Aid Battery', hu: 'Hallókészülék-elem', de: 'Hörgerätebatterie' },
  EarPlugs: { en: 'Ear Plugs', hu: 'Füldugó', de: 'Ohrstöpsel' },
  FootCare: { en: 'Foot Care', hu: 'Lábápolás', de: 'Fußpflege' },
  Insoles: { en: 'Insoles', hu: 'Talpbetét', de: 'Einlegesohlen' },
  FootFile: { en: 'Foot File', hu: 'Lábreszelő', de: 'Hornhautfeile' },
  ShoePolish: { en: 'Shoe Polish', hu: 'Cipőkrém', de: 'Schuhcreme' },
  HeelPad: { en: 'Heel Pad', hu: 'Sarokbetét', de: 'Fersenpolster' },
  ShoeLace: { en: 'Shoe Lace', hu: 'Cipőfűző', de: 'Schnürsenkel' },
  ShoeBrush: { en: 'Shoe Brush', hu: 'Cipőkefe', de: 'Schuhbürste' },
  ShoeTree: { en: 'Shoe Tree', hu: 'Sámfa', de: 'Schuhspanner' },
  CompressionSock: { en: 'Compression Sock', hu: 'Kompressziós zokni', de: 'Kompressionssocke' },
  AntifungalTreatment: { en: 'Antifungal Treatment', hu: 'Gombaellenes készítmény', de: 'Antimykotikum' },

  // --- Entertainment ---
  SewingKit: { en: 'Sewing Kit', hu: 'Varrókészlet', de: 'Nähset' },
  YarnAndKnitting: { en: 'Yarn and Knitting', hu: 'Fonál és kötés', de: 'Wolle und Stricken' },
  CraftSupply: { en: 'Craft Supplies', hu: 'Kézműves kellék', de: 'Bastelbedarf' },
  Fabric: { en: 'Fabric', hu: 'Textil', de: 'Stoff' },
  SewingMachine: { en: 'Sewing Machine', hu: 'Varrógép', de: 'Nähmaschine' },
  Needle: { en: 'Needle', hu: 'Tű', de: 'Nadel' },
  Thread: { en: 'Thread', hu: 'Cérna', de: 'Nähfaden' },
  Button: { en: 'Button', hu: 'Gomb', de: 'Knopf' },
  Zipper: { en: 'Zipper', hu: 'Cipzár', de: 'Reißverschluss' },
  Ribbon: { en: 'Ribbon', hu: 'Díszszalag', de: 'Zierband' },
  Bead: { en: 'Bead', hu: 'Gyöngy', de: 'Perle' },
  Felt: { en: 'Felt', hu: 'Filclap', de: 'Filz' },
  ArtBrush: { en: 'Art Brush', hu: 'Művészecset', de: 'Künstlerpinsel' },
  Canvas: { en: 'Canvas', hu: 'Festővászon', de: 'Leinwand' },
  ModelingClay: { en: 'Modeling Clay', hu: 'Gyurma', de: 'Modelliermasse' },
  Sketchbook: { en: 'Sketchbook', hu: 'Rajzfüzet', de: 'Skizzenbuch' },
  CraftGlue: { en: 'Craft Glue', hu: 'Kézműves ragasztó', de: 'Bastelkleber' },
  CraftKnife: { en: 'Craft Knife', hu: 'Makettvágó', de: 'Bastelmesser' },
  CuttingMat: { en: 'Cutting Mat', hu: 'Vágóalátét', de: 'Schneidematte' },
  CraftPaint: { en: 'Craft Paint', hu: 'Hobbifesték', de: 'Bastelfarbe' },

  // --- HomeImprovement ---
  Paint: { en: 'Paint', hu: 'Festék', de: 'Farbe' },
  Primer: { en: 'Primer', hu: 'Alapozó', de: 'Grundierung' },
  BrushRoller: { en: 'Brush and Roller', hu: 'Ecset és festőhenger', de: 'Pinsel und Rolle' },
  PainterTape: { en: 'Painter\'s Tape', hu: 'Festőszalag', de: 'Malerband' },
  Wallpaper: { en: 'Wallpaper', hu: 'Tapéta', de: 'Tapete' },
  WallpaperPaste: { en: 'Wallpaper Paste', hu: 'Tapétaragasztó', de: 'Tapetenkleister' },
  Varnish: { en: 'Varnish', hu: 'Lakk', de: 'Lack' },
  WoodStain: { en: 'Wood Stain', hu: 'Pácolóanyag', de: 'Holzlasur' },
  WallFiller: { en: 'Wall Filler', hu: 'Glett', de: 'Spachtelmasse' },
  Putty: { en: 'Putty', hu: 'Kitt', de: 'Kitt' },
  CaulkGun: { en: 'Caulk Gun', hu: 'Kinyomópisztoly', de: 'Kartuschenpistole' },
  PaintTray: { en: 'Paint Tray', hu: 'Festőtálca', de: 'Farbwanne' },
  DropCloth: { en: 'Drop Cloth', hu: 'Takarófólia', de: 'Abdeckfolie' },
  SprayPaint: { en: 'Spray Paint', hu: 'Festékspray', de: 'Sprühfarbe' },
  PaintThinner: { en: 'Paint Thinner', hu: 'Hígító', de: 'Verdünner' },
  Sander: { en: 'Sander', hu: 'Csiszológép', de: 'Schleifmaschine' },
  SandingBlock: { en: 'Sanding Block', hu: 'Csiszolótalp', de: 'Schleifklotz' },
  PaintScraper: { en: 'Paint Scraper', hu: 'Festéklehúzó', de: 'Farbschaber' },
  WireBrush: { en: 'Wire Brush', hu: 'Drótkefe', de: 'Drahtbürste' },
  Trowel: { en: 'Trowel', hu: 'Simítókanál', de: 'Glättkelle' },
  Plumbing: { en: 'Plumbing', hu: 'Vízvezeték-szerelvény', de: 'Sanitärinstallation' },
  PipeFitting: { en: 'Pipe Fitting', hu: 'Idom', de: 'Rohrfitting' },
  Faucet: { en: 'Faucet', hu: 'Csaptelep', de: 'Wasserhahn' },
  ShowerHead: { en: 'Shower Head', hu: 'Zuhanyfej', de: 'Duschkopf' },
  PipeInsulation: { en: 'Pipe Insulation', hu: 'Csőszigetelés', de: 'Rohrisolierung' },
  PlumbersTape: { en: 'Plumber\'s Tape', hu: 'Teflonszalag', de: 'Gewindedichtband' },
  Siphon: { en: 'Siphon', hu: 'Szifon', de: 'Siphon' },
  ToiletCistern: { en: 'Toilet Cistern', hu: 'WC-tartály', de: 'Spülkasten' },
  ShutoffValve: { en: 'Shutoff Valve', hu: 'Elzárószelep', de: 'Absperrventil' },
  FlexibleConnector: { en: 'Flexible Connector', hu: 'Bekötőcső', de: 'Anschlussschlauch' },
  Electrical: { en: 'Electrical Fittings', hu: 'Elektromos szerelvény', de: 'Elektroinstallation' },
  Switch: { en: 'Switch', hu: 'Kapcsoló', de: 'Schalter' },
  Outlet: { en: 'Outlet', hu: 'Konnektor', de: 'Steckdose' },
  Fuse: { en: 'Fuse', hu: 'Biztosíték', de: 'Sicherung' },
  Wire: { en: 'Wire', hu: 'Vezeték', de: 'Leitung' },
  JunctionBox: { en: 'Junction Box', hu: 'Kötődoboz', de: 'Abzweigdose' },
  CircuitBreaker: { en: 'Circuit Breaker', hu: 'Kismegszakító', de: 'Leitungsschutzschalter' },
  WireConnector: { en: 'Wire Connector', hu: 'Sorkapocs', de: 'Klemme' },
  CableConduit: { en: 'Cable Conduit', hu: 'Védőcső', de: 'Kabelkanal' },
  VoltageTester: { en: 'Voltage Tester', hu: 'Fázisceruza', de: 'Spannungsprüfer' },
  AirConditioner: { en: 'Air Conditioner', hu: 'Légkondicionáló', de: 'Klimaanlage' },
  Radiator: { en: 'Radiator', hu: 'Radiátor', de: 'Heizkörper' },
  Thermostat: { en: 'Thermostat', hu: 'Szobatermosztát', de: 'Raumthermostat' },
  AirVent: { en: 'Air Vent', hu: 'Szellőzőrács', de: 'Lüftungsgitter' },
  AirDuct: { en: 'Air Duct', hu: 'Légcsatorna', de: 'Luftkanal' },
  ChimneyPart: { en: 'Chimney Part', hu: 'Kéménytartozék', de: 'Kaminzubehör' },
  StoveFan: { en: 'Stove Fan', hu: 'Kandallóventilátor', de: 'Kaminventilator' },
  RadiatorValve: { en: 'Radiator Valve', hu: 'Radiátorszelep', de: 'Heizkörperventil' },
  CondensatePump: { en: 'Condensate Pump', hu: 'Kondenzvíz-pumpa', de: 'Kondensatpumpe' },
  UnderfloorHeating: { en: 'Underfloor Heating', hu: 'Padlófűtés elem', de: 'Fußbodenheizung' },
  Insulation: { en: 'Insulation', hu: 'Szigetelőanyag', de: 'Dämmstoff' },
  WeatherStripping: { en: 'Weather Stripping', hu: 'Ajtószigetelés', de: 'Dichtungsband' },
  WindowFilm: { en: 'Window Film', hu: 'Ablakfólia', de: 'Fensterfolie' },
  Blinds: { en: 'Blinds', hu: 'Reluxa', de: 'Jalousie' },
  MosquitoNet: { en: 'Mosquito Net', hu: 'Szúnyogháló', de: 'Fliegengitter' },
  AtticLadder: { en: 'Attic Ladder', hu: 'Padlásfeljáró', de: 'Dachbodentreppe' },
  Firewood: { en: 'Firewood', hu: 'Tűzifa', de: 'Brennholz' },
  WoodPellet: { en: 'Wood Pellet', hu: 'Pellet', de: 'Holzpellets' },
  Coal: { en: 'Coal', hu: 'Szén', de: 'Kohle' },
  GasCylinder: { en: 'Gas Cylinder', hu: 'Gázpalack', de: 'Gasflasche' },

  // --- Garden ---
  OutdoorFurniture: { en: 'Outdoor Furniture', hu: 'Kerti bútor', de: 'Gartenmöbel' },
  PatioUmbrella: { en: 'Patio Umbrella', hu: 'Napernyő', de: 'Sonnenschirm' },
  GardenDecor: { en: 'Garden Decor', hu: 'Kerti dekoráció', de: 'Gartendeko' },
  BirdFeeder: { en: 'Bird Feeder', hu: 'Madáretető', de: 'Vogelhäuschen' },
  Hammock: { en: 'Hammock', hu: 'Függőágy', de: 'Hängematte' },
  Swing: { en: 'Swing', hu: 'Hinta', de: 'Schaukel' },
  Sandbox: { en: 'Sandbox', hu: 'Homokozó', de: 'Sandkasten' },
  Playhouse: { en: 'Playhouse', hu: 'Játszóház', de: 'Spielhaus' },
  Trampoline: { en: 'Trampoline', hu: 'Trambulin', de: 'Trampolin' },
  SwimmingPool: { en: 'Swimming Pool', hu: 'Medence', de: 'Swimmingpool' },
  PoolChemical: { en: 'Pool Chemical', hu: 'Medence vegyszer', de: 'Poolchemie' },
  PoolPump: { en: 'Pool Pump', hu: 'Medenceszivattyú', de: 'Poolpumpe' },
  PoolCover: { en: 'Pool Cover', hu: 'Medencetakaró', de: 'Poolabdeckung' },
  PoolToy: { en: 'Pool Toy', hu: 'Medencejáték', de: 'Wasserspielzeug' },
  OutdoorLighting: { en: 'Outdoor Lighting', hu: 'Kültéri világítás', de: 'Außenbeleuchtung' },
  SolarLight: { en: 'Solar Light', hu: 'Napelemes lámpa', de: 'Solarleuchte' },
  FirePit: { en: 'Fire Pit', hu: 'Tűzrakóhely', de: 'Feuerschale' },
  PatioHeater: { en: 'Patio Heater', hu: 'Kültéri hősugárzó', de: 'Terrassenheizer' },
  GardenStatue: { en: 'Garden Statue', hu: 'Kerti szobor', de: 'Gartenfigur' },
  Fountain: { en: 'Fountain', hu: 'Szökőkút', de: 'Springbrunnen' },
  Grill: { en: 'Grill', hu: 'Grill', de: 'Grill' },
  GrillAccessory: { en: 'Grill Accessory', hu: 'Grill-kiegészítő', de: 'Grillzubehör' },
  Charcoal: { en: 'Charcoal', hu: 'Faszén', de: 'Holzkohle' },
  Propane: { en: 'Propane Cylinder', hu: 'Propánpalack', de: 'Propangasflasche' },
  GrillBrush: { en: 'Grill Brush', hu: 'Grillkefe', de: 'Grillbürste' },
  GrillCover: { en: 'Grill Cover', hu: 'Grilltakaró', de: 'Grillabdeckung' },
  Skewer: { en: 'Skewer', hu: 'Nyárs', de: 'Grillspieß' },
  GrillThermometer: { en: 'Grill Thermometer', hu: 'Grill hőmérő', de: 'Grillthermometer' },
  SmokingChips: { en: 'Smoking Chips', hu: 'Füstölőforgács', de: 'Räucherchips' },
  FireStarter: { en: 'Fire Starter', hu: 'Tűzgyújtó', de: 'Grillanzünder' },
  GrillGrate: { en: 'Grill Grate', hu: 'Grillrács', de: 'Grillrost' },
  Rotisserie: { en: 'Rotisserie', hu: 'Forgónyárs', de: 'Drehspieß' },
  Smoker: { en: 'Smoker', hu: 'Füstölő', de: 'Räucherofen' },
  PizzaOven: { en: 'Pizza Oven', hu: 'Kemence', de: 'Pizzaofen' },
  GrillTongs: { en: 'Grill Tongs', hu: 'Grillfogó', de: 'Grillzange' },
  BastingBrush: { en: 'Basting Brush', hu: 'Kenőecset', de: 'Backpinsel' },
  GrillBasket: { en: 'Grill Basket', hu: 'Grillkosár', de: 'Grillkorb' },
  GrillMat: { en: 'Grill Mat', hu: 'Grillszőnyeg', de: 'Grillmatte' },
  CharcoalChimney: { en: 'Charcoal Chimney', hu: 'Faszéngyújtó kémény', de: 'Anzündkamin' },
  PropaneRegulator: { en: 'Propane Regulator', hu: 'Gázreduktor', de: 'Gasdruckregler' },
  Seeds: { en: 'Seeds', hu: 'Mag', de: 'Saatgut' },
  Soil: { en: 'Potting Soil', hu: 'Virágföld', de: 'Blumenerde' },
  Fertilizer: { en: 'Fertilizer', hu: 'Műtrágya', de: 'Dünger' },
  PlanterPot: { en: 'Planter Pot', hu: 'Cserép', de: 'Blumentopf' },
  Hose: { en: 'Garden Hose', hu: 'Locsolótömlő', de: 'Gartenschlauch' },
  Sprinkler: { en: 'Sprinkler', hu: 'Szórófej', de: 'Rasensprinkler' },
  LawnMower: { en: 'Lawn Mower', hu: 'Fűnyíró', de: 'Rasenmäher' },
  Trimmer: { en: 'Edge Trimmer', hu: 'Szegélynyíró', de: 'Rasentrimmer' },
  PlantBulb: { en: 'Plant Bulb', hu: 'Virághagyma', de: 'Blumenzwiebel' },
  Seedling: { en: 'Seedling', hu: 'Palánta', de: 'Setzling' },
  SeedTray: { en: 'Seed Tray', hu: 'Magvető tálca', de: 'Anzuchtschale' },
  GrowLight: { en: 'Grow Light', hu: 'Növénylámpa', de: 'Pflanzenlampe' },
  Greenhouse: { en: 'Greenhouse', hu: 'Üvegház', de: 'Gewächshaus' },
  ColdFrame: { en: 'Cold Frame', hu: 'Melegágy', de: 'Frühbeet' },
  PlantLabel: { en: 'Plant Label', hu: 'Növénycímke', de: 'Pflanzenschild' },
  PruningSealant: { en: 'Pruning Sealant', hu: 'Sebkezelő', de: 'Wundverschluss' },
  GraftingSupply: { en: 'Grafting Supplies', hu: 'Szemzőkellék', de: 'Veredelungsbedarf' },
  SoilTestKit: { en: 'Soil Test Kit', hu: 'Talajteszt', de: 'Bodentestset' },
  SoilConditioner: { en: 'Soil Conditioner', hu: 'Talajjavító', de: 'Bodenverbesserer' },
  Perlite: { en: 'Perlite', hu: 'Perlit', de: 'Perlit' },
  Peat: { en: 'Peat', hu: 'Tőzeg', de: 'Torf' },
  BarkMulch: { en: 'Bark Mulch', hu: 'Kéregmulcs', de: 'Rindenmulch' },
  DecorativeGravel: { en: 'Decorative Gravel', hu: 'Díszkavics', de: 'Zierkies' },
  PavingStone: { en: 'Paving Stone', hu: 'Járdalap', de: 'Gehwegplatte' },
  GardenBorder: { en: 'Garden Border', hu: 'Ágyásszegély', de: 'Beeteinfassung' },
  RaisedBed: { en: 'Raised Bed', hu: 'Emelt ágyás', de: 'Hochbeet' },
  CompostAccelerator: { en: 'Compost Accelerator', hu: 'Komposztgyorsító', de: 'Kompostbeschleuniger' },
  GardenSieve: { en: 'Garden Sieve', hu: 'Kertészszita', de: 'Gartensieb' },
  Dibber: { en: 'Dibber', hu: 'Ültetőfa', de: 'Pflanzholz' },
  HarvestBasket: { en: 'Harvest Basket', hu: 'Szedőkosár', de: 'Erntekorb' },

  // --- SportsAndLeisure ---
  Tent: { en: 'Tent', hu: 'Sátor', de: 'Zelt' },
  SleepingBag: { en: 'Sleeping Bag', hu: 'Hálózsák', de: 'Schlafsack' },
  CampingMat: { en: 'Camping Mat', hu: 'Kempingmatrac', de: 'Isomatte' },
  CampingCookware: { en: 'Camping Cookware', hu: 'Kemping főzőeszköz', de: 'Campingkochgeschirr' },
  Cooler: { en: 'Cooler', hu: 'Hűtőláda', de: 'Kühlbox' },
  CampingChair: { en: 'Camping Chair', hu: 'Kempingszék', de: 'Campingstuhl' },
  CampingTable: { en: 'Camping Table', hu: 'Kempingasztal', de: 'Campingtisch' },
  CampingStove: { en: 'Camping Stove', hu: 'Kempingfőző', de: 'Campingkocher' },
  CampingLantern: { en: 'Camping Lantern', hu: 'Kempinglámpa', de: 'Campinglaterne' },
  GasCartridge: { en: 'Gas Cartridge', hu: 'Gázpatron', de: 'Gaskartusche' },
  WaterCanister: { en: 'Water Canister', hu: 'Víztartály', de: 'Wasserkanister' },
  Tarp: { en: 'Tarp', hu: 'Ponyva', de: 'Plane' },
  CampingAxe: { en: 'Camping Axe', hu: 'Balta', de: 'Beil' },
  PocketKnife: { en: 'Pocket Knife', hu: 'Bicska', de: 'Taschenmesser' },
  Compass: { en: 'Compass', hu: 'Kompasz', de: 'Kompass' },
  GPSDevice: { en: 'GPS Device', hu: 'GPS készülék', de: 'GPS-Gerät' },
  TrekkingPole: { en: 'Trekking Pole', hu: 'Trekking bot', de: 'Trekkingstock' },
  CampingShower: { en: 'Camping Shower', hu: 'Kempingzuhany', de: 'Campingdusche' },
  PortableToilet: { en: 'Portable Toilet', hu: 'Mobil WC', de: 'Campingtoilette' },
  TentRepairKit: { en: 'Tent Repair Kit', hu: 'Sátorjavító készlet', de: 'Zeltreparaturset' },
  FitnessEquipment: { en: 'Fitness Equipment', hu: 'Fitneszeszköz', de: 'Fitnessgerät' },
  YogaMat: { en: 'Yoga Mat', hu: 'Jógaszőnyeg', de: 'Yogamatte' },
  Dumbbell: { en: 'Dumbbell', hu: 'Súlyzó', de: 'Hantel' },
  ResistanceBand: { en: 'Resistance Band', hu: 'Erősítő gumiszalag', de: 'Widerstandsband' },
  Treadmill: { en: 'Treadmill', hu: 'Futópad', de: 'Laufband' },
  ExerciseBike: { en: 'Exercise Bike', hu: 'Szobakerékpár', de: 'Heimtrainer' },
  RowingMachine: { en: 'Rowing Machine', hu: 'Evezőgép', de: 'Rudergerät' },
  EllipticalTrainer: { en: 'Elliptical Trainer', hu: 'Elliptikus tréner', de: 'Crosstrainer' },
  WeightBench: { en: 'Weight Bench', hu: 'Súlyzópad', de: 'Hantelbank' },
  Barbell: { en: 'Barbell', hu: 'Rúdsúlyzó', de: 'Langhantel' },
  Kettlebell: { en: 'Kettlebell', hu: 'Kettlebell', de: 'Kettlebell' },
  JumpRope: { en: 'Jump Rope', hu: 'Ugrálókötél', de: 'Springseil' },
  PullUpBar: { en: 'Pull-Up Bar', hu: 'Húzódzkodó rúd', de: 'Klimmzugstange' },
  FoamRoller: { en: 'Foam Roller', hu: 'SMR henger', de: 'Faszienrolle' },
  ExerciseBall: { en: 'Exercise Ball', hu: 'Fitneszlabda', de: 'Gymnastikball' },
  FitnessTracker: { en: 'Fitness Tracker', hu: 'Aktivitásmérő', de: 'Fitnesstracker' },
  SportsDrink: { en: 'Sports Drink', hu: 'Sportital', de: 'Sportgetränk' },
  ProteinSupplement: { en: 'Protein Supplement', hu: 'Fehérjepor', de: 'Proteinpulver' },
  SportsTape: { en: 'Sports Tape', hu: 'Sporttapasz', de: 'Sporttape' },
  BoxingGlove: { en: 'Boxing Glove', hu: 'Boxkesztyű', de: 'Boxhandschuh' },
  Racket: { en: 'Racket', hu: 'Ütő', de: 'Schläger' },
  Ball: { en: 'Ball', hu: 'Labda', de: 'Ball' },
  SkiEquipment: { en: 'Ski Equipment', hu: 'Sífelszerelés', de: 'Skiausrüstung' },
  Snowboard: { en: 'Snowboard', hu: 'Snowboard', de: 'Snowboard' },
  IceSkates: { en: 'Ice Skates', hu: 'Korcsolya', de: 'Schlittschuhe' },
  Skateboard: { en: 'Skateboard', hu: 'Gördeszka', de: 'Skateboard' },
  SwimGear: { en: 'Swim Gear', hu: 'Úszófelszerelés', de: 'Schwimmausrüstung' },
  FishingGear: { en: 'Fishing Gear', hu: 'Horgászfelszerelés', de: 'Angelausrüstung' },
  HuntingGear: { en: 'Hunting Gear', hu: 'Vadászfelszerelés', de: 'Jagdausrüstung' },
  ClimbingGear: { en: 'Climbing Gear', hu: 'Hegymászó felszerelés', de: 'Kletterausrüstung' },
  BicycleAccessory: { en: 'Bicycle Accessory', hu: 'Kerékpár-kiegészítő', de: 'Fahrradzubehör' },
  Helmet: { en: 'Helmet', hu: 'Bukósisak', de: 'Helm' },
  Pump: { en: 'Pump', hu: 'Pumpa', de: 'Luftpumpe' },
  BicycleTire: { en: 'Bicycle Tire', hu: 'Kerékpárgumi', de: 'Fahrradreifen' },
  BicycleTube: { en: 'Bicycle Tube', hu: 'Belső gumi', de: 'Fahrradschlauch' },
  BicycleChain: { en: 'Bicycle Chain', hu: 'Kerékpárlánc', de: 'Fahrradkette' },
  BicycleBrake: { en: 'Bicycle Brake', hu: 'Kerékpárfék', de: 'Fahrradbremse' },
  BicycleLight: { en: 'Bicycle Light', hu: 'Kerékpárlámpa', de: 'Fahrradlicht' },
  BicycleLock: { en: 'Bicycle Lock', hu: 'Kerékpárzár', de: 'Fahrradschloss' },
  BicycleRack: { en: 'Bicycle Rack', hu: 'Kerékpártartó', de: 'Fahrradträger' },
  BicycleSaddle: { en: 'Bicycle Saddle', hu: 'Kerékpárülés', de: 'Fahrradsattel' },
  BottleCage: { en: 'Bottle Cage', hu: 'Kulacstartó', de: 'Flaschenhalter' },
  BicyclePannier: { en: 'Bicycle Pannier', hu: 'Kerékpártáska', de: 'Fahrradtasche' },
  BicycleComputer: { en: 'Bicycle Computer', hu: 'Kerékpáros computer', de: 'Fahrradcomputer' },
  BicycleRepairKit: { en: 'Bicycle Repair Kit', hu: 'Kerékpárjavító készlet', de: 'Fahrradflickzeug' },
  BicycleGrease: { en: 'Bicycle Grease', hu: 'Kerékpárzsír', de: 'Fahrradfett' },
  ChildBikeSeat: { en: 'Child Bike Seat', hu: 'Kerékpáros gyerekülés', de: 'Fahrradkindersitz' },
  BikeTrailer: { en: 'Bike Trailer', hu: 'Kerékpár-utánfutó', de: 'Fahrradanhänger' },
  EBikeBattery: { en: 'E-Bike Battery', hu: 'E-bike akkumulátor', de: 'E-Bike-Akku' },
  Scooter: { en: 'Scooter', hu: 'Roller', de: 'Roller' },

  // --- Entertainment ---
  BoardGame: { en: 'Board Game', hu: 'Társasjáték', de: 'Brettspiel' },
  Puzzle: { en: 'Puzzle', hu: 'Kirakó', de: 'Puzzle' },
  CardGame: { en: 'Card Game', hu: 'Kártyajáték', de: 'Kartenspiel' },
  VideoGame: { en: 'Video Game', hu: 'Videojáték', de: 'Videospiel' },
  GameConsole: { en: 'Game Console', hu: 'Játékkonzol', de: 'Spielkonsole' },
  GameController: { en: 'Game Controller', hu: 'Kontroller', de: 'Controller' },
  BuildingBlocks: { en: 'Building Blocks', hu: 'Építőkocka', de: 'Bausteine' },
  Doll: { en: 'Doll', hu: 'Baba', de: 'Puppe' },
  RideOnToy: { en: 'Ride-On Toy', hu: 'Járgány', de: 'Rutscher' },
  OutdoorToy: { en: 'Outdoor Toy', hu: 'Kültéri játék', de: 'Außenspielzeug' },

  // --- BabyAndKids ---
  SchoolBag: { en: 'School Bag', hu: 'Iskolatáska', de: 'Schultasche' },
  LunchBox: { en: 'Lunch Box', hu: 'Uzsonnásdoboz', de: 'Brotdose' },
  WaterBottle: { en: 'Water Bottle', hu: 'Kulacs', de: 'Trinkflasche' },
  PencilCase: { en: 'Pencil Case', hu: 'Tolltartó', de: 'Federmappe' },
  Textbook: { en: 'Textbook', hu: 'Tankönyv', de: 'Schulbuch' },
  GeometrySet: { en: 'Geometry Set', hu: 'Körző és vonalzó készlet', de: 'Geometrieset' },
  Watercolor: { en: 'Watercolor', hu: 'Vízfesték', de: 'Wasserfarbe' },
  Crayon: { en: 'Crayon', hu: 'Zsírkréta', de: 'Wachsmalstift' },
  Chalk: { en: 'Chalk', hu: 'Kréta', de: 'Kreide' },
  Globe: { en: 'Globe', hu: 'Földgömb', de: 'Globus' },

  // --- Garden ---
  GardenEdger: { en: 'Garden Edger', hu: 'Szegélyvágó', de: 'Kantenschneider' },
  WeedControl: { en: 'Weed Control', hu: 'Gyomirtás kelléke', de: 'Unkrautbekämpfung' },
  WeedKiller: { en: 'Weed Killer', hu: 'Gyomirtó szer', de: 'Unkrautvernichter' },
  Mulch: { en: 'Mulch', hu: 'Mulcs', de: 'Mulch' },
  Compost: { en: 'Compost', hu: 'Komposzt', de: 'Kompost' },
  CompostBin: { en: 'Compost Bin', hu: 'Komposztáló', de: 'Komposter' },
  RainBarrel: { en: 'Rain Barrel', hu: 'Esővízgyűjtő hordó', de: 'Regentonne' },
  IrrigationTimer: { en: 'Irrigation Timer', hu: 'Öntözésidőzítő', de: 'Bewässerungscomputer' },
  HoseNozzle: { en: 'Hose Nozzle', hu: 'Tömlőfej', de: 'Schlauchdüse' },
  GardenSprayer: { en: 'Garden Sprayer', hu: 'Permetező', de: 'Drucksprüher' },
  Wheelbarrow: { en: 'Wheelbarrow', hu: 'Talicska', de: 'Schubkarre' },
  GardenCart: { en: 'Garden Cart', hu: 'Kerti kocsi', de: 'Gartenwagen' },
  Trellis: { en: 'Trellis', hu: 'Növényrács', de: 'Rankgitter' },
  PlantStake: { en: 'Plant Stake', hu: 'Növénytámasz', de: 'Pflanzstab' },
  PlantNetting: { en: 'Plant Netting', hu: 'Növényháló', de: 'Pflanzennetz' },
  GardenFencing: { en: 'Garden Fencing', hu: 'Kerti kerítés', de: 'Gartenzaun' },
  LawnAerator: { en: 'Lawn Aerator', hu: 'Gyepszellőztető', de: 'Rasenlüfter' },
  Dethatcher: { en: 'Dethatcher', hu: 'Gyepfilc-eltávolító', de: 'Vertikutierer' },
  SeedSpreader: { en: 'Seed Spreader', hu: 'Szórókocsi', de: 'Streuwagen' },
  LeafBlower: { en: 'Leaf Blower', hu: 'Lombfúvó', de: 'Laubbläser' },
  HedgeTrimmer: { en: 'Hedge Trimmer', hu: 'Sövényvágó', de: 'Heckenschere' },
  Chainsaw: { en: 'Chainsaw', hu: 'Láncfűrész', de: 'Kettensäge' },

  // --- Tools ---
  WoodRouter: { en: 'Wood Router', hu: 'Felsőmaró', de: 'Oberfräse' },
  Planer: { en: 'Planer', hu: 'Gyalu', de: 'Hobel' },
  Jigsaw: { en: 'Jigsaw', hu: 'Dekopírfűrész', de: 'Stichsäge' },
  AngleGrinder: { en: 'Angle Grinder', hu: 'Sarokcsiszoló', de: 'Winkelschleifer' },
  MiterSaw: { en: 'Miter Saw', hu: 'Gérvágó', de: 'Kappsäge' },
  TableSaw: { en: 'Table Saw', hu: 'Asztali körfűrész', de: 'Tischkreissäge' },
  Lathe: { en: 'Lathe', hu: 'Eszterga', de: 'Drehmaschine' },
  CNCMachine: { en: 'CNC Machine', hu: 'CNC gép', de: 'CNC-Maschine' },
  Workbench: { en: 'Workbench', hu: 'Munkapad', de: 'Werkbank' },
  ToolChest: { en: 'Tool Chest', hu: 'Szerszámszekrény', de: 'Werkzeugschrank' },
  Toolbox: { en: 'Toolbox', hu: 'Szerszámosláda', de: 'Werkzeugkasten' },
  PegboardHook: { en: 'Pegboard Hook', hu: 'Perforált fali kampó', de: 'Lochwandhaken' },
  MagneticTray: { en: 'Magnetic Tray', hu: 'Mágneses tálca', de: 'Magnetschale' },
  Ladder: { en: 'Ladder', hu: 'Létra', de: 'Leiter' },
  StepStool: { en: 'Step Stool', hu: 'Fellépő', de: 'Trittleiter' },
  FloorMat: { en: 'Floor Mat', hu: 'Padlóvédő szőnyeg', de: 'Bodenschutzmatte' },
  AntiFatigueMat: { en: 'Anti-Fatigue Mat', hu: 'Fáradásgátló szőnyeg', de: 'Anti-Ermüdungsmatte' },
  ShopVac: { en: 'Shop Vacuum', hu: 'Ipari porszívó', de: 'Industriestaubsauger' },
  PressureWasher: { en: 'Pressure Washer', hu: 'Magasnyomású mosó', de: 'Hochdruckreiniger' },
  AirCompressor: { en: 'Air Compressor', hu: 'Kompresszor', de: 'Kompressor' },
  TireInflator: { en: 'Tire Inflator', hu: 'Kerékfelfújó', de: 'Reifenfüller' },
  JumperCables: { en: 'Jumper Cables', hu: 'Bikázókábel', de: 'Starthilfekabel' },
  BatteryCharger: { en: 'Battery Charger', hu: 'Akkumulátortöltő', de: 'Batterieladegerät' },
  CarJack: { en: 'Car Jack', hu: 'Autóemelő', de: 'Wagenheber' },
  JackStand: { en: 'Jack Stand', hu: 'Alátámasztó bak', de: 'Unterstellbock' },
  GarageDoorOpener: { en: 'Garage Door Opener', hu: 'Garázskapu-nyitó', de: 'Garagentorantrieb' },
  Padlock: { en: 'Padlock', hu: 'Lakat', de: 'Vorhängeschloss' },
  Chain: { en: 'Chain', hu: 'Lánc', de: 'Kette' },
  BungeeCord: { en: 'Bungee Cord', hu: 'Gumipók', de: 'Spanngummi' },
  Rope: { en: 'Rope', hu: 'Kötél', de: 'Seil' },
  ZipTie: { en: 'Zip Tie', hu: 'Kábelkötöző', de: 'Kabelbinder' },
  DuctTape: { en: 'Duct Tape', hu: 'Szövetszalag', de: 'Gewebeband' },
  ElectricalTape: { en: 'Electrical Tape', hu: 'Szigetelőszalag', de: 'Isolierband' },
  MaskingTape: { en: 'Masking Tape', hu: 'Maszkolószalag', de: 'Kreppband' },
  Fasteners: { en: 'Fasteners', hu: 'Kötőelem', de: 'Verbindungselemente' },
  Nails: { en: 'Nails', hu: 'Szög', de: 'Nägel' },
  Screws: { en: 'Screws', hu: 'Csavar', de: 'Schrauben' },
  Bolts: { en: 'Bolts', hu: 'Hatlapfejű csavar', de: 'Sechskantschrauben' },
  Nuts: { en: 'Hex Nuts', hu: 'Anya', de: 'Muttern' },
  Washers: { en: 'Washers', hu: 'Alátét', de: 'Unterlegscheiben' },
  Sandpaper: { en: 'Sandpaper', hu: 'Csiszolópapír', de: 'Schleifpapier' },
  SolderingIron: { en: 'Soldering Iron', hu: 'Forrasztópáka', de: 'Lötkolben' },
  WeldingEquipment: { en: 'Welding Equipment', hu: 'Hegesztőeszköz', de: 'Schweißgerät' },
  EpoxyAdhesive: { en: 'Epoxy Adhesive', hu: 'Epoxi ragasztó', de: 'Epoxidkleber' },
  SiliconeSealant: { en: 'Silicone Sealant', hu: 'Szilikon tömítő', de: 'Silikondichtmasse' },
  Degreaser: { en: 'Degreaser', hu: 'Zsírtalanító', de: 'Entfetter' },
  BrakeCleaner: { en: 'Brake Cleaner', hu: 'Féktisztító', de: 'Bremsenreiniger' },
  PenetratingOil: { en: 'Penetrating Oil', hu: 'Rozsdaoldó', de: 'Rostlöser' },

  // --- Bathroom ---
  BathroomFurniture: { en: 'Bathroom Furniture', hu: 'Fürdőszobabútor', de: 'Badmöbel' },
  BathroomMirror: { en: 'Bathroom Mirror', hu: 'Fürdőszobatükör', de: 'Badspiegel' },
  ShowerCurtain: { en: 'Shower Curtain', hu: 'Zuhanyfüggöny', de: 'Duschvorhang' },
  ShowerEnclosure: { en: 'Shower Enclosure', hu: 'Zuhanykabin', de: 'Duschkabine' },
  Bathtub: { en: 'Bathtub', hu: 'Kád', de: 'Badewanne' },
  ShowerTray: { en: 'Shower Tray', hu: 'Zuhanytálca', de: 'Duschtasse' },
  Toilet: { en: 'Toilet', hu: 'WC', de: 'Toilette' },
  ToiletSeat: { en: 'Toilet Seat', hu: 'WC-ülőke', de: 'Toilettensitz' },
  Bidet: { en: 'Bidet', hu: 'Bidé', de: 'Bidet' },
  WashBasin: { en: 'Wash Basin', hu: 'Mosdókagyló', de: 'Waschbecken' },
  BathMat: { en: 'Bath Mat', hu: 'Fürdőszobaszőnyeg', de: 'Badematte' },
  TowelRail: { en: 'Towel Rail', hu: 'Törölközőtartó', de: 'Handtuchhalter' },
  ToiletPaperHolder: { en: 'Toilet Paper Holder', hu: 'WC-papír tartó', de: 'Toilettenpapierhalter' },
  SoapDispenser: { en: 'Soap Dispenser', hu: 'Szappanadagoló', de: 'Seifenspender' },
  ToothbrushHolder: { en: 'Toothbrush Holder', hu: 'Fogkefetartó', de: 'Zahnputzbecher' },
  BathroomShelf: { en: 'Bathroom Shelf', hu: 'Fürdőszobapolc', de: 'Badregal' },
  ShowerCaddy: { en: 'Shower Caddy', hu: 'Zuhanypolc', de: 'Duschkorb' },
  BathPillow: { en: 'Bath Pillow', hu: 'Fürdőpárna', de: 'Badewannenkissen' },
  BathSalt: { en: 'Bath Salt', hu: 'Fürdősó', de: 'Badesalz' },
  BubbleBath: { en: 'Bubble Bath', hu: 'Habfürdő', de: 'Schaumbad' },
  BathToy: { en: 'Bath Toy', hu: 'Fürdőjáték', de: 'Badespielzeug' },
  ShowerHose: { en: 'Shower Hose', hu: 'Zuhanycső', de: 'Duschschlauch' },
  FaucetAerator: { en: 'Faucet Aerator', hu: 'Perlátor', de: 'Strahlregler' },
  DrainStrainer: { en: 'Drain Strainer', hu: 'Lefolyószűrő', de: 'Abflusssieb' },
  ToiletFreshener: { en: 'Toilet Freshener', hu: 'WC-illatosító', de: 'WC-Duftspüler' },
  BathroomHeater: { en: 'Bathroom Heater', hu: 'Fürdőszobai fűtő', de: 'Badheizung' },

  // --- PersonalCare ---
  HairDryer: { en: 'Hair Dryer', hu: 'Hajszárító', de: 'Haartrockner' },
  HairStraightener: { en: 'Hair Straightener', hu: 'Hajvasaló', de: 'Haarglätter' },
  ElectricShaver: { en: 'Electric Shaver', hu: 'Villanyborotva', de: 'Elektrorasierer' },
  Epilator: { en: 'Epilator', hu: 'Epilátor', de: 'Epilierer' },

  // --- HomeImprovement ---
  BuildingMaterial: { en: 'Building Material', hu: 'Építőanyag', de: 'Baumaterial' },
  Cement: { en: 'Cement', hu: 'Cement', de: 'Zement' },
  Mortar: { en: 'Mortar', hu: 'Habarcs', de: 'Mörtel' },
  Concrete: { en: 'Concrete', hu: 'Beton', de: 'Beton' },
  Sand: { en: 'Sand', hu: 'Homok', de: 'Sand' },
  Gravel: { en: 'Gravel', hu: 'Sóder', de: 'Schotter' },
  Brick: { en: 'Brick', hu: 'Tégla', de: 'Ziegel' },
  ConcreteBlock: { en: 'Concrete Block', hu: 'Zsalukő', de: 'Schalungsstein' },
  Drywall: { en: 'Drywall', hu: 'Gipszkarton', de: 'Gipskartonplatte' },
  DrywallCompound: { en: 'Drywall Compound', hu: 'Gipszkarton glett', de: 'Fugenspachtel' },
  DrywallTape: { en: 'Drywall Tape', hu: 'Gipszkarton szalag', de: 'Fugenband' },
  DrywallAnchor: { en: 'Drywall Anchor', hu: 'Gipszkarton dűbel', de: 'Gipskartondübel' },
  Lumber: { en: 'Lumber', hu: 'Fűrészáru', de: 'Schnittholz' },
  Plywood: { en: 'Plywood', hu: 'Rétegelt lemez', de: 'Sperrholz' },
  OSBBoard: { en: 'OSB Board', hu: 'OSB lap', de: 'OSB-Platte' },
  MDFBoard: { en: 'MDF Board', hu: 'MDF lap', de: 'MDF-Platte' },
  Beam: { en: 'Beam', hu: 'Gerenda', de: 'Balken' },
  MetalProfile: { en: 'Metal Profile', hu: 'Fémprofil', de: 'Metallprofil' },
  RebarSteel: { en: 'Rebar', hu: 'Betonvas', de: 'Betonstahl' },
  WireMesh: { en: 'Wire Mesh', hu: 'Hegesztett háló', de: 'Baustahlmatte' },
  RoofTile: { en: 'Roof Tile', hu: 'Tetőcserép', de: 'Dachziegel' },
  RoofingMembrane: { en: 'Roofing Membrane', hu: 'Tetőfólia', de: 'Dachbahn' },
  Gutter: { en: 'Gutter', hu: 'Ereszcsatorna', de: 'Dachrinne' },
  Downspout: { en: 'Downspout', hu: 'Lefolyócső', de: 'Fallrohr' },
  Flashing: { en: 'Flashing', hu: 'Szegélylemez', de: 'Kantenblech' },
  Tile: { en: 'Tile', hu: 'Csempe', de: 'Fliese' },
  TileAdhesive: { en: 'Tile Adhesive', hu: 'Csemperagasztó', de: 'Fliesenkleber' },
  TileGrout: { en: 'Tile Grout', hu: 'Fugázó', de: 'Fugenmasse' },
  TileSpacer: { en: 'Tile Spacer', hu: 'Fugakereszt', de: 'Fliesenkreuz' },
  Flooring: { en: 'Flooring', hu: 'Padlóburkolat', de: 'Bodenbelag' },
  Laminate: { en: 'Laminate', hu: 'Laminált padló', de: 'Laminat' },
  Parquet: { en: 'Parquet', hu: 'Parketta', de: 'Parkett' },
  VinylFlooring: { en: 'Vinyl Flooring', hu: 'Vinyl padló', de: 'Vinylboden' },
  Carpeting: { en: 'Carpeting', hu: 'Padlószőnyeg', de: 'Teppichboden' },
  FloorUnderlay: { en: 'Floor Underlay', hu: 'Aljzatszigetelés', de: 'Trittschalldämmung' },
  Baseboard: { en: 'Baseboard', hu: 'Szegélyléc', de: 'Sockelleiste' },
  DecorativeTrim: { en: 'Decorative Trim', hu: 'Díszléc', de: 'Dekorleiste' },
  Threshold: { en: 'Threshold', hu: 'Küszöb', de: 'Türschwelle' },
  ExpandingFoam: { en: 'Expanding Foam', hu: 'Purhab', de: 'Bauschaum' },
  Waterproofing: { en: 'Waterproofing', hu: 'Vízszigetelés', de: 'Bauwerksabdichtung' },
  Door: { en: 'Door', hu: 'Ajtó', de: 'Tür' },
  Window: { en: 'Window', hu: 'Ablak', de: 'Fenster' },
  DoorHandle: { en: 'Door Handle', hu: 'Ajtókilincs', de: 'Türklinke' },
  LockCylinder: { en: 'Lock Cylinder', hu: 'Zárbetét', de: 'Schließzylinder' },
  Hinge: { en: 'Hinge', hu: 'Zsanér', de: 'Scharnier' },
  DoorCloser: { en: 'Door Closer', hu: 'Ajtócsukó', de: 'Türschließer' },
  DoorStop: { en: 'Door Stop', hu: 'Ajtóütköző', de: 'Türstopper' },
  Peephole: { en: 'Peephole', hu: 'Kitekintő', de: 'Türspion' },
  WindowHandle: { en: 'Window Handle', hu: 'Ablakkilincs', de: 'Fenstergriff' },
  WindowSill: { en: 'Window Sill', hu: 'Ablakpárkány', de: 'Fensterbank' },
  GarageDoor: { en: 'Garage Door', hu: 'Garázskapu', de: 'Garagentor' },
  Gate: { en: 'Gate', hu: 'Kapu', de: 'Tor' },
  FencePost: { en: 'Fence Post', hu: 'Kerítésoszlop', de: 'Zaunpfosten' },
  FencePanel: { en: 'Fence Panel', hu: 'Kerítéselem', de: 'Zaunelement' },
  Bracket: { en: 'Bracket', hu: 'Konzol', de: 'Winkel' },
  Hook: { en: 'Hook', hu: 'Kampó', de: 'Haken' },
  CabinetHinge: { en: 'Cabinet Hinge', hu: 'Bútorpánt', de: 'Möbelscharnier' },
  DrawerSlide: { en: 'Drawer Slide', hu: 'Fióksín', de: 'Schubladenschiene' },
  CabinetHandle: { en: 'Cabinet Handle', hu: 'Bútorfogantyú', de: 'Möbelgriff' },
  FurnitureLeg: { en: 'Furniture Leg', hu: 'Bútorláb', de: 'Möbelfuß' },
  FurnitureConnector: { en: 'Furniture Connector', hu: 'Bútorösszekötő', de: 'Möbelverbinder' },
  CasterWheel: { en: 'Caster Wheel', hu: 'Bútorgörgő', de: 'Möbelrolle' },
  WallPlug: { en: 'Wall Plug', hu: 'Dűbel', de: 'Dübel' },
  AnchorBolt: { en: 'Anchor Bolt', hu: 'Betondűbel', de: 'Ankerbolzen' },
  ThreadedRod: { en: 'Threaded Rod', hu: 'Menetes szár', de: 'Gewindestange' },
  Rivet: { en: 'Rivet', hu: 'Szegecs', de: 'Niete' },
  ClevisPin: { en: 'Pin', hu: 'Csapszeg', de: 'Bolzen' },
  Spring: { en: 'Spring', hu: 'Rugó', de: 'Feder' },
  Bearing: { en: 'Bearing', hu: 'Csapágy', de: 'Lager' },
  Gasket: { en: 'Gasket', hu: 'Tömítés', de: 'Dichtung' },

  // --- Automotive ---
  GarageStorage: { en: 'Garage Storage', hu: 'Garázstároló', de: 'Garagenaufbewahrung' },
  GarageShelving: { en: 'Garage Shelving', hu: 'Garázspolc', de: 'Garagenregal' },
  CeilingRack: { en: 'Ceiling Rack', hu: 'Mennyezeti tároló', de: 'Deckenlift' },
  BikeHanger: { en: 'Bike Hanger', hu: 'Kerékpárakasztó', de: 'Fahrradhalter' },
  TireRack: { en: 'Tire Rack', hu: 'Gumiabroncs-tartó', de: 'Reifenregal' },
  OilDrainPan: { en: 'Oil Drain Pan', hu: 'Olajgyűjtő tálca', de: 'Ölauffangwanne' },
  Funnel: { en: 'Funnel', hu: 'Tölcsér', de: 'Trichter' },
  GreaseGun: { en: 'Grease Gun', hu: 'Zsírzóprés', de: 'Fettpresse' },
  SocketSet: { en: 'Socket Set', hu: 'Dugókulcs készlet', de: 'Steckschlüsselsatz' },
  TorqueWrench: { en: 'Torque Wrench', hu: 'Nyomatékkulcs', de: 'Drehmomentschlüssel' },
  ImpactWrench: { en: 'Impact Wrench', hu: 'Ütvecsavarozó', de: 'Schlagschrauber' },
  MechanicCreeper: { en: 'Mechanic Creeper', hu: 'Szerelőágy', de: 'Rollbrett' },
  WheelChock: { en: 'Wheel Chock', hu: 'Kerékkitámasztó', de: 'Radkeil' },
  CarCover: { en: 'Car Cover', hu: 'Autótakaró', de: 'Autoabdeckung' },
  SnowChain: { en: 'Snow Chain', hu: 'Hólánc', de: 'Schneekette' },
  IceScraper: { en: 'Ice Scraper', hu: 'Jégkaparó', de: 'Eiskratzer' },
  SnowBrush: { en: 'Snow Brush', hu: 'Hókefe', de: 'Schneebürste' },
  TowStrap: { en: 'Tow Strap', hu: 'Vontatókötél', de: 'Abschleppseil' },
  RoofRack: { en: 'Roof Rack', hu: 'Tetőcsomagtartó', de: 'Dachträger' },
  TrailerHitch: { en: 'Trailer Hitch', hu: 'Vonóhorog', de: 'Anhängerkupplung' },
  CarMat: { en: 'Car Mat', hu: 'Autószőnyeg', de: 'Automatte' },
  SeatCover: { en: 'Seat Cover', hu: 'Üléshuzat', de: 'Sitzbezug' },
  CarVacuum: { en: 'Car Vacuum', hu: 'Autós porszívó', de: 'Autostaubsauger' },
  OBDScanner: { en: 'OBD Scanner', hu: 'OBD diagnosztika', de: 'OBD-Diagnosegerät' },
  DashCam: { en: 'Dash Cam', hu: 'Menetrögzítő kamera', de: 'Dashcam' },
  PhoneMount: { en: 'Phone Mount', hu: 'Autós telefontartó', de: 'Handyhalterung' },
  FuelCan: { en: 'Fuel Can', hu: 'Benzinkanna', de: 'Benzinkanister' },
  Antifreeze: { en: 'Antifreeze', hu: 'Fagyálló', de: 'Frostschutzmittel' },
  AdBlueFluid: { en: 'AdBlue', hu: 'AdBlue', de: 'AdBlue' },
  SparkPlug: { en: 'Spark Plug', hu: 'Gyújtógyertya', de: 'Zündkerze' },

  // --- Winter ---
  SnowShovel: { en: 'Snow Shovel', hu: 'Hólapát', de: 'Schneeschaufel' },
  SnowBlower: { en: 'Snow Blower', hu: 'Hókotró gép', de: 'Schneefräse' },
  DeIcingSalt: { en: 'De-Icing Salt', hu: 'Útszóró só', de: 'Streusalz' },
  GritBin: { en: 'Grit Bin', hu: 'Sótároló', de: 'Streugutbehälter' },
  RoofRake: { en: 'Roof Rake', hu: 'Hóeltávolító gereblye', de: 'Dachschneerechen' },
  HeatingCable: { en: 'Heating Cable', hu: 'Fűtőkábel', de: 'Heizkabel' },
  PipeHeater: { en: 'Pipe Heater', hu: 'Csőfűtés', de: 'Rohrbegleitheizung' },
  WindowInsulationKit: { en: 'Window Insulation Kit', hu: 'Ablakszigetelő készlet', de: 'Fensterisolierset' },
  Sled: { en: 'Sled', hu: 'Szánkó', de: 'Schlitten' },
  WinterCarKit: { en: 'Winter Car Kit', hu: 'Téli autós készlet', de: 'Winterset für das Auto' },

  // --- Valuables ---
  DocumentStorage: { en: 'Document Storage', hu: 'Irattároló', de: 'Dokumentenaufbewahrung' },
  CashBox: { en: 'Cash Box', hu: 'Pénzkazetta', de: 'Geldkassette' },
  KeyOrganizer: { en: 'Key Organizer', hu: 'Kulcstartó', de: 'Schlüsselbrett' },
  SpareKey: { en: 'Spare Key', hu: 'Pótkulcs', de: 'Ersatzschlüssel' },
  Jewelry: { en: 'Jewelry', hu: 'Ékszer', de: 'Schmuck' },
  Watch: { en: 'Watch', hu: 'Karóra', de: 'Armbanduhr' },
  Collectible: { en: 'Collectible', hu: 'Gyűjtői tárgy', de: 'Sammlerstück' },
  Souvenir: { en: 'Souvenir', hu: 'Ajándéktárgy', de: 'Souvenir' },
  GiftCard: { en: 'Gift Card', hu: 'Ajándékkártya', de: 'Geschenkkarte' }
}

/** Parses `Name = 123,` members out of the canonical C# enum. */
function parseCanonicalEnum() {
  const source = fs.readFileSync(enumSourcePath, 'utf8')
  const body = source.slice(source.indexOf('enum ProductCategory'))
  const members = []
  const memberPattern = /^\s*([A-Za-z_][A-Za-z_0-9]*)\s*=\s*(\d+)\s*,?\s*(?:\/\/.*)?$/gm
  let match
  while ((match = memberPattern.exec(body)) !== null) {
    members.push({ name: match[1], value: Number(match[2]) })
  }
  return members
}

function groupIndexOf(value) {
  const matches = GROUPS.map((group, index) => ({ group, index }))
    .filter(({ group }) => group.ranges.some(([from, to]) => value >= from && value <= to))
  return matches.length === 1 ? matches[0].index : null
}

function assertConsistent(members) {
  const problems = []

  const seenValues = new Map()
  const seenNames = new Set()
  for (const { name, value } of members) {
    if (seenValues.has(value)) problems.push(`duplicate value ${value}: ${seenValues.get(value)} and ${name}`)
    if (seenNames.has(name)) problems.push(`duplicate member name ${name}`)
    seenValues.set(value, name)
    seenNames.add(name)
  }

  for (const { name } of members) {
    const labels = LABELS[name]
    if (!labels) {
      problems.push(`missing labels for ${name} — add it to LABELS`)
      continue
    }
    for (const lang of LANGUAGES) {
      if (!labels[lang]) problems.push(`missing ${lang} label for ${name}`)
    }
  }

  for (const name of Object.keys(LABELS)) {
    if (!seenNames.has(name)) problems.push(`stale LABELS entry ${name} — no such member in the API enum`)
  }

  // Every member must land in exactly one group, or the picker would silently
  // drop it (no group) or list it twice (overlapping ranges).
  for (const { name, value } of members) {
    if (groupIndexOf(value) === null) {
      const hits = GROUPS.filter(g => g.ranges.some(([from, to]) => value >= from && value <= to)).map(g => g.name)
      problems.push(hits.length === 0
        ? `${name} = ${value} falls in no GROUPS range`
        : `${name} = ${value} falls in several GROUPS ranges: ${hits.join(', ')}`)
    }
  }

  const groupNames = new Set()
  for (const group of GROUPS) {
    if (groupNames.has(group.name)) problems.push(`duplicate group name ${group.name}`)
    groupNames.add(group.name)
    for (const lang of LANGUAGES) {
      if (!group[lang]) problems.push(`missing ${lang} label for group ${group.name}`)
    }
  }

  // Duplicated labels are how the previous hu/de drift stayed invisible: two
  // categories collapsing onto one word makes a misalignment unnoticeable.
  for (const lang of LANGUAGES) {
    for (const [scope, entries] of [
      ['category', members.map(m => [m.name, LABELS[m.name]?.[lang]])],
      ['group', GROUPS.map(g => [g.name, g[lang]])]
    ]) {
      const byLabel = new Map()
      for (const [name, label] of entries) {
        if (!label) continue
        if (!byLabel.has(label)) byLabel.set(label, [])
        byLabel.get(label).push(name)
      }
      for (const [label, names] of byLabel) {
        if (names.length > 1) problems.push(`duplicate ${lang} ${scope} label "${label}" on ${names.join(', ')}`)
      }
    }
  }

  if (problems.length > 0) {
    throw new Error(`ProductCategory sync aborted:\n  - ${problems.join('\n  - ')}`)
  }
}

/**
 * Replaces the `{ ... }` block that follows `opener` with `replacement`,
 * matching braces so nothing outside the block is touched.
 */
function replaceBlock(source, opener, replacement, label) {
  const openerIndex = source.indexOf(opener)
  if (openerIndex === -1) throw new Error(`could not find ${label}`)

  let depth = 0
  let end = -1
  for (let i = openerIndex + opener.length - 1; i < source.length; i++) {
    if (source[i] === '{') depth++
    else if (source[i] === '}') {
      depth--
      if (depth === 0) { end = i; break }
    }
  }
  if (end === -1) throw new Error(`unbalanced braces after ${label}`)

  return source.slice(0, openerIndex) + replacement + source.slice(end + 1)
}

function writeEnumsTs(members) {
  let source = fs.readFileSync(enumsTsPath, 'utf8')

  const categoryBody = members.map(({ name, value }) => `  ${name} = ${value}`).join(',\r\n')
  source = replaceBlock(
    source,
    'export enum ProductCategory {',
    `export enum ProductCategory {\r\n${categoryBody}\r\n}`,
    'export enum ProductCategory'
  )

  const groupBody = GROUPS.map((group, index) => `  ${group.name} = ${index}`).join(',\r\n')
  source = replaceBlock(
    source,
    'export enum ProductCategoryGroup {',
    `export enum ProductCategoryGroup {\r\n${groupBody}\r\n}`,
    'export enum ProductCategoryGroup'
  )

  fs.writeFileSync(enumsTsPath, source, 'utf8')
}

function writeGroupsTs() {
  const ranges = GROUPS
    .map(group => `  [ProductCategoryGroup.${group.name}]: [${group.ranges.map(([from, to]) => `[${from}, ${to}]`).join(', ')}]`)
    .join(',\r\n')

  const lines = [
    '/**',
    ' * GENERATED by scripts/sync-product-category.mjs — do not edit by hand.',
    ' *',
    ' * Which ProductCategoryGroup each ProductCategory value belongs to. The grouping',
    ' * is presentation-only (it drives the grouped category pickers); the API neither',
    ' * knows nor stores it. Ranges may cover numbers no member uses yet.',
    ' */',
    '',
    "import { ProductCategoryGroup } from '~/types/enums'",
    '',
    '/** Groups in the order the pickers should show them. */',
    'export const PRODUCT_CATEGORY_GROUP_ORDER: ProductCategoryGroup[] = [',
    GROUPS.map(group => `  ProductCategoryGroup.${group.name}`).join(',\r\n'),
    ']',
    '',
    'const PRODUCT_CATEGORY_GROUP_RANGES: Record<ProductCategoryGroup, [number, number][]> = {',
    ranges,
    '}',
    '',
    '/**',
    ' * The group a raw category value belongs to, or `null` for a value this build',
    ' * does not know (an older row pointing at a since-removed category).',
    ' */',
    'export function getProductCategoryGroup(value: number): ProductCategoryGroup | null {',
    '  for (const group of PRODUCT_CATEGORY_GROUP_ORDER) {',
    '    for (const [from, to] of PRODUCT_CATEGORY_GROUP_RANGES[group]) {',
    '      if (value >= from && value <= to) return group',
    '    }',
    '  }',
    '  return null',
    '}',
    ''
  ]

  fs.writeFileSync(groupsTsPath, lines.join('\r\n'), 'utf8')
}

function writeLocale(lang, members) {
  const localePath = path.join(localeDir, `${lang}.json`)
  let source = fs.readFileSync(localePath, 'utf8')

  const categoryEntries = members
    .map(({ name, value }) => `      ${JSON.stringify(String(value))}: ${JSON.stringify(LABELS[name][lang])}`)
    .join(',\r\n')
  source = replaceBlock(
    source,
    '"productCategory": {',
    `"productCategory": {\r\n${categoryEntries}\r\n    }`,
    `enums.productCategory in ${lang}.json`
  )

  const groupEntries = GROUPS
    .map((group, index) => `      ${JSON.stringify(String(index))}: ${JSON.stringify(group[lang])}`)
    .join(',\r\n')
  source = replaceBlock(
    source,
    '"productCategoryGroup": {',
    `"productCategoryGroup": {\r\n${groupEntries}\r\n    }`,
    `enums.productCategoryGroup in ${lang}.json`
  )

  // Guard against a stray edit breaking the locale file.
  JSON.parse(source)
  fs.writeFileSync(localePath, source, 'utf8')
}

const members = parseCanonicalEnum()
assertConsistent(members)

writeEnumsTs(members)
writeGroupsTs()
for (const lang of LANGUAGES) writeLocale(lang, members)

console.log(`ProductCategory synced from the API enum: ${members.length} members in ${GROUPS.length} groups -> app/types/enums.ts, app/utils/productCategoryGroups.ts, ${LANGUAGES.join('/')}.json`)
