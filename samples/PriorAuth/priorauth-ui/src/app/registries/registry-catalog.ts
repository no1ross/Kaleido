import { Injectable, inject } from '@angular/core';
import { Observable, ReplaySubject, catchError, map, of, shareReplay, switchMap, tap } from 'rxjs';
import { HttpClient } from '@angular/common/http';

import {
    ProcessProcessorRegistryRecord,
    ProcessStepRegistryRecord
} from '../kaleido/models/process-registry';
import {
    QueryableRecord,
    QueryableView,
    QueryableViewRegistration
} from '../kaleido/models/queryable-registry';
import {
    buildRegistryUrl,
    getServiceRoutes
} from '../../configuration/urlConfig';
import { ProcessRegistry } from '../kaleido/services/process-registry';
import { QueryableRegistry } from '../kaleido/services/queryable-registry';

export interface RegistryClientError {
    readonly clientName: string;
    readonly clientType: 'Process' | 'Queryable';
    readonly reason: string;
}

export interface RegistryConflict {
    readonly type: 'process-step' | 'queryable-context' | 'queryable-view';
    readonly name: string;
    readonly services: readonly string[];
}

export interface ProcessorGroup {
    readonly serviceName: string;
    readonly processors: readonly ProcessProcessorRegistryRecord[];
}

export interface QueryableGroup {
    readonly serviceName: string;
    readonly contexts: readonly QueryableRecord[];
}

export interface RegistryCatalogState {
    readonly ok: boolean;
    readonly error?: string;
    readonly url: string;
    readonly clientErrors: readonly RegistryClientError[];
    readonly processorGroups: readonly ProcessorGroup[];
    readonly queryableGroups: readonly QueryableGroup[];
    readonly processSteps: readonly ProcessStepEntry[];
    readonly queryableViews: readonly QueryableViewRegistration[];
    readonly conflicts: readonly RegistryConflict[];
}

