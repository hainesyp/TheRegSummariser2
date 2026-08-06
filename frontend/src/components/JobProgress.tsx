import type { JobStatusResponse } from '../api/types'

interface JobProgressProps {
  job: JobStatusResponse
}

const STATUS_LABELS: Record<string, string> = {
  Pending: 'Queued',
  Fetching: 'Fetching article…',
  Summarizing: 'Summarising…',
  Completed: 'Done',
  Duplicate: 'Duplicate (skipped)',
  Failed: 'Failed',
}

export function JobProgress({ job }: JobProgressProps) {
  const processedCount = job.completedCount + job.duplicateCount + job.failedCount
  const percent = job.totalCount === 0 ? 0 : Math.round((processedCount / job.totalCount) * 100)

  return (
    <div className="job-progress">
      <div className="progress-bar">
        <div className="progress-bar__fill" style={{ width: `${percent}%` }} />
      </div>
      <p className="progress-summary">
        {processedCount} / {job.totalCount} articles processed
        {job.duplicateCount > 0 && ` (${job.duplicateCount} duplicate)`}
        {job.failedCount > 0 && ` (${job.failedCount} failed)`}
      </p>

      {job.errorMessage && <p className="error-text">{job.errorMessage}</p>}

      <ul className="article-list">
        {job.articles.map((article) => (
          <li key={article.url} className={`article-item article-item--${article.status.toLowerCase()}`}>
            <span className="article-item__title">{article.title ?? article.url}</span>
            <span className="article-item__status">{STATUS_LABELS[article.status] ?? article.status}</span>
            {article.error && <span className="article-item__error">{article.error}</span>}
          </li>
        ))}
      </ul>
    </div>
  )
}
