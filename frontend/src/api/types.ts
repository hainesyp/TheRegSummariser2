export type JobStatusValue = 'Pending' | 'Processing' | 'AwaitingDecision' | 'Completed' | 'Failed'

export type ArticleStatusValue = 'Pending' | 'Fetching' | 'Summarizing' | 'Completed' | 'Duplicate' | 'Failed'

export interface ArticleStatusDto {
  url: string
  status: ArticleStatusValue
  title: string | null
  category: string | null
  error: string | null
}

export interface JobStatusResponse {
  jobId: string
  status: JobStatusValue
  totalCount: number
  completedCount: number
  duplicateCount: number
  failedCount: number
  errorMessage: string | null
  articles: ArticleStatusDto[]
}

export interface CreateJobResponse {
  jobId: string
  articleCount: number
}

export interface ErrorResponse {
  message: string
}