export interface ProcessStepEntry {
    readonly serviceName: string;
    readonly processor: ProcessProcessorRegistryRecord;
    readonly step: ProcessStepRegistryRecord;
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
                this.fetchRegistry()),
            shareReplay(1));

    constructor() {
        this.refresh();
    }

    loadState(): Observable<RegistryCatalogState> {
        return this.state$;
    }

    refresh(): void {
        console.log('[RegistryCatalog] Refreshing...');
        this.refreshTrigger.next();
    }

    private fetchRegistry(): Observable<RegistryCatalogState> {
        const routerService = getServiceRoutes().find(s => s.key === 'router');

        if (!routerService?.registryPath) {
            throw new Error('Router service with registryPath is required.');
        }

        const url = buildRegistryUrl(routerService.registryPath);

        console.log(`[RegistryCatalog] Loading from ${url}...`);

        const started = performance.now();

        return this.http
            .get<{ processes: ProcessProcessorRegistryRecord[]; queryables: QueryableRecord[]; clientErrors?: RegistryClientError[] }>(url)
            .pipe(
                map(data => {
                    const duration = Math.round(performance.now() - started);
                    const clientErrors: readonly RegistryClientError[] = data.clientErrors ?? [];

                    console.group('[RegistryCatalog]');
                    console.log(`Loaded in ${duration}ms — Processors: ${data.processes.length}, Queryables: ${data.queryables.length}`);
                    console.log('Url', url);
                    if (clientErrors.length > 0) {
                        console.warn(`Partial registry — ${clientErrors.length} downstream client(s) failed:`);
                        console.table(clientErrors.map(e => ({ Client: e.clientName, Type: e.clientType, Reason: e.reason })));
                    }
                    console.groupEnd();

                    return this.buildState(url, data.processes, data.queryables, clientErrors);
                }),
                tap(state => {
                    this.processRegistry.populateRegistry(
                        state.processSteps,
                        state.conflicts.filter(c => c.type === 'process-step'));

                    this.queryableRegistry.populateRegistry(
                        state.queryableGroups.flatMap(g => g.contexts.map(ctx => ({ context: ctx }))),
                        state.queryableViews,
                        state.conflicts.filter(c =>
                            c.type === 'queryable-context' ||
                            c.type === 'queryable-view'));
                }),
                catchError(error => {
                    const msg = this.formatError(error);
                    console.error(`[RegistryCatalog] Failed to load registry from ${url}.`, error);
                    return of({
                        ok: false,
                        error: msg,
                        url,
                        clientErrors: [],
                        processorGroups: [],
                        queryableGroups: [],
                        processSteps: [],
                        queryableViews: [],
                        conflicts: []
                    } satisfies RegistryCatalogState);
                })
            );
    }

    private buildState(
        url: string,
        processes: readonly ProcessProcessorRegistryRecord[],
        queryables: readonly QueryableRecord[],
        clientErrors: readonly RegistryClientError[]
    ): RegistryCatalogState {
        // Group processors by serviceName
        const processorsByService = new Map<string, ProcessProcessorRegistryRecord[]>();
        for (const processor of processes) {
            const group = processorsByService.get(processor.serviceName) ?? [];
            group.push(processor);
            processorsByService.set(processor.serviceName, group);
        }

        // Group queryables by service key derived from first path segment of metadataUrl
        const queryablesByService = new Map<string, QueryableRecord[]>();
        for (const context of queryables) {
            const key = this.serviceKeyFromUrl(context.metadataUrl);
            const group = queryablesByService.get(key) ?? [];
            group.push(context);
            queryablesByService.set(key, group);
        }

        const processSteps: ProcessStepEntry[] =
            processes.flatMap(processor =>
                processor.steps.map(step => ({
                    serviceName: processor.serviceName,
                    processor,
                    step
                })));

        const queryableViews: QueryableViewRegistration[] =
            queryables.flatMap(context =>
                context.views.map(view => ({ context, view })));

        const conflicts = [
            ...this.detectConflicts(
                'process-step',
                processSteps,
                e => `${e.processor.name}:${e.step.name}`,
                e => e.serviceName),
            ...this.detectConflicts(
                'queryable-context',
                queryables.map(ctx => ({ key: this.serviceKeyFromUrl(ctx.metadataUrl), ctx })),
                e => e.ctx.name,
                e => e.key),
            ...this.detectConflicts(
                'queryable-view',
                queryableViews.map(qv => ({ key: this.serviceKeyFromUrl(qv.context.metadataUrl), qv })),
                e => e.qv.view.name,
                e => e.key)
        ];

        const conflictedStepNames = new Set(
            conflicts.filter(c => c.type === 'process-step').map(c => c.name));

        const conflictedContextNames = new Set(
            conflicts.filter(c => c.type === 'queryable-context').map(c => c.name));

        const conflictedViewNames = new Set(
            conflicts.filter(c => c.type === 'queryable-view').map(c => c.name));

        const processorGroups: ProcessorGroup[] = Array.from(processorsByService.entries())
            .sort(([a], [b]) => a.localeCompare(b))
            .map(([serviceName, procs]) => ({ serviceName, processors: procs }));

        const queryableGroups: QueryableGroup[] = Array.from(queryablesByService.entries())
            .sort(([a], [b]) => a.localeCompare(b))
            .map(([serviceName, contexts]) => ({ serviceName, contexts }));

        return {
            ok: true,
            url,
            clientErrors,
            processorGroups,
            queryableGroups,
            processSteps: processSteps.filter(e =>
                !conflictedStepNames.has(`${e.processor.name}:${e.step.name}`)),
            queryableViews: queryableViews.filter(qv =>
                !conflictedContextNames.has(qv.context.name) &&
                !conflictedViewNames.has(qv.view.name)),
            conflicts
        };
    }

    private serviceKeyFromUrl(url: string): string {
        return url.replace(/^\/+/, '').split('/')[0] ?? '';
    }

    private detectConflicts<TEntry>(
        type: RegistryConflict['type'],
        entries: readonly TEntry[],
        getName: (entry: TEntry) => string,
        getServiceKey: (entry: TEntry) => string
    ): RegistryConflict[] {
        const servicesByName = new Map<string, Set<string>>();

        for (const entry of entries) {
            const name = getName(entry);
            const services = servicesByName.get(name) ?? new Set<string>();
            services.add(getServiceKey(entry));
            servicesByName.set(name, services);
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
                    `[RegistryCatalog] Conflict: ${type} '${name}' across: ${conflict.services.join(', ')}`);

                return conflict;
            });
    }

    private formatError(error: unknown): string {
        if (typeof error === 'object' && error !== null && 'message' in error) {
            const message = error.message;
            if (typeof message === 'string' && message.length > 0) {
                return message;
            }
        }
        return 'Request failed.';
    }
}
