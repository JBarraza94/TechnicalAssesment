/** Mirrors the API's CreateCompanyRequest. */
export interface CreateCompanyRequest {
  name: string;
  website: string;
}

/** Mirrors CompanyResponse / CompanySearchResult. Score is present only for "q" searches. */
export interface Company {
  id: string;
  name: string;
  website: string;
  score?: number;
  explanation?: string;
}

/** Mirrors the API's ErrorResponse. */
export interface ErrorResponse {
  message: string;
  errors?: string[];
}

/** Error surfaced to components: always carries a user-facing message. */
export class ApiError extends Error {
  constructor(
    message: string,
    readonly status: number,
    readonly errors: string[] = [],
  ) {
    super(message);
    this.name = 'ApiError';
  }
}
