import { Component } from '@angular/core';

import { QueryableView, QueryableRecord, QueryableConstraint } from '../../kaleido/models/queryable-registry';
import { QueryableRegistry } from '../../kaleido/services/queryable-registry';

@Component({
    selector: 'kaleido-queryable-registry',
    standalone: true,
    templateUrl: './queryable-registry.html',
    styleUrl: './queryable-registry.scss'
})
export class QueryableRegistryViewer {

    readonly contexts: QueryableRecord[];

    selectedContext?: QueryableRecord;

    selectedView?: QueryableView;

    constructor(
        private readonly queryableRegistry: QueryableRegistry
    ) {

        this.contexts =
            [...queryableRegistry.getContexts()]
                .sort((a, b) =>
                    a.name.localeCompare(b.name));

        this.selectedContext =
            this.contexts[0];

        this.selectedView =
            this.selectedContext?.views[0];
    }

    selectContext(
        context: QueryableRecord
    ): void {

        this.selectedContext =
            context;

        this.selectedView =
            context.views[0];
    }

    selectView(
        view: QueryableView
    ): void {

        this.selectedView =
            view;
    }

    get totalViews(): number {

        return this.contexts.reduce(
            (sum, context) =>
                sum + context.views.length,
            0);
    }

    get totalFields(): number {

        return this.contexts.reduce(
            (sum, context) =>
                sum + context.fields.length,
            0);
    }

    getOperators(
        operators: string[]
    ): string {

        return operators.join(', ');
    }

    getPageableText(
        view: QueryableView
    ): string {

        if (!view.pageable) {
            return 'No';
        }

        return `Yes (${view.pageable.defaultSize}/${view.pageable.maxSize})`;
    }

    formatConstraint(
        constraint: QueryableConstraint
    ): string {

        switch (constraint.type) {

            case 'StringLength': {

                const min =
                    constraint.parameters.find(
                        x => x.name === 'MinimumLength')
                        ?.value;

                const max =
                    constraint.parameters.find(
                        x => x.name === 'MaximumLength')
                        ?.value;

                return `String Length (${min}-${max})`;
            }

            case 'Range': {

                const min =
                    constraint.parameters.find(
                        x => x.name === 'Minimum')
                        ?.value;

                const max =
                    constraint.parameters.find(
                        x => x.name === 'Maximum')
                        ?.value;

                return `Range (${min}-${max})`;
            }

            default:
                return constraint.type;
        }
    }
}