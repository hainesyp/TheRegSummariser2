import { useEffect, useState } from 'react'
import { createJob, getJobStatus } from './api/client'
import type { JobStatusResponse } from './api/types'
import { FileUpload } from './components/FileUpload'
import { JobProgress } from './components/JobProgress'
import { ResultDownload } from './components/ResultDownload'
import './App.css'

const POLL_INTERVAL_MS = 2000

function App() {
  const [jobId, setJobId] = useState<string | null>(null)
  const [job, setJob] = useState<JobStatusResponse | null>(null)
  const [submitError, setSubmitError] = useState<string | null>(null)
  const [isSubmitting, setIsSubmitting] = useState(false)

  useEffect(() => {
    if (!jobId || job?.status === 'Completed' || job?.status === 'Failed') {
      return
    }

    let cancelled = false

    const poll = async () => {
      try {
        const status = await getJobStatus(jobId)
        if (!cancelled) {
          setJob(status)
        }
      } catch (err) {
        if (!cancelled) {
          setSubmitError(err instanceof Error ? err.message : 'Failed to fetch job status.')
        }
      }
    }

    poll()
    const interval = setInterval(poll, POLL_INTERVAL_MS)

    return () => {
      cancelled = true
      clearInterval(interval)
    }
  }, [jobId, job?.status])

  const handleSubmit = async (files: File[]) => {
    setIsSubmitting(true)
    setSubmitError(null)
    try {
      const response = await createJob(files)
      setJobId(response.jobId)
      setJob(null)
    } catch (err) {
      setSubmitError(err instanceof Error ? err.message : 'Failed to upload files.')
    } finally {
      setIsSubmitting(false)
    }
  }

  const handleReset = () => {
    setJobId(null)
    setJob(null)
    setSubmitError(null)
  }

  const isProcessing = jobId !== null && job?.status !== 'Completed' && job?.status !== 'Failed'

  return (
    <div className="app">
      <header>
        <h1>The Register Summary</h1>
        <p>Upload Register newsletter .msg files to get a categorised, AI-summarised HTML report.</p>
      </header>

      <main>
        {!jobId && <FileUpload disabled={isSubmitting} onSubmit={handleSubmit} />}

        {submitError && <p className="error-text">{submitError}</p>}

        {job && <JobProgress job={job} />}

        {job?.status === 'Completed' && <ResultDownload jobId={jobId!} onReset={handleReset} />}

        {job?.status === 'Failed' && (
          <div className="result-download">
            <button type="button" className="secondary-button" onClick={handleReset}>
              Start another
            </button>
          </div>
        )}

        {isProcessing && !job && <p>Uploading…</p>}
      </main>
    </div>
  )
}

export default App
