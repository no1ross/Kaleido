import { Component, inject } from '@angular/core';
import { AsyncPipe } from '@angular/common';

import { QueryableConstraint, QueryableRecord, QueryableView } from '../../kaleido/models/queryable-registry';
import {
    RegistryCatalog,
    RegistryCatalogState,
    RegistryConflict,
    QueryableGroup
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

    selectedGroup?: QueryableGroup;
    selectedContext?: QueryableRecord;
    selectedView?: QueryableView;

    refresh(): void {
        this.selectedGroup = undefined;
        this.selectedContext = undefined;
        this.selectedView = undefined;
        this.registryCatalog.refresh();
    }

    ensureSelection(state: RegistryCatalogState): void {
        if (!this.selectedGroup) {
            this.selectedGroup = state.queryableGroups[0];
        }
        if (!this.selectedContext) {
            this.selectedContext = this.selectedGroup?.contexts[0];
        }
        if (!this.selectedView) {
            this.selectedView = this.selectedContext?.views[0];
        }
    }

    selectContext(group: QueryableGroup, context: QueryableRecord): void {
        this.selectedGroup = group;
        this.selectedContext = context;
        this.selectedView = context.views[0];
    }

    selectView(view: QueryableView): void {
        this.selectedView = view;
    }

    getTotalViews(state: RegistryCatalogState): number {
        return state.queryableGroups.reduce(
            (sum, g) => sum + g.contexts.reduce((s, c) => s + c.views.length, 0), 0);
    }

    getTotalFields(state: RegistryCatalogState): number {
        return state.queryableGroups.reduce(
            (sum, g) => sum + g.contexts.reduce((s, c) => s + c.fields.length, 0), 0);
    }

    getQueryableConflicts(state: RegistryCatalogState): readonly RegistryConflict[] {
        return state.conflicts.filter(c =>
            c.type === 'queryable-context' || c.type === 'queryable-view');
    }

    getOperators(operators: string[]): string {
        return operators.join(', ');
    }

    formatConstraint(constraint: QueryableConstraint): string {
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
