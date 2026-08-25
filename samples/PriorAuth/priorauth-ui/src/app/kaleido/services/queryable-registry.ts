import { Injectable } from '@angular/core';

import {
    QueryableRecord,
    QueryableView,
    ServiceQueryableRecord,
    ServiceQueryableViewRegistration
} from '../models/queryable-registry';
import { RegistryConflict } from '../../registries/registry-catalog';
import { PriorAuthServiceRouteConfig } from '../../../configuration/urlConfig';

@Injectable({
    providedIn: 'root'
})
export class QueryableRegistry {
    private readonly contextsByName =
        new Map<string, ServiceQueryableRecord>();

    private readonly viewsByName =
        new Map<string, ServiceQueryableViewRegistration>();

    private conflicts: readonly RegistryConflict[] = [];

    populateRegistry(
        contexts: readonly ServiceQueryableRecord[],
        views: readonly ServiceQueryableViewRegistration[],
        conflicts: readonly RegistryConflict[]
    ): void {
        this.contextsByName.clear();
        this.viewsByName.clear();
        this.conflicts = conflicts;

        for (const context of contexts) {
            this.contextsByName.set(
                context.context.name,
                context);
        }

        for (const view of views) {
            this.viewsByName.set(
                view.view.name,
                view);
        }
    }

    getContext(
        name: string
    ): QueryableRecord {
        return this.getServiceContext(name).context;
    }

    getServiceContext(
        name: string
    ): ServiceQueryableRecord {
        const entry =
            this.tryGetServiceContext(name);

        if (!entry) {
            throw new Error(
                `Queryable context '${name}' is not registered.`);
        }

        return entry;
    }

    tryGetContext(
        name: string
    ): QueryableRecord | undefined {
        return this.tryGetServiceContext(name)?.context;
    }

    tryGetServiceContext(
        name: string
    ): ServiceQueryableRecord | undefined {
        return this.contextsByName.get(name);
    }

    getView(
        name: string
    ): QueryableView {
        return this.getViewRegistration(name).view;
    }

    getViewRegistration(
        name: string
    ): ServiceQueryableViewRegistration {
        const entry =
            this.tryGetViewRegistration(name);

        if (!entry) {
            throw new Error(
                `Queryable view '${name}' is not registered.`);
        }

        return entry;
    }

    tryGetView(
        name: string
    ): QueryableView | undefined {
        return this.tryGetViewRegistration(name)?.view;
    }

    tryGetViewRegistration(
        name: string
    ): ServiceQueryableViewRegistration | undefined {
        return this.viewsByName.get(name);
    }

    getContexts(): readonly QueryableRecord[] {
        return Array.from(this.contextsByName.values())
            .map(entry => entry.context);
    }

    getViews(): readonly QueryableView[] {
        return Array.from(this.viewsByName.values())
            .map(entry => entry.view);
    }

    getServiceForContext(
        name: string
    ): PriorAuthServiceRouteConfig {
        return this.getServiceContext(name).service;
    }

    getServiceForView(
        name: string
    ): PriorAuthServiceRouteConfig {
        return this.getViewRegistration(name).service;
    }

    getConflicts(): readonly RegistryConflict[] {
        return this.conflicts;
    }
}
