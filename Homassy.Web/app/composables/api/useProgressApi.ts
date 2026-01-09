/**
 * Progress API composable
 * Provides progress tracking API calls for async operations
 */
export interface ProgressStatus {
  jobId: string
  percentage: number
  stage: string
  status: 'inprogress' | 'completed' | 'failed' | 'cancelled'
  errorMessage?: string
}

export const useProgressApi = () => {
  const client = useApiClient()

  /**
   * Get progress status of a job
   */
  const getProgress = async (jobId: string) => {
    return await client.get<ProgressStatus>(
      `/api/v1/Progress/${jobId}`,
      {
        showErrorToast: false,
        showSuccessToast: false
      }
    )
  }

  /**
   * Cancel a running job
   */
  const cancelJob = async (jobId: string) => {
    return await client.delete(
      `/api/v1/Progress/${jobId}`,
      {
        showErrorToast: false,
        showSuccessToast: false
      }
    )
  }

  /**
   * Poll for progress updates
   * @param jobId - The job ID to poll
   * @param onProgress - Callback for progress updates
   * @param interval - Polling interval in ms (default: 500)
   * @returns Cleanup function to stop polling
   */
  const pollProgress = (
    jobId: string,
    onProgress: (progress: ProgressStatus) => void,
    interval: number = 500
  ): (() => void) => {
    let isActive = true
    let timeoutId: ReturnType<typeof setTimeout> | null = null

    const poll = async () => {
      if (!isActive) return

      try {
        const response = await getProgress(jobId)
        if (response.data) {
          onProgress(response.data)

          // Continue polling if still in progress
          if (response.data.status === 'inprogress' && isActive) {
            timeoutId = setTimeout(poll, interval)
          }
        }
      } catch (error) {
        console.error('Failed to poll progress:', error)
        // Stop polling on error
        isActive = false
      }
    }

    // Start polling
    poll()

    // Return cleanup function
    return () => {
      isActive = false
      if (timeoutId) {
        clearTimeout(timeoutId)
      }
    }
  }

  return {
    getProgress,
    cancelJob,
    pollProgress
  }
}
