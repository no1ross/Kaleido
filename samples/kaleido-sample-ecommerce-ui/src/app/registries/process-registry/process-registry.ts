import { Component } from '@angular/core';
import { ProcessRegistry } from '../../kaleido/services/process-registry';
import { ProcessStepRegistryRecord, ProcessFieldConstraintMetadata } from '../../kaleido/models/process-registry';

@Component({
    selector: 'kaleido-process-registry',
    standalone: true,
    templateUrl: './process-registry.html',
    styleUrl: './process-registry.scss'
})
export class ProcessRegistryViewer {

    readonly steps: readonly ProcessStepRegistryRecord[];

    selectedStep?: ProcessStepRegistryRecord;

    constructor(
        private readonly processRegistry: ProcessRegistry
    ) {
        this.steps =
            Array.from(
                processRegistry.getSteps())
                .sort((a, b) =>
                    a.name.localeCompare(b.name));

        this.selectedStep =
            this.steps[0];
    }

    selectStep(
        step: ProcessStepRegistryRecord
    ): void {

        this.selectedStep = step;
    }

    formatConstraint(
        constraint: ProcessFieldConstraintMetadata
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