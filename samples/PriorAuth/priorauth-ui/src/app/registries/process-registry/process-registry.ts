import { Component, inject } from '@angular/core';
import { AsyncPipe } from '@angular/common';

import {
    RegistryCatalog,
    RegistryCatalogState,
    RegistryConflict,
    ServiceRegistrySnapshot
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

    selectedService?: ServiceRegistrySnapshot;
    selectedProcessor?: ProcessProcessorRegistryRecord;
    selectedStep?: ProcessStepRegistryRecord;

    refresh(): void {
        this.selectedService = undefined;
        this.selectedProcessor = undefined;
        this.selectedStep = undefined;
        this.registryCatalog.refresh();
    }

    selectService(
        snapshot: ServiceRegistrySnapshot
    ): void {
        this.selectedService = snapshot;
        this.selectedProcessor = snapshot.process.data?.[0];
        this.selectedStep = this.selectedProcessor?.steps[0];
    }

    selectProcessor(
        processor: ProcessProcessorRegistryRecord
    ): void {
        this.selectedProcessor = processor;
        this.selectedStep = processor.steps[0];
    }

    selectStep(
        snapshot: ServiceRegistrySnapshot,
        processor: ProcessProcessorRegistryRecord,
        step: ProcessStepRegistryRecord
    ): void {
        this.selectedService = snapshot;
        this.selectedProcessor = processor;
        this.selectedStep = step;
    }

    ensureSelection(
        snapshots: readonly ServiceRegistrySnapshot[]
    ): void {
        const processSnapshots =
            snapshots.filter(snapshot => snapshot.process.configured);

        if (!this.selectedService) {
            this.selectedService =
                processSnapshots.find(snapshot => snapshot.process.ok && (snapshot.process.data?.length ?? 0) > 0)
                ?? processSnapshots.find(snapshot => !snapshot.process.ok)
                ?? processSnapshots[0];
        }

        if (!this.selectedProcessor) {
            this.selectedProcessor =
                this.selectedService?.process.data?.[0];
        }

        if (!this.selectedStep) {
            this.selectedStep =
                this.selectedProcessor?.steps[0];
        }
    }

    getParticipants(
        snapshot: ServiceRegistrySnapshot
    ): readonly ProcessProcessorRegistryRecord[] {
        return snapshot.process.data ?? [];
    }

    getTotalSteps(
        snapshots: readonly ServiceRegistrySnapshot[]
    ): number {
        return snapshots.reduce(
            (sum, snapshot) =>
                sum + (snapshot.process.data?.reduce((inner, processor) => inner + processor.steps.length, 0) ?? 0),
            0);
    }

    getInitialStepNames(
        processor: ProcessProcessorRegistryRecord
    ): string {
        return processor.initialSteps
            .map((step: ProcessStepSummary) => step.displayName ?? step.name)
            .join(', ');
    }

    getProcessConflicts(
        state: RegistryCatalogState
    ): readonly RegistryConflict[] {
        return state.conflicts.filter(
            conflict => conflict.type === 'process-step');
    }

    formatConstraint(
        constraint: ProcessFieldConstraintMetadata
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
