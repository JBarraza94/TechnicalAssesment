import { HttpClient, HttpErrorResponse, HttpParams } from '@angular/common/http';
import { Injectable, InjectionToken, inject } from '@angular/core';
import { Observable, catchError, throwError } from 'rxjs';
import { ApiError, Company, CreateCompanyRequest, ErrorResponse } from './company.models';

/** Base URL of the API (e.g. http://localhost:5000/api), provided from the environment file. */
export const API_BASE_URL = new InjectionToken<string>('API_BASE_URL');

export const NETWORK_ERROR_MESSAGE = 'Cannot reach the server. Check your connection and that the API is running.';
export const SERVER_ERROR_MESSAGE = 'Something went wrong on the server. Please try again later.';

@Injectable({ providedIn: 'root' })
export class CompanyService {
  private readonly http = inject(HttpClient);
  private readonly baseUrl = `${inject(API_BASE_URL)}/companies`;

  create(request: CreateCompanyRequest): Observable<Company> {
    return this.http.post<Company>(this.baseUrl, request).pipe(catchError(toApiError));
  }

  /** Lists companies; a non-empty query uses the API's relevance search (?q=). */
  list(query = ''): Observable<Company[]> {
    const q = query.trim();
    const params = q ? new HttpParams().set('q', q) : undefined;
    return this.http.get<Company[]>(this.baseUrl, { params }).pipe(catchError(toApiError));
  }

  getById(id: string): Observable<Company> {
    return this.http.get<Company>(`${this.baseUrl}/${encodeURIComponent(id)}`).pipe(catchError(toApiError));
  }
}

/** Keeps 4xx server messages as-is; replaces network and 5xx errors with friendly text. */
export function toApiError(error: HttpErrorResponse): Observable<never> {
  if (error.status === 0) {
    return throwError(() => new ApiError(NETWORK_ERROR_MESSAGE, 0));
  }

  if (error.status >= 500) {
    return throwError(() => new ApiError(SERVER_ERROR_MESSAGE, error.status));
  }

  const body = error.error as Partial<ErrorResponse> | string | null;
  const message =
    typeof body === 'object' && body?.message
      ? body.message
      : `The request failed (${error.status} ${error.statusText || 'error'}).`;
  const errors = typeof body === 'object' && Array.isArray(body?.errors) ? body.errors : [message];
  return throwError(() => new ApiError(message, error.status, errors));
}
