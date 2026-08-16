import { HttpClient, HttpErrorResponse } from '@angular/common/http';
import { Injectable, inject } from '@angular/core';
import { Observable } from 'rxjs';

import { QueryableResult } from '../models/queryable-result';
import { QueryRequest } from '../models/queryable-request';
import { QueryErrorResponse } from '../models/query-error-response';

import { catchError, throwError, map } from 'rxjs';
import { QueryableRegistry } from './queryable-registry';
import { buildApiUrl } from '../../../configuration/urlConfig';
import { QueryableRequestValidator, QueryableRequestValidationError } from './queryable-request-validator';

@Injectable({
  providedIn: 'root'
})
export class QueryableService {

  private readonly http =
    inject(HttpClient);

  private readonly queryableRegistry =
    inject(QueryableRegistry);

private readonly queryRequestValidator =
    inject(QueryableRequestValidator);    

  query<TResponse, TParameters = unknown>(
    view: string,
    request: QueryRequest<TParameters>
  ): Observable<QueryableResult<TResponse>> {

    const viewMetadata =
      this.queryableRegistry.getView(view);

    const validationResult =
        this.queryRequestValidator.validate(
            viewMetadata,
            request);

    if (!validationResult.isValid) {

        console.error(
            '[QueryableService] Request validation failed.',
            validationResult.messages);

        return throwError(
            () => new QueryableRequestValidationError(
                validationResult.messages));
    }


    console.log(viewMetadata);

    this.logRequest(view, request);

    return this.http.post<
      QueryableResult<TResponse>>(
        buildApiUrl(viewMetadata.queryUrl),
        request)
      .pipe(
          map(result => {

              this.logResponse(view, result);

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
        view: string,
        request: unknown): void {

        console.group(`[QUERYABLE] $${view}`);

        console.log('Request', request);

        console.groupEnd();
    }
    
    private logResponse(
        view: string,
        request: unknown): void {

        console.group(`[QUERYABLE] $${view}`);

        console.log('Response', request);

        console.groupEnd();
    }
}
