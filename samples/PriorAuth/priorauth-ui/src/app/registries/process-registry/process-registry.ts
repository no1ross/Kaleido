import { Component, inject } from '@angular/core';
import { AsyncPipe } from '@angular/common';

import {
    RegistryCatalog,
    RegistryCatalogState,
    RegistryConflict,
    ProcessorGroup
} from '../registry-catalog';
import {
    ProcessFieldConstraintMetadata,
    ProcessProcessorRegistryRecord,
    ProcessStepRegistryRecord,
    ProcessStepSummary
} from '../../kaleido/models/process-registry';

@Component({
    selector: 'priorauth-process-registry',
    standalone: true,
    imports: [AsyncPipe],
    templateUrl: './process-registry.html',
    styleUrl: './process-registry.scss'
})
export class ProcessRegistryViewer {
    private readonly registryCatalog =
        inject(RegistryCatalog);

    readonly state$ =
        this.registryCatalog.loadState();

    selectedGroup?: ProcessorGroup;
    selectedProcessor?: ProcessProcessorRegistryRecord;
    selectedStep?: ProcessStepRegistryRecord;

    refresh(): void {
        this.selectedGroup = undefined;
        this.selectedProcessor = undefined;
        this.selectedStep = undefined;
        this.registryCatalog.refresh();
    }

    ensureSelection(state: RegistryCatalogState): void {
        if (!this.selectedGroup) {
            this.selectedGroup = state.processorGroups[0];
        }
        if (!this.selectedProcessor) {
            this.selectedProcessor = this.selectedGroup?.processors[0];
        }
        if (!this.selectedStep) {
            this.selectedStep = this.selectedProcessor?.steps[0];
        }
    }

    selectProcessor(
        group: ProcessorGroup,
        processor: ProcessProcessorRegistryRecord
    ): void {
        this.selectedGroup = group;
        this.selectedProcessor = processor;
        this.selectedStep = processor.steps[0];
    }

    selectStep(
        group: ProcessorGroup,
        processor: ProcessProcessorRegistryRecord,
        step: ProcessStepRegistryRecord
    ): void {
        this.selectedGroup = group;
        this.selectedProcessor = processor;
        this.selectedStep = step;
    }

    getTotalSteps(state: RegistryCatalogState): number {
        return state.processorGroups.reduce(
            (sum, g) => sum + g.processors.reduce((s, p) => s + p.steps.length, 0), 0);
    }

    getProcessConflicts(state: RegistryCatalogState): readonly RegistryConflict[] {
        return state.conflicts.filter(c => c.type === 'process-step');
    }

    getInitialStepNames(processor: ProcessProcessorRegistryRecord): string {
        return processor.initialSteps
            .map((step: ProcessStepSummary) => step.displayName ?? step.name)
            .join(', ');
    }

    formatConstraint(constraint: ProcessFieldConstraintMetadata): string {
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
