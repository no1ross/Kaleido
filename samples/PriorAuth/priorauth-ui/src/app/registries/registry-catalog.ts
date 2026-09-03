import { Injectable, inject } from '@angular/core';
import { Observable, ReplaySubject, catchError, forkJoin, map, of, shareReplay, switchMap, tap } from 'rxjs';
import { HttpClient } from '@angular/common/http';

import {
    ProcessProcessorRegistryRecord,
    ProcessStepRegistryRecord,
    ServiceProcessProcessorRegistryRecord,
    ServiceProcessStepRegistryRecord
} from '../kaleido/models/process-registry';
import {
    QueryableRecord,
    QueryableView,
    ServiceQueryableRecord,
    ServiceQueryableViewRegistration
} from '../kaleido/models/queryable-registry';
import {
    buildRegistryUrl,
    getServiceRoutes,
    PriorAuthServiceRouteConfig
} from '../../configuration/urlConfig';
import { ProcessRegistry } from '../kaleido/services/process-registry';
import { QueryableRegistry } from '../kaleido/services/queryable-registry';

export interface ServiceRegistrySnapshot {
    readonly service: PriorAuthServiceRouteConfig;
    readonly process: RegistryLoadResult<ProcessProcessorRegistryRecord[]>;
    readonly queryable: RegistryLoadResult<QueryableRecord[]>;
}

export interface RegistryLoadResult<T> {
    readonly configured: boolean;
    readonly ok: boolean;
    readonly url?: string;
    readonly data?: T;
    readonly error?: string;
}

export interface RegistryConflict {
    readonly type: 'process-step' | 'queryable-context' | 'queryable-view';
    readonly name: string;
    readonly services: readonly string[];
}

export interface RegistryCatalogState {
    readonly snapshots: readonly ServiceRegistrySnapshot[];
    readonly processSteps: readonly ServiceProcessStepRegistryRecord[];
    readonly queryableContexts: readonly ServiceQueryableRecord[];
    readonly queryableViews: readonly ServiceQueryableViewRegistration[];
    readonly conflicts: readonly RegistryConflict[];
}

@Injectable({
    providedIn: 'root'
})
export class RegistryCatalog {
    private readonly http =
        inject(HttpClient);

    private readonly processRegistry =
        inject(ProcessRegistry);

    private readonly queryableRegistry =
        inject(QueryableRegistry);

    private readonly refreshTrigger =
        new ReplaySubject<void>(1);

    private readonly state$ =
        this.refreshTrigger.pipe(
            switchMap(() =>
                this.createStateObservable()),
            shareReplay(1));

    constructor() {
        this.refresh();
    }

    loadAll(): Observable<readonly ServiceRegistrySnapshot[]> {
        return this.state$.pipe(
            map(state => state.snapshots));
    }

    loadState(): Observable<RegistryCatalogState> {
        return this.state$;
    }

    refresh(): void {
        console.log('[RegistryCatalog] Refreshing registry catalog...');
        this.refreshTrigger.next();
    }

    private createStateObservable(): Observable<RegistryCatalogState> {
        const requests =
            getServiceRoutes().map(service =>
                forkJoin({
                    process: this.loadProcessRegistry(service),
                    queryable: this.loadQueryableRegistry(service)
                }).pipe(
                    map(result => ({
                        service,
                        process: result.process,
                        queryable: result.queryable
                    } satisfies ServiceRegistrySnapshot))));

        return forkJoin(requests)
            .pipe(
                map(snapshots =>
                    this.buildState(snapshots)),
                tap(state => {
                    this.processRegistry.populateRegistry(
                        state.processSteps,
                        state.conflicts.filter(conflict => conflict.type === 'process-step'));

                    this.queryableRegistry.populateRegistry(
                        state.queryableContexts,
                        state.queryableViews,
                        state.conflicts.filter(conflict =>
                            conflict.type === 'queryable-context' ||
                            conflict.type === 'queryable-view'));
                }));
    }

    private buildState(
        snapshots: readonly ServiceRegistrySnapshot[]
    ): RegistryCatalogState {
        const processParticipants =
            snapshots.flatMap(snapshot =>
                (snapshot.process.ok
                    ? snapshot.process.data ?? []
                    : [])
                    .map(processor => ({
                        service: snapshot.service,
                        processor
                    } satisfies ServiceProcessProcessorRegistryRecord)));

        const processSteps =
            processParticipants.flatMap(entry =>
                entry.processor.steps.map(step => ({
                    service: entry.service,
                    processor: entry.processor,
                    step
                } satisfies ServiceProcessStepRegistryRecord)));

        const queryableContexts =
            snapshots.flatMap(snapshot =>
                (snapshot.queryable.ok
                    ? snapshot.queryable.data ?? []
                    : [])
                    .map(context => ({
                        service: snapshot.service,
                        context
                    } satisfies ServiceQueryableRecord)));

        const queryableViews =
            queryableContexts.flatMap(entry =>
                entry.context.views.map(view => ({
                    service: entry.service,
                    context: entry.context,
                    view
                } satisfies ServiceQueryableViewRegistration)));

        const conflicts = [
            ...this.detectConflicts(
                'process-step',
                processSteps,
                entry => entry.step.name,
                entry => entry.service.displayName),
            ...this.detectConflicts(
                'queryable-context',
                queryableContexts,
                entry => entry.context.name,
                entry => entry.service.displayName),
            ...this.detectConflicts(
                'queryable-view',
                queryableViews,
                entry => entry.view.name,
                entry => entry.service.displayName)
        ];

        if (conflicts.length > 0) {
            console.group('[RegistryCatalog] Duplicate registry conflicts detected.');
            console.table(conflicts.map(conflict => ({
                Type: conflict.type,
                Name: conflict.name,
                Services: conflict.services.join(', ')
            })));
            console.groupEnd();
        }

        return {
            snapshots,
            processSteps: this.filterConflictedEntries(
                processSteps,
                conflicts,
                'process-step',
                entry => entry.step.name),
            queryableContexts: this.filterConflictedEntries(
                queryableContexts,
                conflicts,
                'queryable-context',
                entry => entry.context.name),
            queryableViews: this.filterConflictedEntries(
                queryableViews,
                conflicts,
                'queryable-view',
                entry => entry.view.name),
            conflicts
        };
    }

