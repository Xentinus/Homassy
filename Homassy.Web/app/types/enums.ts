/**
 * Enums used across the API
 */

export enum Unit {
  Piece = 0,
  Gram = 1,
  Kilogram = 2,
  Milligram = 3,
  Milliliter = 10,
  Centiliter = 11,
  Deciliter = 12,
  Liter = 13,
  Meter = 20,
  Centimeter = 21,
  Millimeter = 22,
  SquareMeter = 30,
  CubicMeter = 31,
  Teaspoon = 40,
  Tablespoon = 41,
  Cup = 42,
  Pack = 50,
  Box = 51,
  Bottle = 52,
  Can = 53,
  Jar = 54,
  Bag = 55
}

export enum Currency {
  Huf = 135, // Hungarian Forint
  Eur = 105, // Euro
  Usd = 279  // US Dollar
}

export enum Language {
  Hungarian = 0,
  German = 1,
  English = 2
}

export enum UserTimeZone {
  CentralEuropean = 0
}

export enum ImageFormat {
  Jpeg = 0,
  Png = 1,
  WebP = 2
}

export enum SelectValueType {
  Units = 0,
  Currencies = 1,
  TimeZones = 2,
  Languages = 3
}

export enum BarcodeFormat {
  Ean13 = 0,
  Ean8 = 1,
  Upc = 2,
  Code128 = 3,
  Code39 = 4,
  Qr = 5
}

export enum UserStatus {
  Active = 0,
  Inactive = 1,
  Suspended = 2,
  Deleted = 3
}
