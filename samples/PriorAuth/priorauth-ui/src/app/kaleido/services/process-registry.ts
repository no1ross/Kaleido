import { Injectable, inject } from '@angular/core';

import {
    ProcessProcessorRegistryRecord,
    ProcessStepRegistryRecord
} from '../models/process-registry';
import { RegistryConflict, ProcessStepEntry } from '../../registries/registry-catalog';
import { ProcessStateService } from '../../process/services/process-state-service';

export interface ProcessStepRegistration {
    readonly serviceName: string;
    readonly processor: ProcessProcessorRegistryRecord;
    readonly step: ProcessStepRegistryRecord;
}

@Injectable({
    providedIn: 'root'
})
export class ProcessRegistry {
    // Keyed by "processorName:stepName" to prevent collisions when the same
    // step name exists on multiple processors.
    private readonly stepsByKey =
        new Map<string, ProcessStepRegistration>();

    private conflicts: readonly RegistryConflict[] = [];

    private readonly processState =
        inject(ProcessStateService);

    private static makeKey(processorName: string, stepName: string): string {
        return `${processorName.toLowerCase()}:${stepName.toLowerCase()}`;
    }

    populateRegistry(
        steps: readonly ProcessStepEntry[],
        conflicts: readonly RegistryConflict[]
    ): void {
        this.stepsByKey.clear();
        this.conflicts = conflicts;

        for (const entry of steps) {
            this.stepsByKey.set(
                ProcessRegistry.makeKey(entry.processor.name, entry.step.name),
                { serviceName: entry.serviceName, processor: entry.processor, step: entry.step });
        }

        // Set the initial processor from the processor marked IsEntryProcessor.
        const entryEntry = steps.find(s => s.processor.isEntryProcessor);
        if (entryEntry) {
            this.processState.setCurrentProcessor(entryEntry.processor.name);
        }
    }

    getRegistration(
        processorName: string,
        stepName: string
    ): ProcessStepRegistration {
        const entry = this.tryGetRegistration(processorName, stepName);

        if (!entry) {
            throw new Error(
                `Process step '${stepName}' on processor '${processorName}' is not registered.`);
        }

        return entry;
    }

    tryGetRegistration(
        processorName: string,
        stepName: string
    ): ProcessStepRegistration | undefined {
        return this.stepsByKey.get(
            ProcessRegistry.makeKey(processorName, stepName));
    }

    getStep(
        processorName: string,
        stepName: string
    ): ProcessStepRegistryRecord {
        return this.getRegistration(processorName, stepName).step;
    }

    tryGetStep(
        processorName: string,
        stepName: string
    ): ProcessStepRegistryRecord | undefined {
        return this.tryGetRegistration(processorName, stepName)?.step;
    }

    // Returns any registered entry for a given processor — used to resolve
    // the serviceName for cross-processor handoffs where only the processor
    // name is known (not a specific step).
    getAnyEntryForProcessor(
        processorName: string
    ): ProcessStepRegistration | undefined {
        const lower = processorName.toLowerCase();

        for (const entry of this.stepsByKey.values()) {
            if (entry.processor.name.toLowerCase() === lower) {
                return entry;
            }
        }

        return undefined;
    }

    getSteps(): readonly ProcessStepRegistryRecord[] {
        return Array.from(this.stepsByKey.values()).map(e => e.step);
    }

    getEntryProcessorName(): string | undefined {
        return Array.from(this.stepsByKey.values())
            .find(s => s.processor.isEntryProcessor)?.processor.name;
    }

    ensureCurrentProcessorSet(): void {
        if (!this.processState.state().currentProcessorName) {
            const entryProcessorName = this.getEntryProcessorName();
            if (entryProcessorName) {
                this.processState.setCurrentProcessor(entryProcessorName);
            }
        }
    }

    getConflicts(): readonly RegistryConflict[] {
        return this.conflicts;
    }
}
