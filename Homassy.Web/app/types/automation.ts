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
  NotifyOnly = 1
}

export enum AutomationExecutionStatus {
  AutoConsumed = 0,
  NotificationSent = 1,
  ManuallyConfirmed = 2,
  Skipped = 3,
  Failed = 4
}

// ===================
// Responses
// ===================

export interface AutomationResponse {
  publicId: string
  inventoryItemPublicId: string
  productName: string
  productBrand: string
  scheduleType: ScheduleType
  intervalDays?: number
  scheduledDayOfWeek?: number
  scheduledDayOfMonth?: number
  scheduledTime: string
  actionType: AutomationActionType
  consumeQuantity?: number
  consumeUnit?: Unit
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
  inventoryItemPublicId: string
  scheduleType: ScheduleType
  intervalDays?: number
  scheduledDayOfWeek?: number
  scheduledDayOfMonth?: number
  scheduledTime: string
  actionType: AutomationActionType
  consumeQuantity?: number
  consumeUnit?: Unit
  isSharedWithFamily?: boolean
}

export interface UpdateAutomationRequest {
  scheduleType?: ScheduleType
  intervalDays?: number
  scheduledDayOfWeek?: number
  scheduledDayOfMonth?: number
  scheduledTime?: string
  actionType?: AutomationActionType
  consumeQuantity?: number
  consumeUnit?: Unit
  isEnabled?: boolean
}

export interface ExecuteAutomationRequest {
  notes?: string
}