    private detectConflicts<TEntry>(
        type: RegistryConflict['type'],
        entries: readonly TEntry[],
        getName: (entry: TEntry) => string,
        getServiceName: (entry: TEntry) => string
    ): RegistryConflict[] {
        const servicesByName =
            new Map<string, Set<string>>();

        for (const entry of entries) {
            const name =
                getName(entry);

            const services =
                servicesByName.get(name) ?? new Set<string>();

            services.add(
                getServiceName(entry));

            servicesByName.set(
                name,
                services);
        }

        return Array.from(servicesByName.entries())
            .filter(([, services]) => services.size > 1)
            .map(([name, services]) => {
                const conflict = {
                    type,
                    name,
                    services: Array.from(services.values())
                } satisfies RegistryConflict;

                console.error(
                    `[RegistryCatalog] Duplicate ${type} '${name}' detected across services: ${conflict.services.join(', ')}.`);

                return conflict;
            });
    }

    private filterConflictedEntries<TEntry>(
        entries: readonly TEntry[],
        conflicts: readonly RegistryConflict[],
        type: RegistryConflict['type'],
        getName: (entry: TEntry) => string
    ): TEntry[] {
        const conflictedNames =
            new Set(
                conflicts
                    .filter(conflict => conflict.type === type)
                    .map(conflict => conflict.name));

        return entries.filter(entry =>
            !conflictedNames.has(
                getName(entry)));
    }

    private loadProcessRegistry(
        service: PriorAuthServiceRouteConfig
    ): Observable<RegistryLoadResult<ProcessProcessorRegistryRecord[]>> {
        if (!service.processRegistryPath) {
            return of({
                configured: false,
                ok: false,
                error: 'Not configured.'
            });
        }

        const url =
            buildRegistryUrl(
                service,
                service.processRegistryPath);

        console.log(
            `[ProcessRegistry:${service.key}] Loading process registry from ${url}...`);

        const started =
            performance.now();

        return this.http
            .get<ProcessProcessorRegistryRecord[]>(url)
            .pipe(
                map(data => ({
                    configured: true,
                    ok: true,
                    url,
                    data
                })),
                tap(result => {
                    const duration =
                        Math.round(
                            performance.now() - started);

                    console.group(`[ProcessRegistry:${service.key}]`);
                    console.log(`Loaded ${result.data?.length ?? 0} processors in ${duration}ms.`);
                    console.log('Service', service.displayName);
                    console.log('Url', url);
                    console.table(
                        (result.data ?? []).map(processor => ({
                            Name: processor.name,
                            DisplayName: processor.displayName,
                            InitialSteps: processor.initialSteps.length,
                            Steps: processor.steps.length
                        })));
                    console.groupEnd();
                }),
                catchError(error => {
                    const formattedError =
                        this.formatError(error);

                    console.error(
                        `[ProcessRegistry:${service.key}] Failed to load process registry from ${url}.`,
                        error);

                    return of({
                        configured: true,
                        ok: false,
                        url,
                        error: formattedError
                    });
                })
            );
    }

    private loadQueryableRegistry(
        service: PriorAuthServiceRouteConfig
    ): Observable<RegistryLoadResult<QueryableRecord[]>> {
        if (!service.queryableRegistryPath) {
            return of({
                configured: false,
                ok: false,
                error: 'Not configured.'
            });
        }

        const url =
            buildRegistryUrl(
                service,
                service.queryableRegistryPath);

        console.log(
            `[QueryableRegistry:${service.key}] Loading queryable registry from ${url}...`);

        const started =
            performance.now();

        return this.http
            .get<QueryableRecord[]>(url)
            .pipe(
                map(data => ({
                    configured: true,
                    ok: true,
                    url,
                    data
                })),
                tap(result => {
                    const duration =
                        Math.round(
                            performance.now() - started);

                    console.group(`[QueryableRegistry:${service.key}]`);
                    console.log(`Loaded ${result.data?.length ?? 0} contexts in ${duration}ms.`);
                    console.log('Service', service.displayName);
                    console.log('Url', url);
                    console.table(
                        (result.data ?? []).map(record => ({
                            Name: record.name,
                            DisplayName: record.displayName,
                            Views: record.views.length,
                            Fields: record.fields.length
                        })));
                    console.groupEnd();
                }),
                catchError(error => {
                    const formattedError =
                        this.formatError(error);

                    console.error(
                        `[QueryableRegistry:${service.key}] Failed to load queryable registry from ${url}.`,
                        error);

                    return of({
                        configured: true,
                        ok: false,
                        url,
                        error: formattedError
                    });
                })
            );
    }

    private formatError(
        error: unknown
    ): string {
        if (typeof error === 'object' && error !== null && 'message' in error) {
            const message = error.message;

            if (typeof message === 'string' && message.length > 0) {
                return message;
            }
        }

        return 'Request failed.';
    }
}
