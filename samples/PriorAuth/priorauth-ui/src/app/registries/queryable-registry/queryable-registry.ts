import { Component, inject } from '@angular/core';
import { AsyncPipe } from '@angular/common';

import { QueryableConstraint, QueryableRecord, QueryableView } from '../../kaleido/models/queryable-registry';
import {
    RegistryCatalog,
    RegistryCatalogState,
    RegistryConflict,
    ServiceRegistrySnapshot
} from '../registry-catalog';

@Component({
    selector: 'priorauth-queryable-registry',
    standalone: true,
    imports: [AsyncPipe],
    templateUrl: './queryable-registry.html',
    styleUrl: './queryable-registry.scss'
})
export class QueryableRegistryViewer {
    private readonly registryCatalog =
        inject(RegistryCatalog);

    readonly state$ =
        this.registryCatalog.loadState();

    selectedService?: ServiceRegistrySnapshot;
    selectedContext?: QueryableRecord;
    selectedView?: QueryableView;

    refresh(): void {
        this.selectedService = undefined;
        this.selectedContext = undefined;
        this.selectedView = undefined;
        this.registryCatalog.refresh();
    }

    ensureSelection(
        snapshots: readonly ServiceRegistrySnapshot[]
    ): void {
        const queryableSnapshots =
            snapshots.filter(snapshot => snapshot.queryable.configured);

        if (!this.selectedService) {
            this.selectedService =
                queryableSnapshots.find(snapshot => snapshot.queryable.ok && (snapshot.queryable.data?.length ?? 0) > 0)
                ?? queryableSnapshots.find(snapshot => !snapshot.queryable.ok)
                ?? queryableSnapshots[0];
        }

        if (!this.selectedContext) {
            this.selectedContext =
                this.selectedService?.queryable.data?.[0];
        }

        if (!this.selectedView) {
            this.selectedView =
                this.selectedContext?.views[0];
        }
    }

    selectService(
        snapshot: ServiceRegistrySnapshot
    ): void {
        this.selectedService = snapshot;
        this.selectedContext = snapshot.queryable.data?.[0];
        this.selectedView = this.selectedContext?.views[0];
    }

    selectContext(
        context: QueryableRecord
    ): void {
        this.selectedContext = context;
        this.selectedView = context.views[0];
    }

    selectView(
        view: QueryableView
    ): void {
        this.selectedView = view;
    }

    getSelectedContexts(
        snapshots: readonly ServiceRegistrySnapshot[]
    ): readonly QueryableRecord[] {
        this.ensureSelection(snapshots);

        return this.selectedService?.queryable.data ?? [];
    }

    getTotalViews(
        snapshots: readonly ServiceRegistrySnapshot[]
    ): number {
        return snapshots.reduce(
            (sum, snapshot) =>
                sum + (snapshot.queryable.data?.reduce((inner, context) => inner + context.views.length, 0) ?? 0),
            0);
    }

    getTotalFields(
        snapshots: readonly ServiceRegistrySnapshot[]
    ): number {
        return snapshots.reduce(
            (sum, snapshot) =>
                sum + (snapshot.queryable.data?.reduce((inner, context) => inner + context.fields.length, 0) ?? 0),
            0);
    }

    getQueryableConflicts(
        state: RegistryCatalogState
    ): readonly RegistryConflict[] {
        return state.conflicts.filter(
            conflict =>
                conflict.type === 'queryable-context' ||
                conflict.type === 'queryable-view');
    }

    getOperators(
        operators: string[]
    ): string {
        return operators.join(', ');
    }

    formatConstraint(
        constraint: QueryableConstraint
    ): string {
        switch (constraint.type) {
            case 'StringLength': {
                const min = constraint.parameters.find(x => x.name === 'MinimumLength')?.value;
                const max = constraint.parameters.find(x => x.name === 'MaximumLength')?.value;

                return `String Length (${min}-${max})`;
            }

            case 'Range': {
                const min = constraint.parameters.find(x => x.name === 'Minimum')?.value;
                const max = constraint.parameters.find(x => x.name === 'Maximum')?.value;

                return `Range (${min}-${max})`;
            }

            default:
                return constraint.type;
        }
    }
}
