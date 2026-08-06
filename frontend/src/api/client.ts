import type { CreateJobResponse, ErrorResponse, JobStatusResponse } from './types'

async function parseErrorMessage(response: Response): Promise<string> {
  try {
    const body = (await response.json()) as ErrorResponse
    return body.message ?? `Request failed with status ${response.status}`
  } catch {
    return `Request failed with status ${response.status}`
  }
}

export async function createJob(files: File[]): Promise<CreateJobResponse> {
  const formData = new FormData()
  for (const file of files) {
    formData.append('files', file)
  }

  const response = await fetch('/api/jobs', {
    method: 'POST',
    body: formData,
  })

  if (!response.ok) {
    throw new Error(await parseErrorMessage(response))
  }

  return (await response.json()) as CreateJobResponse
}

export async function getJobStatus(jobId: string): Promise<JobStatusResponse> {
  const response = await fetch(`/api/jobs/${jobId}`)

  if (!response.ok) {
    throw new Error(await parseErrorMessage(response))
  }

  return (await response.json()) as JobStatusResponse
}

export function getDownloadUrl(jobId: string): string {
  return `/api/jobs/${jobId}/download`
}

export async function getHealth(): Promise<{ status: string; version: string }> {
  const response = await fetch('/api/health')

  if (!response.ok) {
    throw new Error(await parseErrorMessage(response))
  }

  return (await response.json()) as { status: string; version: string }
}

export async function retryFailedArticles(jobId: string): Promise<void> {
  const response = await fetch(`/api/jobs/${jobId}/retry`, { method: 'POST' })

  if (!response.ok) {
    throw new Error(await parseErrorMessage(response))
  }
}

export async function finishJob(jobId: string): Promise<void> {
  const response = await fetch(`/api/jobs/${jobId}/finish`, { method: 'POST' })

  if (!response.ok) {
    throw new Error(await parseErrorMessage(response))
  }
}
