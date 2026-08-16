import { Injectable, inject } from '@angular/core';
import { Observable, tap, map, of, catchError, throwError } from 'rxjs';
import { QueryableView, QueryableRecord, QueryableField, QueryableViewRegistration } from '../models/queryable-registry';
import { HttpClient } from '@angular/common/http';
import { getQueryableRegistryUrl } from '../../../configuration/urlConfig';

@Injectable({
    providedIn: 'root'
})
export class QueryableRegistry {

    private initialized = false;
    
    private readonly http =
        inject(HttpClient);

    private readonly contextsByName =
        new Map<string, QueryableRecord>();

    private readonly viewsByName =
        new Map<string, QueryableView>();

    private readonly viewRegistrationsByName =
        new Map<string, QueryableViewRegistration>();
    
    get isInitialized(): boolean {
        return this.initialized;
    }

    initialize(): Observable<void> {

        if (this.initialized) {

            console.log(
                '[QueryableRegistry] Already initialized.');

            return of(undefined);
        }

        console.log(
            '[QueryableRegistry] Loading queryable registry...');

        const started =
            performance.now();

        return this.http
            .get<QueryableRecord[]>(
                getQueryableRegistryUrl())
            .pipe(
                tap(records => {

                    this.populateRegistry(records);

                    this.initialized = true;

                    const duration =
                        Math.round(
                            performance.now() - started);

                    console.group('[QueryableRegistry]');

                    console.log(
                        `[QueryableRegistry] Loaded ${records.length} contexts in ${duration}ms.`);

                    console.table(
                        records.map(record => ({
                            Name: record.name,
                            DisplayName: record.displayName,
                            Views: record.views.length,
                            Fields: record.fields.length
                        })));

                    console.groupEnd();
                }),
                map(() => undefined),
                catchError(error => {

                    console.error(
                        '[QueryableRegistry] Failed to load queryable registry.',
                        error);

                    return throwError(
                        () => error);
                })
            );
    }

    private populateRegistry(
        records: QueryableRecord[]
    ): void {

        this.contextsByName.clear();
        this.viewsByName.clear();

        for (const context of records) {

            this.contextsByName.set(
                context.name,
                context);

            for (const view of context.views) {

                this.viewsByName.set(
                    view.name,
                    view);

                this.viewRegistrationsByName.set(
                    view.name,
                    {
                        context,
                        view
                    });
            }
        }
    }

    getContext(
        name: string
    ): QueryableRecord {

        const context =
            this.contextsByName.get(name);

        if (!context) {

            throw new Error(
                `Queryable context '${name}' is not registered.`);
        }

        return context;
    }

    getContexts(): readonly QueryableRecord[] {

        return Array.from(
            this.contextsByName.values());
    }

    getView(
        name: string
    ): QueryableView {

        const view =
            this.viewsByName.get(name);

        if (!view) {

            throw new Error(
                `Queryable view '${name}' is not registered.`);
        }

        return view;
    }

    getViews(): readonly QueryableView[] {

        return Array.from(
            this.viewsByName.values());
    }

    tryGetView(
        name: string
    ): QueryableView | undefined {

        return this.viewsByName.get(name);
    }

    tryGetContext(
        name: string
    ): QueryableRecord | undefined {

        return this.contextsByName.get(name);
    }

    hasContext(
        name: string
    ): boolean {

        return this.contextsByName.has(name);
    }

    hasView(
        name: string
    ): boolean {

        return this.viewsByName.has(name);
    }

    getFilterableFields(
        contextName: string
    ): readonly QueryableField[] {

        const context =
            this.getContext(contextName);

        return context.fields
            .filter(field =>
                field.isFilterable)
            .sort((a, b) =>
                a.name.localeCompare(b.name));
    }

    getSortableFields(
        contextName: string
    ): readonly QueryableField[] {

        const context =
            this.getContext(contextName);

        return context.fields
            .filter(field =>
                field.isSortable)
            .sort((a, b) =>
                a.name.localeCompare(b.name));
    }

    getSearchableFields(
        contextName: string
    ): readonly QueryableField[] {

        const context =
            this.getContext(contextName);

        return context.fields
            .filter(field =>
                field.isSearchable)
            .sort((a, b) => {

                const leftPriority =
                    a.searchPriority ?? Number.MAX_SAFE_INTEGER;

                const rightPriority =
                    b.searchPriority ?? Number.MAX_SAFE_INTEGER;

                if (leftPriority !== rightPriority) {
                    return leftPriority - rightPriority;
                }

                return a.name.localeCompare(b.name);
            });
    }

    getField(
        contextName: string,
        fieldName: string
    ): QueryableField {

        const field =
            this.tryGetField(
                contextName,
                fieldName);

        if (!field) {
            throw new Error(
                `Queryable field '${fieldName}' is not registered for context '${contextName}'.`);
        }

        return field;
    }

    tryGetField(
        contextName: string,
        fieldName: string
    ): QueryableField | undefined {

        const context =
            this.tryGetContext(contextName);

        if (!context) {
            return undefined;
        }

        return context.fields.find(field =>
            field.name === fieldName);
    }

    getFilterOperators(
        contextName: string,
        fieldName: string
    ): readonly string[] {

        const field =
            this.getField(
                contextName,
                fieldName);

        return field.filterOperators;
    }

    hasFilterableFields(
        contextName: string
    ): boolean {

        return this.getFilterableFields(contextName).length > 0;
    }

    hasSortableFields(
        contextName: string
    ): boolean {

        return this.getSortableFields(contextName).length > 0;
    }

    hasSearchableFields(
        contextName: string
    ): boolean {

        return this.getSearchableFields(contextName).length > 0;
    }

    getViewRegistration(
        viewName: string
    ): QueryableViewRegistration {

        const registration =
            this.viewRegistrationsByName.get(
                viewName);

        if (!registration) {

            throw new Error(
                `Queryable view '${viewName}' is not registered.`);
        }

        return registration;
    }

    getFilterableFieldsForView(
        viewName: string
    ): readonly QueryableField[] {

        const registration =
            this.getViewRegistration(
                viewName);

        return registration.context.fields
            .filter(field =>
                field.isFilterable)
            .sort((a, b) =>
                a.name.localeCompare(
                    b.name));
    }

    getSortableFieldsForView(
        viewName: string
    ): readonly QueryableField[] {

        const registration =
            this.getViewRegistration(
                viewName);

        return registration.context.fields
            .filter(field =>
                field.isSortable)
            .sort((a, b) =>
                a.name.localeCompare(
                    b.name));
    }

    getSearchableFieldsForView(
        viewName: string
    ): readonly QueryableField[] {

        const registration =
            this.getViewRegistration(
                viewName);

        return registration.context.fields
            .filter(field =>
                field.isSearchable)
            .sort((a, b) => {

                const leftPriority =
                    a.searchPriority ??
                    Number.MAX_SAFE_INTEGER;

                const rightPriority =
                    b.searchPriority ??
                    Number.MAX_SAFE_INTEGER;

                if (leftPriority !== rightPriority) {

                    return leftPriority - rightPriority;
                }

                return a.name.localeCompare(
                    b.name);
            });
    }


}