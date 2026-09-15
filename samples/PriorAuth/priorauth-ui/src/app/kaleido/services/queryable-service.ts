import { HttpClient, HttpErrorResponse } from '@angular/common/http';
import { Injectable, inject } from '@angular/core';
import { Observable, catchError, map, throwError } from 'rxjs';

import { QueryableResult } from '../models/queryable-result';
import { QueryRequest } from '../models/queryable-request';
import { QueryErrorResponse } from '../models/query-error-response';
import {
    QueryableField,
    QueryablePagingMetadata,
    QueryableParameter,
    QueryableRecord,
    QueryableViewRegistration
} from '../models/queryable-registry';
import { QueryableRegistry } from './queryable-registry';
import {
    QueryableRequestValidationError,
    QueryableRequestValidator
} from './queryable-request-validator';
import { buildServiceUrl, getServiceRoutes, PriorAuthServiceRouteConfig } from '../../../configuration/urlConfig';

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

    queryView<TResponse, TParameters = unknown>(
        view: string,
        request: QueryRequest<TParameters>
    ): Observable<QueryableResult<TResponse>> {
        const registration =
            this.queryableRegistry.getViewRegistration(view);

        return this.executeValidatedQuery<TResponse, TParameters>(
            view,
            request,
            registration.context,
            registration.view.queryUrl,
            registration.view.parameters,
            registration.context.fields,
            registration.view.pageable);
    }

    queryContext<TResponse, TParameters = unknown>(
        contextName: string,
        request: QueryRequest<TParameters>
    ): Observable<QueryableResult<TResponse>> {
        const context =
            this.queryableRegistry.getContext(contextName);

        if (!context.queryUrl) {
            return throwError(
                () => new Error(
                    `Queryable context '${contextName}' does not support direct query.`));
        }

        return this.executeValidatedQuery<TResponse, TParameters>(
            contextName,
            request,
            context,
            context.queryUrl,
            [],
            context.fields,
            null);
    }

    private executeValidatedQuery<TResponse, TParameters>(
        operation: string,
        request: QueryRequest<TParameters>,
        context: QueryableRecord,
        path: string,
        parameters: readonly QueryableParameter[],
        fields: readonly QueryableField[],
        pageable: QueryablePagingMetadata | null
    ): Observable<QueryableResult<TResponse>> {
        const validationResult =
            this.queryRequestValidator.validate(
                { parameters, fields, pageable },
                request);

        if (!validationResult.isValid) {
            console.error(
                '[QueryableService] Request validation failed.',
                validationResult.messages);

            return throwError(
                () => new QueryableRequestValidationError(
                    validationResult.messages));
        }

        const serviceKey = this.serviceKeyFromUrl(path);
        const service = this.resolveService(serviceKey);
        const url = buildServiceUrl(service, path);

        this.logRequest(operation, url, request, serviceKey);

        return this.executeQuery<TResponse>(operation, url, request, serviceKey);
    }

    private serviceKeyFromUrl(url: string): string {
        return url.replace(/^\/+/, '').split('/')[0] ?? '';
    }

    private resolveService(serviceKey: string): PriorAuthServiceRouteConfig {
        return getServiceRoutes().find(s => s.key === serviceKey)
            ?? { key: serviceKey, baseUrl: '' };
    }

    private executeQuery<TResponse>(
        operation: string,
        url: string,
        request: unknown,
        serviceName: string
    ): Observable<QueryableResult<TResponse>> {
        return this.http.post<QueryableResult<TResponse>>(url, request)
            .pipe(
                map(result => {
                    this.logResponse(operation, url, result, serviceName);
                    return result;
                }),
                catchError((error: HttpErrorResponse) => {
                    if (error.error?.errors) {
                        const response = error.error as QueryErrorResponse;
                        console.error(
                            error.status >= 500
                                ? 'Queryable server error'
                                : 'Queryable validation error',
                            response);
                        return throwError(() => response);
                    }
                    return throwError(() => error);
                }));
    }

    private logRequest(view: string, url: string, request: unknown, serviceName: string): void {
        console.group(`[QUERYABLE] ${view}`);
        console.log('Service', serviceName);
        console.log('Url', url);
        console.log('Request', request);
        console.groupEnd();
    }

    private logResponse(view: string, url: string, response: unknown, serviceName: string): void {
        console.group(`[QUERYABLE] ${view}`);
        console.log('Service', serviceName);
        console.log('Url', url);
        console.log('Response', response);
        console.groupEnd();
    }
}
