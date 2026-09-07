import { z } from 'zod'
import { ProductCategory, Unit } from '~/types/enums'
import type { CreateProductRequest, UpdateProductRequest } from '~/types/product'

/**
 * The one product create/edit contract, shared by every product form: the master-data
 * drawer (`ProductFormDrawer`), the inventory modal (`AddInventoryItemModal`) and the
 * shopping-list modal (`AddShoppingListItemModal`). The three used to carry their own
 * copy of the schema and payload mapping and had drifted apart, which is how the
 * master-data drawer ended up sending the category as a string and being rejected.
 *
 * The limits mirror `CreateProductRequest` / `UpdateProductRequest` in Homassy.API, so
 * whatever passes here is accepted there instead of coming back as an untyped 400.
 */

/** Lengths `BarcodeValidationService` accepts: UPC-E, EAN-8, UPC-A, EAN-13. */
const BARCODE_LENGTHS = [6, 8, 12, 13]

type Translate = (key: string, named?: Record<string, unknown>) => string

export const buildProductSchema = (t: Translate) => z.object({
  name: z.string({ required_error: t('pages.addProduct.form.nameRequired') })
    .trim()
    .min(1, t('pages.addProduct.form.nameRequired'))
    .min(2, t('pages.addProduct.form.nameLength'))
    .max(128, t('pages.addProduct.form.nameLength')),
  brand: z.string({ required_error: t('pages.addProduct.form.brandRequired') })
    .trim()
    .min(1, t('pages.addProduct.form.brandRequired'))
    .min(2, t('pages.addProduct.form.brandLength'))
    .max(128, t('pages.addProduct.form.brandLength')),
  // Numeric — `Product.Category` is a C# enum, so the API only deserializes numbers.
  category: z.nativeEnum(ProductCategory).optional(),
  unit: z.nativeEnum(Unit, { required_error: t('pages.addProduct.form.unitRequired') }),
  barcode: z.string()
    .trim()
    .refine(value => value === '' || /^\d+$/.test(value), t('pages.addProduct.form.barcodeInvalid'))
    .refine(value => value === '' || BARCODE_LENGTHS.includes(value.length), t('pages.addProduct.form.barcodeInvalid'))
    .optional(),
  isEatable: z.boolean().optional().default(false),
  isFavorite: z.boolean().optional().default(false),
  notes: z.string().trim().max(128, t('pages.addProduct.form.notesLength')).optional()
})

export type ProductSchema = z.output<ReturnType<typeof buildProductSchema>>

/** The shape the form's `state` object holds, before Zod parses it. */
export interface ProductFormState {
  name: string
  brand: string
  category: ProductCategory | undefined
  unit: Unit
  barcode: string
  isEatable: boolean
  isFavorite: boolean
  notes: string
}

export const emptyProductForm = (): ProductFormState => ({
  name: '',
  brand: '',
  category: undefined,
  unit: Unit.Piece,
  barcode: '',
  isEatable: false,
  isFavorite: false,
  notes: ''
})

/** `?? null` and not `|| null`: `ProductCategory.Other` is 0, and dropping it loses the category. */
export const toCreateProductRequest = (data: ProductSchema): CreateProductRequest => ({
  name: data.name.trim(),
  brand: data.brand.trim(),
  category: data.category ?? null,
  unit: data.unit,
  barcode: data.barcode?.trim() || null,
  isEatable: data.isEatable,
  isFavorite: data.isFavorite,
  notes: data.notes?.trim() || null
})

/**
 * `notes` is never sent empty: `ProductInfo` does not carry the stored notes, so the edit
 * form starts blank and an empty value would silently clobber them. Same reason the API
 * treats a null `category` as "leave unchanged", so a cleared category is not sent either.
 * `isFavorite` is per-user and has its own toggle endpoint, so it is not part of an update.
 */
export const toUpdateProductRequest = (data: ProductSchema): UpdateProductRequest => ({
  name: data.name.trim(),
  brand: data.brand.trim(),
  category: data.category ?? undefined,
  unit: data.unit,
  barcode: data.barcode?.trim() || undefined,
  isEatable: data.isEatable,
  notes: data.notes?.trim() || undefined
})

export const useProductFormSchema = () => {
  const { t } = useI18n()
  const productSchema = buildProductSchema(t)

  return {
    productSchema,
    emptyProductForm,
    toCreateProductRequest,
    toUpdateProductRequest
  }
}
