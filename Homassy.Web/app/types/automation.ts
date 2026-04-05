/**
 * Automation related types
 */
import type { Unit } from './enums'

// ===================
// Enums
// ===================

export enum ScheduleType {
  Interval = 0,
  FixedDate = 1
}

export enum AutomationActionType {
  AutoConsume = 0,
  NotifyOnly = 1,
  AddToShoppingList = 2
}

export enum AutomationExecutionStatus {
  AutoConsumed = 0,
  NotificationSent = 1,
  ManuallyConfirmed = 2,
  Skipped = 3,
  Failed = 4,
  AddedToShoppingList = 5
}

export enum DaysOfWeek {
  None = 0,
  Monday = 1,
  Tuesday = 2,
  Wednesday = 4,
  Thursday = 8,
  Friday = 16,
  Saturday = 32,
  Sunday = 64
}

// ===================
// Responses
// ===================

export interface AutomationResponse {
  publicId: string
  inventoryItemPublicId?: string
  productName: string
  productBrand: string
  productPublicId?: string
  shoppingListPublicId?: string
  shoppingListName?: string
  scheduleType: ScheduleType
  intervalDays?: number
  scheduledDaysOfWeek?: number
  scheduledDayOfMonth?: number
  scheduledTime: string
  actionType: AutomationActionType
  consumeQuantity?: number
  consumeUnit?: Unit
  addQuantity?: number
  addUnit?: Unit
  isEnabled: boolean
  nextExecutionAt?: string
  lastExecutedAt?: string
}

export interface AutomationExecutionResponse {
  publicId: string
  executedAt: string
  status: AutomationExecutionStatus
  consumedQuantity?: number
  notes?: string
  triggeredByUserId?: number
}

// ===================
// Requests
// ===================

export interface CreateAutomationRequest {
  inventoryItemPublicId?: string
  productPublicId?: string
  shoppingListPublicId?: string
  scheduleType: ScheduleType
  intervalDays?: number
  scheduledDaysOfWeek?: number
  scheduledDayOfMonth?: number
  scheduledTime: string
  actionType: AutomationActionType
  consumeQuantity?: number
  consumeUnit?: Unit
  addQuantity?: number
  addUnit?: Unit
  isSharedWithFamily?: boolean
}

export interface UpdateAutomationRequest {
  scheduleType?: ScheduleType
  intervalDays?: number
  scheduledDaysOfWeek?: number
  scheduledDayOfMonth?: number
  scheduledTime?: string
  actionType?: AutomationActionType
  consumeQuantity?: number
  consumeUnit?: Unit
  addQuantity?: number
  addUnit?: Unit
  shoppingListPublicId?: string
  productPublicId?: string
  isEnabled?: boolean
}

export interface ExecuteAutomationRequest {
  notes?: string
}
