/**
 * Health check related types
 */

export interface HealthCheckResponse {
  status: string
  duration: string
  dependencies: Record<string, DependencyHealth>
}

export interface DependencyHealth {
  status: string
  duration: string
  description?: string
  data?: Record<string, unknown>
}
