import { Component, inject } from '@angular/core';
import { AsyncPipe } from '@angular/common';

import {
    RegistryCatalog,
    RegistryCatalogState,
    RegistryConflict,
    ServiceRegistrySnapshot
} from '../registry-catalog';
import { ProcessFieldConstraintMetadata, ProcessStepRegistryRecord } from '../../kaleido/models/process-registry';

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
    selectedStep?: ProcessStepRegistryRecord;

    refresh(): void {
        this.selectedService = undefined;
        this.selectedStep = undefined;
        this.registryCatalog.refresh();
    }

    selectService(
        snapshot: ServiceRegistrySnapshot
    ): void {
        this.selectedService = snapshot;
        this.selectedStep = snapshot.process.data?.[0];
    }

    selectStep(
        step: ProcessStepRegistryRecord
    ): void {
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

        if (!this.selectedStep) {
            this.selectedStep =
                this.selectedService?.process.data?.[0];
        }
    }

    getSelectedSteps(
        snapshots: readonly ServiceRegistrySnapshot[]
    ): readonly ProcessStepRegistryRecord[] {
        this.ensureSelection(snapshots);

        return this.selectedService?.process.data ?? [];
    }

    getTotalSteps(
        snapshots: readonly ServiceRegistrySnapshot[]
    ): number {
        return snapshots.reduce(
            (sum, snapshot) => sum + (snapshot.process.data?.length ?? 0),
            0);
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
