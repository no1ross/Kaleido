import { Injectable } from '@angular/core';

import {
    ProcessStepRegistryRecord,
    ServiceProcessStepRegistryRecord
} from '../models/process-registry';
import { RegistryConflict } from '../../registries/registry-catalog';
import { PriorAuthServiceRouteConfig } from '../../../configuration/urlConfig';

@Injectable({
    providedIn: 'root'
})
export class ProcessRegistry {
    private readonly stepsByName =
        new Map<string, ServiceProcessStepRegistryRecord>();

    private conflicts: readonly RegistryConflict[] = [];

    populateRegistry(
        steps: readonly ServiceProcessStepRegistryRecord[],
        conflicts: readonly RegistryConflict[]
    ): void {
        this.stepsByName.clear();
        this.conflicts = conflicts;

        for (const step of steps) {
            this.stepsByName.set(
                step.step.name,
                step);
        }
    }

    getStep(
        stepName: string
    ): ProcessStepRegistryRecord {
        const entry =
            this.getServiceStep(stepName);

        return entry.step;
    }

    getServiceStep(
        stepName: string
    ): ServiceProcessStepRegistryRecord {
        const entry =
            this.tryGetServiceStep(stepName);

        if (!entry) {
            throw new Error(
                `Process step '${stepName}' is not registered.`);
        }

        return entry;
    }

    tryGetStep(
        stepName: string
    ): ProcessStepRegistryRecord | undefined {
        return this.tryGetServiceStep(stepName)?.step;
    }

    tryGetServiceStep(
        stepName: string
    ): ServiceProcessStepRegistryRecord | undefined {
        return this.stepsByName.get(stepName);
    }

    getSteps(): readonly ProcessStepRegistryRecord[] {
        return Array.from(this.stepsByName.values())
            .map(entry => entry.step);
    }

    getServiceForStep(
        stepName: string
    ): PriorAuthServiceRouteConfig {
        return this.getServiceStep(stepName).service;
    }

    getConflicts(): readonly RegistryConflict[] {
        return this.conflicts;
    }
}
