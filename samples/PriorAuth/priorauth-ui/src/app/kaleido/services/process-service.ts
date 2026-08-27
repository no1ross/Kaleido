import { HttpClient } from '@angular/common/http';
import { Injectable, inject } from '@angular/core';
import { Router } from '@angular/router';
import { Observable, catchError, map, throwError } from 'rxjs';

import {
    ProcessExecutionResponse,
    ProcessMessage
} from '../models/participant-process-result';
import { ExecuteStepRequest } from '../models/participant-process-request';
import { RequestContextService } from './request-context-service';
import { ProcessRegistry } from './process-registry';
import {
    ProcessRequestValidationError,
    ProcessRequestValidator
} from './process-request-validator';
import { buildServiceUrl } from '../../../configuration/urlConfig';
import { ProcessStateService } from '../../process/services/process-state-service';
import { getRouteForStep } from '../../process/services/step-route';

@Injectable({
    providedIn: 'root'
})
export class ProcessService {
    private readonly http =
        inject(HttpClient);

    private readonly requestContext =
        inject(RequestContextService);

    private readonly processRegistry =
        inject(ProcessRegistry);

    private readonly processRequestValidator =
        inject(ProcessRequestValidator);

    private readonly processState =
        inject(ProcessStateService);

    private readonly router =
        inject(Router);

    executeStep<TProcessStep, TResponse>(
        stepName: string,
        request: ExecuteStepRequest<TProcessStep>
    ): Observable<ProcessExecutionResponse<TResponse>> {
        const entry =
            this.processRegistry.getServiceStep(stepName);

        const validationResult =
            this.processRequestValidator.validate(
                entry.step,
                request);

        if (!validationResult.isValid) {
            console.error(
                '[ProcessService] Request validation failed.',
                validationResult);

            return throwError(
                () => new ProcessRequestValidationError(
                    validationResult.messages));
        }

        const url =
            buildServiceUrl(
                entry.service,
                entry.step.executeUrl);

        const processRequest = {
            ...request,
            requestId: this.requestContext.currentRequestId
        };

        console.log(entry.step);
        this.logRequest(stepName, url, request, entry.service.displayName);

        return this.http.post<ProcessExecutionResponse<TResponse>>(
            url,
            processRequest)
            .pipe(
                map(result => {
                    this.processState.setProcessId(result.participantProcessId);
                    this.processState.setProcessMessages(result.messages);
                    this.processState.setProcessFlow(
                        result.requiredStep,
                        result.availableSteps);
                    this.navigateToRequiredStep(result.requiredStep);
                    this.logStepOutcome(result);

                    if (
                        result.outcome === 'Failed' ||
                        result.outcome === 'Blocked' ||
                        result.outcome === 'Cancelled'
                    ) {
                        throw {
                            outcome: result.outcome,
                            messages: result.messages
                        } satisfies ProcessErrorResponse;
                    }

                    return result;
                }),
                catchError(error => {
                    if (ProcessErrorResponse.is(error)) {
                        this.processState.setProcessMessages(error.messages);
                    } else {
                        console.error(
                            'Unexpected process error',
                            error);
                    }

                    return throwError(
                        () => error);
                }));
    }

    private logStepOutcome(
        result: ProcessExecutionResponse<any>
    ): void {
        const title =
            `[PROCESS] ${result.stepName} (${result.outcome})`;

        console.group(title);

        console.log(
            'Participant Process',
            result.participantProcessId);

        console.log(
            'Outcome',
            result.outcome);

        if (result.requiredStep) {
            console.log(
                'Required Step',
                result.requiredStep);
        }

        if (result.availableSteps.length > 0) {
            console.log(
                'Available Steps',
                result.availableSteps);
        }

        for (const message of result.messages) {
            this.logMessage(message);
        }

        console.groupEnd();
    }

    private navigateToRequiredStep(
        requiredStep: string | undefined
    ): void {
        if (!requiredStep) {
            return;
        }

        const route = getRouteForStep(requiredStep);

        if (!route) {
            return;
        }

        void this.router.navigate(['/process', route]);
    }

    private logRequest(
        operation: string,
        url: string,
        request: unknown,
        serviceName: string
    ): void {
        console.group(`[PROCESS] ${operation}`);
        console.log('Service', serviceName);
        console.log('Url', url);
        console.log('Request', request);
        console.groupEnd();
    }

    private logMessage(
        message: ProcessMessage
    ): void {
        switch (message.type) {
            case 'Information':
                console.info(`[${message.code}] ${message.message}`);
                return;

            case 'Warning':
                console.warn(`[${message.code}] ${message.message}`);
                return;

            case 'Error':
                console.error(`[${message.code}] ${message.message}`);
                return;

            default:
                console.log(`[${message.code}] ${message.message}`);
        }
    }
}

export class ProcessErrorResponse {
    outcome!: string;
    messages!: ProcessMessage[];

    static is(
        value: unknown
    ): value is ProcessErrorResponse {
        return typeof value === 'object'
            && value !== null
            && 'outcome' in value
            && 'messages' in value;
    }
}
