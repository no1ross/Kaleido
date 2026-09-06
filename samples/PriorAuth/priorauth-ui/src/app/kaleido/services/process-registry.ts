import { Injectable, inject } from '@angular/core';

import {
    ProcessStepRegistryRecord,
    ServiceProcessStepRegistryRecord
} from '../models/process-registry';
import { RegistryConflict } from '../../registries/registry-catalog';
import { PriorAuthServiceRouteConfig } from '../../../configuration/urlConfig';
import { ProcessStateService } from '../../process/services/process-state-service';

@Injectable({
    providedIn: 'root'
})
export class ProcessRegistry {
    // Internally keyed by "processorName:stepName" to prevent collisions when
    // the same step name exists on multiple processors. Public lookup is by
    // processorName + stepName — the processor is tracked in ProcessState, not
    // hardcoded at call sites.
    private readonly stepsByKey =
        new Map<string, ServiceProcessStepRegistryRecord>();

    private conflicts: readonly RegistryConflict[] = [];

    private readonly processState =
        inject(ProcessStateService);

    private static makeKey(processorName: string, stepName: string): string {
        return `${processorName.toLowerCase()}:${stepName.toLowerCase()}`;
    }

    populateRegistry(
        steps: readonly ServiceProcessStepRegistryRecord[],
        conflicts: readonly RegistryConflict[]
    ): void {
        this.stepsByKey.clear();
        this.conflicts = conflicts;

        for (const step of steps) {
            this.stepsByKey.set(
                ProcessRegistry.makeKey(step.processor.name, step.step.name),
                step);
        }

        // Set the initial processor from the service marked isEntryProcessor.
        // This is explicit config, not inferred from initialSteps, to avoid
        // ambiguity when multiple processors advertise initial steps.
        const entryEntry = steps.find(s => s.service.isEntryProcessor);

        if (entryEntry) {
            this.processState.setCurrentProcessor(entryEntry.processor.name);
        }
    }

    getServiceStep(
        processorName: string,
        stepName: string
    ): ServiceProcessStepRegistryRecord {
        const entry = this.tryGetServiceStep(processorName, stepName);

        if (!entry) {
            throw new Error(
                `Process step '${stepName}' on processor '${processorName}' is not registered.`);
        }

        return entry;
    }

    getStep(
        processorName: string,
        stepName: string
    ): ProcessStepRegistryRecord {
        return this.getServiceStep(processorName, stepName).step;
    }

    tryGetServiceStep(
        processorName: string,
        stepName: string
    ): ServiceProcessStepRegistryRecord | undefined {
        return this.stepsByKey.get(
            ProcessRegistry.makeKey(processorName, stepName));
    }

    tryGetStep(
        processorName: string,
        stepName: string
    ): ProcessStepRegistryRecord | undefined {
        return this.tryGetServiceStep(processorName, stepName)?.step;
    }

    // Returns any registered entry for a given processor — used to resolve
    // the service config for cross-processor handoffs where only the
    // processor name is known (not a specific step).
    getAnyEntryForProcessor(
        processorName: string
    ): ServiceProcessStepRegistryRecord | undefined {
        const lower = processorName.toLowerCase();

        for (const entry of this.stepsByKey.values()) {
            if (entry.processor.name.toLowerCase() === lower) {
                return entry;
            }
        }

        return undefined;
    }

    getSteps(): readonly ProcessStepRegistryRecord[] {
        return Array.from(this.stepsByKey.values())
            .map(entry => entry.step);
    }

    getServiceForStep(
        processorName: string,
        stepName: string
    ): PriorAuthServiceRouteConfig {
        return this.getServiceStep(processorName, stepName).service;
    }

    getConflicts(): readonly RegistryConflict[] {
        return this.conflicts;
    }
}
