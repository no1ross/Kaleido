import { Injectable } from '@angular/core';

import {
    QueryableRecord,
    QueryableView,
    QueryableViewRegistration
} from '../models/queryable-registry';
import { RegistryConflict } from '../../registries/registry-catalog';

@Injectable({
    providedIn: 'root'
})
export class QueryableRegistry {
    private readonly contextsByName =
        new Map<string, QueryableRecord>();

    private readonly viewsByName =
        new Map<string, QueryableViewRegistration>();

    private conflicts: readonly RegistryConflict[] = [];

    populateRegistry(
        contexts: readonly { context: QueryableRecord }[],
        views: readonly QueryableViewRegistration[],
        conflicts: readonly RegistryConflict[]
    ): void {
        this.contextsByName.clear();
        this.viewsByName.clear();
        this.conflicts = conflicts;

        for (const { context } of contexts) {
            this.contextsByName.set(context.name, context);
        }

        for (const registration of views) {
            this.viewsByName.set(registration.view.name, registration);
        }
    }

    getContext(name: string): QueryableRecord {
        const entry = this.tryGetContext(name);

        if (!entry) {
            throw new Error(`Queryable context '${name}' is not registered.`);
        }

        return entry;
    }

    tryGetContext(name: string): QueryableRecord | undefined {
        return this.contextsByName.get(name);
    }

    getViewRegistration(name: string): QueryableViewRegistration {
        const entry = this.tryGetViewRegistration(name);

        if (!entry) {
            throw new Error(`Queryable view '${name}' is not registered.`);
        }

        return entry;
    }

    tryGetViewRegistration(name: string): QueryableViewRegistration | undefined {
        return this.viewsByName.get(name);
    }

    getView(name: string): QueryableView {
        return this.getViewRegistration(name).view;
    }

    tryGetView(name: string): QueryableView | undefined {
        return this.tryGetViewRegistration(name)?.view;
    }

    getContexts(): readonly QueryableRecord[] {
        return Array.from(this.contextsByName.values());
    }

    getViews(): readonly QueryableView[] {
        return Array.from(this.viewsByName.values()).map(e => e.view);
    }

    getConflicts(): readonly RegistryConflict[] {
        return this.conflicts;
    }
}
