import { HttpClient, HttpErrorResponse } from '@angular/common/http';
import { Injectable, inject } from '@angular/core';
import { Observable } from 'rxjs';

import { ProcessExecutionResponse } from '../models/processor-process-result';
import { ExecuteStepRequest } from '../../ecommerce/models/processor-process-request';
import { RequestContextService } from './request-context-service';

import { ProcessMessage } from '../models/processor-process-result';
import { catchError, map, throwError } from 'rxjs';

import { ProcessRegistry } from './process-registry';

import { buildApiUrl } from '../../../configuration/urlConfig';
import { ProcessRequestValidator, ProcessRequestValidationError } from './process-request-validator';

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

    executeStep<TProcessStep, TResponse>(
        stepName: string,
        request: ExecuteStepRequest<TProcessStep>
    ): Observable<ProcessExecutionResponse<TResponse>> {

        const step =
            this.processRegistry.getStep(
                stepName);

        const validationResult =
            this.processRequestValidator.validate(
                step,
                request);

        if (!validationResult.isValid) {

            console.error(
                '[ProcessService] Request validation failed.',
                validationResult);

            return throwError(
                () => new ProcessRequestValidationError(
                    validationResult.messages));
        }

        console.log(step);

        const processRequest = {
            ...request,
            requestId:
                this.requestContext.currentRequestId
        };

        this.logRequest(stepName, request);

        return this.http.post<ProcessExecutionResponse<TResponse>>(
            buildApiUrl(step.executeUrl),
            processRequest)
                .pipe(
                    map(result => {

                        this.logStepOutcome(result);

                        if (
                            result.outcome === 'Failed' ||
                            result.outcome === 'Blocked' ||
                            result.outcome === 'Cancelled')
                        {
                            throw {
                                outcome: result.outcome,
                                messages: result.messages
                            } satisfies ProcessErrorResponse;
                        }

                        return result;
                    }),
                    catchError(error => {
                        if (!ProcessErrorResponse.is(error)) {
                            console.error(
                                'Unexpected process error',
                                error);                        
                        }

                        return throwError(
                            () => error);
                    }));
    }

    private logStepOutcome(
        result: ProcessExecutionResponse<any>): void {

        const title =
            `[PROCESS] ${result.stepName} (${result.outcome})`;

        switch (result.outcome) {
            case 'Completed':
                console.group(title);
                break;

            case 'Pending':
                console.group(title);
                break;

            case 'Blocked':
                console.group(title);
                break;

            case 'Cancelled':
                console.group(title);
                break;

            case 'Failed':
                console.group(title);
                break;

            default:
                console.group(title);
                break;
        }

        console.log(
            'Processor Process',
            result.processId);

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

            switch (message.type) {

                case 'Information':
                    console.info(
                        `[${message.code}] ${message.message}`);
                    break;

                case 'Warning':
                    console.warn(
                        `[${message.code}] ${message.message}`);
                    break;

                case 'Error':
                    console.error(
                        `[${message.code}] ${message.message}`);
                    break;

                default:
                    console.log(
                        `[${message.code}] ${message.message}`);
                    break;
            }
        }

        //console.log('Response', result);

        console.groupEnd();
    }

    private logRequest(
        operation: string,
        request: unknown): void {

        console.group(`[PROCESS] ${operation}`);

        console.log('Request', request);

        console.groupEnd();
    }
}

export class ProcessErrorResponse {

    outcome!: string;

    messages!: ProcessMessage[];

    static is(
        value: unknown)
        : value is ProcessErrorResponse {

        return typeof value === 'object'
            && value !== null
            && 'outcome' in value
            && 'messages' in value;
    }
}