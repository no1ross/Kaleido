import { Injectable, inject } from '@angular/core';
import { Observable, tap, map, of, catchError, throwError } from 'rxjs';
import { ProcessStepRegistryRecord, ProcessStepFieldMetadata } from '../models/process-registry';
import { HttpClient } from '@angular/common/http';
import { getProcessRegistryUrl } from '../../../configuration/urlConfig';

@Injectable({
    providedIn: 'root'
})
export class ProcessRegistry {
    
    private readonly http =
        inject(HttpClient);

    private readonly stepsByName =
        new Map<string, ProcessStepRegistryRecord>();

    private readonly normalizedNamesByName =
        new Map<string, string>();

    private initialized = false;

    initialize(): Observable<void> {

        if (this.initialized) {

            console.log(
                '[ProcessRegistry] Already initialized.');

            return of(undefined);
        }

        console.log(
            '[ProcessRegistry] Loading process registry...');

        const started =
            performance.now();

        return this.http
            .get<ProcessStepRegistryRecord[]>(
                getProcessRegistryUrl())
            .pipe(
                tap(steps => {

                    this.populateRegistry(steps);

                    this.initialized = true;

                    const duration =
                        Math.round(
                            performance.now() - started);
                        
                    console.group('[ProcessRegistry]');

                    console.log(
                        `Loaded ${steps.length} steps in ${duration}ms.`);

                    console.table(
                        steps.map(step => ({
                            Name: step.name,
                            DisplayName: step.displayName,
                            Repeatable: step.repeatable,
                            Fields: step.fields.length,
                            Dependencies: step.dependencies.length
                        })));

                    console.groupEnd();
                }),
                map(() => undefined),
                catchError(error => {

                    console.error(
                        '[ProcessRegistry] Failed to load process registry.',
                        error);

                    return throwError(() => error);
                })
            );
    }

    get isInitialized(): boolean {
        return this.initialized;
    }

    populateRegistry(
        steps: ProcessStepRegistryRecord[]
    ): void {
        this.stepsByName.clear();
        this.normalizedNamesByName.clear();

        for (const step of steps) {
            this.stepsByName.set(
                step.name,
                step);

            this.normalizedNamesByName.set(
                step.name.toLowerCase(),
                step.name);
        }
    }

    getStep(
        stepName: string
    ): ProcessStepRegistryRecord {
        const step =
            this.tryGetStep(stepName);

        if (!step) {
            throw new Error(
                `Process step '${stepName}' is not registered.`);
        }

        return step;
    }

    getSteps(): readonly ProcessStepRegistryRecord[] {

        return Array.from(
            this.stepsByName.values());
    }

    tryGetStep(
        stepName: string
    ): ProcessStepRegistryRecord | undefined {
        const normalizedName =
            this.normalizedNamesByName.get(
                stepName.toLowerCase());

        if (!normalizedName) {
            return undefined;
        }

        return this.stepsByName.get(normalizedName);
    }

    hasStep(
        stepName: string
    ): boolean {
        return this.tryGetStep(stepName) !== undefined;
    }

    getExecuteUrl(
        stepName: string
    ): string {
        return this.getStep(stepName).executeUrl;
    }

    getFields(
        stepName: string
    ): ProcessStepFieldMetadata[] {
        return this.getStep(stepName).fields;
    }

    getRequiredFields(
        stepName: string
    ): ProcessStepFieldMetadata[] {
        return this
            .getStep(stepName)
            .fields
            .filter(field =>
                this.isRequiredField(field));
    }

    validateRequest(
        stepName: string,
        request: unknown
    ): ProcessStepValidationResult {
        const step =
            this.getStep(stepName);

        const errors: ProcessStepValidationError[] = [];

        if (!this.isObject(request)) {
            return {
                valid: false,
                errors: [
                    {
                        fieldName: null,
                        message:
                            `Request for process step '${step.name}' must be an object.`
                    }
                ]
            };
        }

        for (const field of step.fields) {
            if (!this.isRequiredField(field)) {
                continue;
            }

            const value =
                request[field.name];

            if (
                value === undefined ||
                value === null ||
                value === ''
            ) {
                errors.push({
                    fieldName: field.name,
                    message:
                        `Required field '${field.name}' was not supplied for process step '${step.name}'.`
                });
            }
        }

        return {
            valid: errors.length === 0,
            errors
        };
    }

    resolveStepSummaries(
        stepNames: string[]
    ): ProcessStepRegistryRecord[] {
        return stepNames
            .map(stepName =>
                this.tryGetStep(stepName))
            .filter(
                (step): step is ProcessStepRegistryRecord =>
                    step !== undefined);
    }

    private isRequiredField(
        field: ProcessStepFieldMetadata
    ): boolean {
        if (!field.dataType.nullable) {
            return true;
        }

        return field.constraints.some(
            constraint =>
                constraint.type === 'Required');
    }

    private isObject(
        value: unknown
    ): value is Record<string, unknown> {
        return (
            typeof value === 'object' &&
            value !== null &&
            !Array.isArray(value)
        );
    }
}

export interface ProcessStepValidationResult {
    valid: boolean;
    errors: ProcessStepValidationError[];
}

export interface ProcessStepValidationError {
    fieldName: string | null;
    message: string;
}