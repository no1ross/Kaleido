import { HttpClient, HttpErrorResponse } from '@angular/common/http';
import { Injectable, inject } from '@angular/core';
import { Router } from '@angular/router';
import { Observable, catchError, map, of, switchMap, take, throwError } from 'rxjs';

import {
    ProcessExecutionResponse,
    ProcessMessage,
    ProcessRequiredStep
} from '../models/processor-process-result';
import { ProcessStateResponse } from '../models/process-state-response';
import { ExecuteStepRequest } from '../models/processor-process-request';

import { ProcessRegistry } from './process-registry';
import {
    errorCode,
    IDEMPOTENCY_OUTCOME_UNKNOWN,
    IdempotentExecuteService
} from './idempotent-execute-service';
import {
    ProcessRequestValidationError,
    ProcessRequestValidator
} from './process-request-validator';
import { buildServiceUrl, getServiceRoutes, PriorAuthServiceRouteConfig } from '../../../configuration/urlConfig';
import { ProcessStateService } from '../../process/services/process-state-service';
import { buildProcessRoute } from '../../process/services/process-navigation';
import { getResumeRoute, getRouteForStep } from '../../process/services/step-route';
import { RegistryCatalog } from '../../registries/registry-catalog';

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

    private readonly registryCatalog =
        inject(RegistryCatalog);

    private readonly idempotentExecute =
        inject(IdempotentExecuteService);

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
            this.processRegistry.getRegistration(processorName, stepName);

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

        const url = buildServiceUrl(this.resolveService(entry.serviceName), entry.step.executeUrl);

        console.log(entry.step);
        this.logRequest(stepName, url, request, entry.serviceName);

        const processId = this.processState.state().processId;

        return this.idempotentExecute.post<ProcessExecutionResponse<TResponse>>(url, request, processId)
            .pipe(
                switchMap(result => {
                    this.processState.setProcessId(result.processId);
                    this.processState.setProcessMessages(result.businessMessages);
                    this.logStepOutcome(result);

                    if (result.targetProcessorName) {
                        // Cross-processor handoff — fetch the target processor's
                        // state to get the authoritative requiredStep, available
                        // steps, and any pending information request before navigating.
                        // This also switches currentProcessorName in state so
                        // subsequent steps go to the right processor automatically.
                        return this.fetchProcessorState(
                            result.targetProcessorName,
                            result.processId)
                            .pipe(
                                map(targetState => {
                                    this.applyNextSteps(
                                        result.targetProcessorName!,
                                        targetState.requiredStep,
                                        targetState.availableSteps);
                                    return result;
                                }));
                    }

                    // Local step — use the response directly.
                    this.applyNextSteps(
                        processorName,
                        result.requiredStep,
                        result.availableSteps);

                    // Anything but "completed" means the step did not finish:
                    // "pending" = rejected before it ran (e.g. not a next step,
                    // answers that don't fit); the reason is in frameworkMessages.
                    if (result.outcome !== 'completed') {
                        throw {
                            outcome: result.outcome,
                            messages: [
                                ...result.businessMessages,
                                ...result.frameworkMessages
                            ]
                        } satisfies ProcessErrorResponse;
                    }

                    return [result];
                }),
                catchError(error => {
                    if (errorCode(error) === IDEMPOTENCY_OUTCOME_UNKNOWN) {
                        return this.reloadAfterUnknownOutcome(processorName, processId);
                    }

                    if (ProcessErrorResponse.is(error)) {
                        this.processState.setProcessMessages(error.messages);
                    } else if (error instanceof HttpErrorResponse && error.error?.errors) {
                        // KaleidoFrameworkException (500) and other structured error
                        // responses share the same { errors: [{ code, message }] } shape.
                        // Surface them as a QueryErrorResponse so component error handlers
                        // can display the server's message rather than a generic fallback.
                        console.error(
                            error.status >= 500
                                ? 'Process server error'
                                : 'Process request error',
                            error.error);
                        return throwError(() => error.error);
                    } else {
                        console.error('Unexpected process error', error);
                    }

                    return throwError(() => error);
                }));
    }

    /**
     * Continues a process started elsewhere (another session, or an agent through the MCP adapter):
     * reads its state from the processor that owns it, follows a handoff to the target processor,
     * restores the current processor, required step (with any pending questions) and available
     * steps, then navigates to the required step or, when nothing is required, to the furthest
     * available step this UI has a screen for.
     */
    resumeProcess(
        processorName: string,
        processId: string
    ): Observable<ProcessStateResponse> {
        return this.registryCatalog.loadState()
            .pipe(
                take(1),
                switchMap(() => this.fetchProcessorState(processorName, processId)),
                switchMap(state => {
                    const target = state.targetProcessorName;

                    return target
                        ? this.fetchProcessorState(target, processId)
                            .pipe(map(targetState => ({ processorName: target, state: targetState })))
                        : of({ processorName, state });
                }),
                map(({ processorName: owner, state }) => {
                    this.processState.setProcessId(state.processId);
                    this.applyNextSteps(owner, state.requiredStep, state.availableSteps);

                    if (!getRouteForStep(state.requiredStep?.name)) {
                        this.navigateToRoute(getResumeRoute(state.availableSteps));
                    }

                    return state;
                }));
    }

    /**
     * The service could not tell whether an earlier attempt of this call ran (it stopped while the
     * step ran): reload the process state, so the user sees what is true now, and say so.
     */
    private reloadAfterUnknownOutcome(
        processorName: string,
        processId: string | undefined
    ): Observable<never> {
        const failure: ProcessErrorResponse = {
            outcome: 'unknown',
            messages: [{
                code: IDEMPOTENCY_OUTCOME_UNKNOWN,
                type: 'warning',
                message: 'We could not confirm whether this step was saved. '
                    + 'The latest state of the request is shown: check it before trying again.'
            }]
        };

        const reload = processId
            ? this.fetchProcessorState(processorName, processId).pipe(
                map(state => this.applyNextSteps(processorName, state.requiredStep, state.availableSteps)))
            : of(undefined);

        return reload.pipe(
            switchMap(() => {
                this.processState.setProcessMessages(failure.messages);
                return throwError(() => failure);
            }));
    }

    private fetchProcessorState(
        processorName: string,
        processId: string
    ): Observable<ProcessStateResponse> {
        const entry =
            this.processRegistry.getAnyEntryForProcessor(processorName);

        if (!entry) {
            return throwError(() => new Error(
                `Cannot resolve service for processor '${processorName}'. ` +
                `No steps from that processor are in the registry.`));
        }

        const stateUrl = buildServiceUrl(
            this.resolveService(entry.serviceName),
            `/${entry.serviceName}/processes/${processId}`);

        console.log(
            `[ProcessService] Fetching process state from '${processorName}'`,
            stateUrl);

        return this.http.get<ProcessStateResponse>(stateUrl);
    }

    private resolveService(serviceName: string): PriorAuthServiceRouteConfig {
        return getServiceRoutes().find(s => s.key === serviceName)
            ?? { key: serviceName, baseUrl: '' };
    }

    // The required step is an object: its name drives navigation, and an information step
    // carries the questions to present (requiredStep.informationRequest).
    private applyNextSteps(
        processorName: string,
        requiredStep: ProcessRequiredStep | undefined,
        availableSteps: ProcessExecutionResponse<unknown>['availableSteps']
    ): void {
        this.processState.setProcessFlow(
            processorName,
            requiredStep?.name,
            availableSteps);
        this.processState.setInformationRequest(
            requiredStep?.name,
            requiredStep?.informationRequest);
        this.navigateToRequiredStep(requiredStep?.name);
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
            console.log('Required Step', result.requiredStep.name, result.requiredStep.informationRequest);
        }

        if (result.availableSteps.length > 0) {
            console.log('Available Steps', result.availableSteps);
        }

        for (const message of result.businessMessages) {
            this.logMessage(message);
        }

        for (const message of result.frameworkMessages) {
            console.debug(`[FRAMEWORK:${message.code}] ${message.message}`);
        }

        console.groupEnd();
    }

    private navigateToRequiredStep(
        requiredStep: string | undefined
    ): void {
        this.navigateToRoute(getRouteForStep(requiredStep));
    }

    private navigateToRoute(
        route: string | undefined
    ): void {
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
        switch (message.type?.toLowerCase()) {
            case 'information':
                console.info(`[${message.code}] ${message.message}`);
                return;

            case 'warning':
                console.warn(`[${message.code}] ${message.message}`);
                return;

            case 'error':
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
