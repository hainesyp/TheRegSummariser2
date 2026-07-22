export type JobStatusValue = 'Pending' | 'Processing' | 'Completed' | 'Failed'

export type ArticleStatusValue = 'Pending' | 'Fetching' | 'Summarizing' | 'Completed' | 'Failed'

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
