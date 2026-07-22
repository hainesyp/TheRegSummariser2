import { getDownloadUrl } from '../api/client'

interface ResultDownloadProps {
  jobId: string
  onReset: () => void
}

export function ResultDownload({ jobId, onReset }: ResultDownloadProps) {
  return (
    <div className="result-download">
      <p>Your report is ready.</p>
      <a className="primary-button" href={getDownloadUrl(jobId)} download>
        Download HTML Report
      </a>
      <button type="button" className="secondary-button" onClick={onReset}>
        Start another
      </button>
    </div>
  )
}
