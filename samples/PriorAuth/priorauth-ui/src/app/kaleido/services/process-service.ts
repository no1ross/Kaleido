import { HttpClient } from '@angular/common/http';
import { Injectable, inject } from '@angular/core';
import { Router } from '@angular/router';
import { Observable, catchError, map, switchMap, throwError } from 'rxjs';

import {
    ProcessExecutionResponse,
    ProcessMessage,
    ProcessStepSummary
} from '../models/processor-process-result';
import { ProcessStateResponse } from '../models/process-state-response';
import { CaptureRequestedServiceResponse } from '../models/questionnaire';
import { ExecuteStepRequest } from '../models/processor-process-request';

import { ProcessRegistry } from './process-registry';
import {
    ProcessRequestValidationError,
    ProcessRequestValidator
} from './process-request-validator';
import { buildServiceUrl } from '../../../configuration/urlConfig';
import { ProcessStateService } from '../../process/services/process-state-service';
import { buildProcessRoute } from '../../process/services/process-navigation';
import { getRouteForStep } from '../../process/services/step-route';

@Injectable({
    providedIn: 'root'
})
export class ProcessService {
    private readonly http =
        inject(HttpClient);

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
        // Ensure current processor is set to entry processor if not already set
        this.processRegistry.ensureCurrentProcessorSet();

        const processorName = this.processState.state().currentProcessorName;

        if (!processorName) {
            return throwError(
                () => new Error(
                    'No current processor in state. ' +
                    'Call setCurrentProcessor() before executing steps.'));
        }

        const entry =
            this.processRegistry.getServiceStep(processorName, stepName);

        const validationResult =
            this.processRequestValidator.validate(entry.step, request);

        if (!validationResult.isValid) {
            console.error(
                '[ProcessService] Request validation failed.',
                validationResult);

            return throwError(
                () => new ProcessRequestValidationError(
                    validationResult.messages));
        }

        const url = buildServiceUrl(entry.service, entry.step.executeUrl);

        console.log(entry.step);
        this.logRequest(stepName, url, request, entry.service.displayName);

        return this.http.post<ProcessExecutionResponse<TResponse>>(url, request)
            .pipe(
                switchMap(result => {
                    this.processState.setProcessId(result.processId);
                    this.processState.setProcessMessages(result.messages);
                    this.logStepOutcome(result);

                    if (result.targetProcessorName) {
                        // Cross-processor handoff — fetch the target processor's
                        // state to get the authoritative requiredStep, available
                        // steps, and questionnaire data before navigating.
                        // This also switches currentProcessorName in state so
                        // subsequent steps go to the right processor automatically.
                        return this.fetchTargetProcessorState(
                            result.targetProcessorName,
                            result.processId)
                            .pipe(
                                map(targetState => {
                                    this.processState.setProcessFlow(
                                        result.targetProcessorName!,
                                        targetState.requiredStep,
                                        targetState.availableSteps);
                                    this.captureQuestionnaireState(
                                        targetState.requiredStep,
                                        result.result);
                                    this.navigateToRequiredStep(
                                        targetState.requiredStep);
                                    return result;
                                }));
                    }

                    // Local step — use the response directly.
                    this.processState.setProcessFlow(
                        processorName,
                        result.requiredStep,
                        result.availableSteps);
                    this.captureQuestionnaireState(result.requiredStep, result.result);
                    this.navigateToRequiredStep(result.requiredStep);

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

                    return [result];
                }),
                catchError(error => {
                    if (ProcessErrorResponse.is(error)) {
                        this.processState.setProcessMessages(error.messages);
                    } else {
                        console.error('Unexpected process error', error);
                    }

                    return throwError(() => error);
                }));
    }

    private fetchTargetProcessorState(
        targetProcessorName: string,
        processId: string
    ): Observable<ProcessStateResponse> {
        const targetEntry =
            this.processRegistry.getAnyEntryForProcessor(targetProcessorName);

        if (!targetEntry) {
            throw new Error(
                `Cannot resolve service for target processor '${targetProcessorName}'. ` +
                `No steps from that processor are in the registry.`);
        }

        const stateUrl = buildServiceUrl(
            targetEntry.service,
            `/${targetEntry.service.key}/processes/${processId}`);

        console.log(
            `[ProcessService] Cross-processor handoff → fetching state from '${targetProcessorName}'`,
            stateUrl);

        return this.http.get<ProcessStateResponse>(stateUrl);
    }

    private captureQuestionnaireState(
        requiredStep: string | undefined,
        result: unknown
    ): void {
        const questionnaireResponse =
            result as CaptureRequestedServiceResponse | undefined;

        this.processState.setQuestionnaire(
            requiredStep,
            questionnaireResponse?.questionnaire);
    }

    private logStepOutcome(
        result: ProcessExecutionResponse<any>
    ): void {
        const title =
            `[PROCESS] ${result.stepName} (${result.outcome})`;

        console.group(title);
        console.log('Processor Process', result.processId);
        console.log('Outcome', result.outcome);

        if (result.targetProcessorName) {
            console.log('Target Processor (cross-processor handoff)', result.targetProcessorName);
        }

        if (result.requiredStep) {
            console.log('Required Step', result.requiredStep);
        }

        if (result.availableSteps.length > 0) {
            console.log('Available Steps', result.availableSteps);
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

        void this.router.navigate(
            buildProcessRoute(
                this.processState.state().processId,
                route));
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
