import {
    Injectable
} from '@angular/core';

import { ExecuteStepRequest } from '../../ecommerce/models/processor-process-request';
import { ProcessStepFieldMetadata, ProcessFieldConstraintMetadata, ProcessStepRegistryRecord } from '../models/process-registry';

export interface ProcessRequestValidationResult {

    isValid: boolean;

    messages: ProcessRequestValidationMessage[];
}

export interface ProcessRequestValidationMessage {

    field: string;

    message: string;
}

export class ProcessRequestValidationError
    extends Error {

    constructor(
        public readonly messages:
            ProcessRequestValidationMessage[]
    ) {

        super(
            messages
                .map(
                    message =>
                        message.message)
                .join('; '));

        this.name =
            'ProcessRequestValidationError';
    }
}

@Injectable({
    providedIn: 'root'
})
export class ProcessRequestValidator {

    validate<TStep>(
        step: ProcessStepRegistryRecord,
        request: ExecuteStepRequest<TStep>
    ): ProcessRequestValidationResult {

        const messages:
            ProcessRequestValidationMessage[] = [];

        const values =
            request.processStep as
                Record<string, unknown>;

        for (const field of step.fields) {

            const value =
                this.getFieldValue(
                    values,
                    field.name);

            const requiredMessage =
                this.validateRequired(
                    field,
                    value);

            if (requiredMessage) {

                messages.push(
                    requiredMessage);

                continue;
            }

            if (this.isEmptyValue(
                value)) {

                continue;
            }

            const stringLengthMessage =
                this.validateStringLength(
                    field,
                    value);

            if (stringLengthMessage) {

                messages.push(
                    stringLengthMessage);
            }

            const rangeMessage =
                this.validateRange(
                    field,
                    value);

            if (rangeMessage) {

                messages.push(
                    rangeMessage);
            }
        }

        return {
            isValid:
                messages.length === 0,
            messages
        };
    }

    private getFieldValue(
        values: Record<string, unknown>,
        fieldName: string
    ): unknown {

        if (fieldName in values) {

            return values[fieldName];
        }

        const camelCaseName =
            this.toCamelCase(
                fieldName);

        if (camelCaseName in values) {

            return values[camelCaseName];
        }

        return undefined;
    }

    private validateRequired(
        field: ProcessStepFieldMetadata,
        value: unknown
    ): ProcessRequestValidationMessage | null {

        const constraint =
            this.getConstraint(
                field,
                'Required');

        if (!constraint) {

            return null;
        }

        if (!this.isEmptyValue(
            value)) {

            return null;
        }

        return {
            field:
                field.name,
            message:
                `${field.name} is required.`
        };
    }

    private validateStringLength(
        field: ProcessStepFieldMetadata,
        value: unknown
    ): ProcessRequestValidationMessage | null {

        const constraint =
            this.getConstraint(
                field,
                'StringLength');

        if (!constraint) {

            return null;
        }

        const stringValue =
            value?.toString() ?? '';

        const minimumLength =
            this.getConstraintNumberParameter(
                constraint,
                'MinimumLength');

        const maximumLength =
            this.getConstraintNumberParameter(
                constraint,
                'MaximumLength');

        if (
            minimumLength !== undefined &&
            stringValue.length < minimumLength
        ) {

            return {
                field:
                    field.name,
                message:
                    `${field.name} must be at least ${minimumLength} characters.`
            };
        }

        if (
            maximumLength !== undefined &&
            stringValue.length > maximumLength
        ) {

            return {
                field:
                    field.name,
                message:
                    `${field.name} must be no more than ${maximumLength} characters.`
            };
        }

        return null;
    }

    private validateRange(
        field: ProcessStepFieldMetadata,
        value: unknown
    ): ProcessRequestValidationMessage | null {

        const constraint =
            this.getConstraint(
                field,
                'Range');

        if (!constraint) {

            return null;
        }

        const minimum =
            this.getConstraintNumberParameter(
                constraint,
                'Minimum');

        const maximum =
            this.getConstraintNumberParameter(
                constraint,
                'Maximum');

        const numericValue =
            this.toNumber(
                value);

        if (numericValue === undefined) {

            return {
                field:
                    field.name,
                message:
                    `${field.name} must be a number.`
            };
        }

        if (
            minimum !== undefined &&
            numericValue < minimum
        ) {

            return {
                field:
                    field.name,
                message:
                    `${field.name} must be greater than or equal to ${minimum}.`
            };
        }

        if (
            maximum !== undefined &&
            numericValue > maximum
        ) {

            return {
                field:
                    field.name,
                message:
                    `${field.name} must be less than or equal to ${maximum}.`
            };
        }

        return null;
    }

    private getConstraint(
        field: ProcessStepFieldMetadata,
        constraintType: string
    ): ProcessFieldConstraintMetadata | undefined {

        return field.constraints.find(
            constraint =>
                constraint.type === constraintType);
    }

    private getConstraintNumberParameter(
        constraint: ProcessFieldConstraintMetadata,
        parameterName: string
    ): number | undefined {

        const parameter =
            constraint.parameters.find(
                x =>
                    x.name === parameterName);

        if (!parameter) {

            return undefined;
        }

        return this.toNumber(
            parameter.value);
    }

    private toNumber(
        value: unknown
    ): number | undefined {

        if (
            value === undefined ||
            value === null ||
            value === ''
        ) {

            return undefined;
        }

        if (typeof value === 'number') {

            return Number.isNaN(
                value)
                ? undefined
                : value;
        }

        const numericValue =
            Number(value);

        return Number.isNaN(
            numericValue)
            ? undefined
            : numericValue;
    }

    private isEmptyValue(
        value: unknown
    ): boolean {

        if (
            value === undefined ||
            value === null
        ) {

            return true;
        }

        if (
            typeof value === 'string' &&
            value.trim() === ''
        ) {

            return true;
        }

        return false;
    }

    private toCamelCase(
        value: string
    ): string {

        if (value.length === 0) {

            return value;
        }

        return value.charAt(0).toLowerCase() +
            value.slice(1);
    }
}