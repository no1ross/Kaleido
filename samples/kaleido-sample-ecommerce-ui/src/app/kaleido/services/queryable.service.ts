import { HttpClient, HttpErrorResponse } from '@angular/common/http';
import { Injectable, inject } from '@angular/core';
import { Observable } from 'rxjs';

import { QueryableResult } from '../models/queryable-result';
import { QueryRequest } from '../models/queryable-request';
import { QueryErrorResponse } from '../models/query-error-response';

import { catchError, throwError } from 'rxjs';

@Injectable({
  providedIn: 'root'
})
export class QueryableService {

  private readonly http =
    inject(HttpClient);

query<T>(
  source: string,
  request: QueryRequest
): Observable<QueryableResult<T>> {

  return this.http.post<
    QueryableResult<T>>(
      `https://localhost:7251/kaleido/queryable/${source}/query`,
      request)
    .pipe(
      catchError(
        (error: HttpErrorResponse) => {

          if (
            error.status === 400 &&
            error.error?.errors) {

            const response =
              error.error as QueryErrorResponse;

            console.error(
              'Queryable validation error',
              response);

            //
            // For now just rethrow.
            // Later we can show a toast/dialog.
            //

            return throwError(
              () => response);
          }

          return throwError(
            () => error);
        }));
}
}
