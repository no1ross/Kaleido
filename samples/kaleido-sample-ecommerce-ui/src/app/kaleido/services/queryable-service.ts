import { HttpClient, HttpErrorResponse } from '@angular/common/http';
import { Injectable, inject } from '@angular/core';
import { Observable } from 'rxjs';

import { QueryableResult } from '../models/queryable-result';
import { QueryRequest } from '../models/queryable-request';
import { QueryErrorResponse } from '../models/query-error-response';

import { catchError, throwError, map } from 'rxjs';

@Injectable({
  providedIn: 'root'
})
export class QueryableService {

  private readonly http =
    inject(HttpClient);

  query<TResponse, TParameters = unknown>(
    context: string,
    view: string,
    request: QueryRequest<TParameters>
  ): Observable<QueryableResult<TResponse>> {

    this.logRequest(context, view, request);

    return this.http.post<
      QueryableResult<TResponse>>(
        `https://localhost:7251/kaleido/queryable/${context}/${view}/query`,
        request)
      .pipe(
          map(result => {

              this.logResponse(context, view, result);

              return result;
          }),
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

                return throwError(
                  () => response);
              }

              return throwError(
                () => error);
          }));
  }

    private logRequest(
        context: string,
        view: string,
        request: unknown): void {

        console.group(`[QUERYABLE] $${context}/${view}`);

        console.log('Request', request);

        console.groupEnd();
    }
    
    private logResponse(
        context: string,
        view: string,
        request: unknown): void {

        console.group(`[QUERYABLE] $${context}/${view}`);

        console.log('Response', request);

        console.groupEnd();
    }
}
