import { useState } from 'react'
import { finishJob, retryFailedArticles } from '../api/client'
import type { JobStatusResponse } from '../api/types'

interface JobDecisionProps {
  jobId: string
  job: JobStatusResponse
}

export function JobDecision({ jobId, job }: JobDecisionProps) {
  const [isSubmitting, setIsSubmitting] = useState(false)
  const [error, setError] = useState<string | null>(null)

  const runAction = async (action: () => Promise<void>) => {
    setIsSubmitting(true)
    setError(null)
    try {
      await action()
    } catch (err) {
      setError(err instanceof Error ? err.message : 'Action failed.')
    } finally {
      setIsSubmitting(false)
    }
  }

  return (
    <div className="job-decision">
      <p>
        {job.failedCount} of {job.totalCount} article{job.totalCount === 1 ? '' : 's'} failed after automatic
        retries. {job.completedCount} summarised successfully so far.
      </p>

      {error && <p className="error-text">{error}</p>}

      <div className="job-decision__actions">
        <button
          type="button"
          className="primary-button"
          disabled={isSubmitting}
          onClick={() => runAction(() => retryFailedArticles(jobId))}
        >
          Retry failed articles
        </button>
        <button
          type="button"
          className="secondary-button"
          disabled={isSubmitting || job.completedCount === 0}
          onClick={() => runAction(() => finishJob(jobId))}
        >
          Finish with {job.completedCount} summarised
        </button>
      </div>
    </div>
  )
}
