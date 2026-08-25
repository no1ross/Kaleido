import { Injectable } from '@angular/core';

import { QueryRequest } from '../models/queryable-request';
import {
    QueryableConstraint,
    QueryableParameter,
    QueryableView
} from '../models/queryable-registry';

export interface QueryableRequestValidationResult {
    isValid: boolean;
    messages: QueryableRequestValidationMessage[];
}

export interface QueryableRequestValidationMessage {
    parameter: string;
    message: string;
}

export class QueryableRequestValidationError extends Error {
    constructor(
        public readonly messages: QueryableRequestValidationMessage[]
    ) {
        super('Queryable request validation failed.');
    }
}

@Injectable({
    providedIn: 'root'
})
export class QueryableRequestValidator {
    validate<TParameters>(
        viewMetadata: QueryableView,
        request: QueryRequest<TParameters>
    ): QueryableRequestValidationResult {
        const messages: QueryableRequestValidationMessage[] = [];
        const parameters = this.getParameters(request);

        for (const parameter of viewMetadata.parameters) {
            const value = this.getParameterValue(parameters, parameter.name);
            const requiredMessage = this.validateRequired(parameter, value);

            if (requiredMessage) {
                messages.push(requiredMessage);
                continue;
            }

            if (this.isEmptyValue(value)) {
                continue;
            }

            const stringLengthMessage = this.validateStringLength(parameter, value);

            if (stringLengthMessage) {
                messages.push(stringLengthMessage);
            }

            const rangeMessage = this.validateRange(parameter, value);

            if (rangeMessage) {
                messages.push(rangeMessage);
            }
        }

        return {
            isValid: messages.length === 0,
            messages
        };
    }

    private getParameters<TParameters>(
        request: QueryRequest<TParameters>
    ): Record<string, unknown> {
        const requestWithParameters = request as { parameters?: Record<string, unknown> };

        return requestWithParameters.parameters ?? {};
    }

    private getParameterValue(
        parameters: Record<string, unknown>,
        parameterName: string
    ): unknown {
        if (parameterName in parameters) {
            return parameters[parameterName];
        }

        const camelCaseName = this.toCamelCase(parameterName);

        if (camelCaseName in parameters) {
            return parameters[camelCaseName];
        }

        return undefined;
    }

    private validateRequired(
        parameter: QueryableParameter,
        value: unknown
    ): QueryableRequestValidationMessage | null {
        const requiredConstraint = this.getConstraint(parameter, 'Required');

        if (!requiredConstraint || !this.isEmptyValue(value)) {
            return null;
        }

        return {
            parameter: parameter.name,
            message: `${parameter.name} is required.`
        };
    }

    private validateStringLength(
        parameter: QueryableParameter,
        value: unknown
    ): QueryableRequestValidationMessage | null {
        const stringLengthConstraint = this.getConstraint(parameter, 'StringLength');

        if (!stringLengthConstraint) {
            return null;
        }

        const stringValue = value?.toString() ?? '';
        const minimumLength = this.getConstraintNumberParameter(stringLengthConstraint, 'MinimumLength');
        const maximumLength = this.getConstraintNumberParameter(stringLengthConstraint, 'MaximumLength');

        if (minimumLength !== undefined && stringValue.length < minimumLength) {
            return {
                parameter: parameter.name,
                message: `${parameter.name} must be at least ${minimumLength} characters.`
            };
        }

        if (maximumLength !== undefined && stringValue.length > maximumLength) {
            return {
                parameter: parameter.name,
                message: `${parameter.name} must be no more than ${maximumLength} characters.`
            };
        }

        return null;
    }

    private validateRange(
        parameter: QueryableParameter,
        value: unknown
    ): QueryableRequestValidationMessage | null {
        const rangeConstraint = this.getConstraint(parameter, 'Range');

        if (!rangeConstraint) {
            return null;
        }

        const minimum = this.getConstraintNumberParameter(rangeConstraint, 'Minimum');
        const maximum = this.getConstraintNumberParameter(rangeConstraint, 'Maximum');
        const numericValue = this.toNumber(value);

        if (numericValue === undefined) {
            return {
                parameter: parameter.name,
                message: `${parameter.name} must be a number.`
            };
        }

        if (minimum !== undefined && numericValue < minimum) {
            return {
                parameter: parameter.name,
                message: `${parameter.name} must be greater than or equal to ${minimum}.`
            };
        }

        if (maximum !== undefined && numericValue > maximum) {
            return {
                parameter: parameter.name,
                message: `${parameter.name} must be less than or equal to ${maximum}.`
            };
        }

        return null;
    }

    private getConstraint(
        parameter: QueryableParameter,
        constraintType: string
    ): QueryableConstraint | undefined {
        return parameter.constraints.find(
            constraint => constraint.type === constraintType);
    }

    private getConstraintNumberParameter(
        constraint: QueryableConstraint,
        parameterName: string
    ): number | undefined {
        const parameter = constraint.parameters.find(x => x.name === parameterName);

        if (!parameter) {
            return undefined;
        }

        return this.toNumber(parameter.value);
    }

    private toNumber(
        value: unknown
    ): number | undefined {
        if (value === undefined || value === null || value === '') {
            return undefined;
        }

        if (typeof value === 'number') {
            return Number.isNaN(value)
                ? undefined
                : value;
        }

        const numericValue = Number(value);

        return Number.isNaN(numericValue)
            ? undefined
            : numericValue;
    }

    private isEmptyValue(
        value: unknown
    ): boolean {
        if (value === undefined || value === null) {
            return true;
        }

        if (typeof value === 'string' && value.trim() === '') {
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

        return value.charAt(0).toLowerCase() + value.slice(1);
    }
}
