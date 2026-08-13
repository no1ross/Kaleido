import { HttpClient, HttpErrorResponse } from '@angular/common/http';
import { Injectable, inject } from '@angular/core';
import { Observable } from 'rxjs';

import { ProcessExecutionResponse, ParticipantProcessResult } from '../models/participant-process-result';
import { ExecuteStepRequest } from '../models/participant-process-request';
import { RequestContextService } from './request-context-service';

@Injectable({
    providedIn: 'root'
})
export class ProcessService {

    private readonly http =
        inject(HttpClient);

    private readonly requestContext =
        inject(RequestContextService);

    executeStep<TProcessStep, TResponse>(
        stepName: string,
        request: ExecuteStepRequest<TProcessStep>
    ): Observable<ProcessExecutionResponse<TResponse>> {

        const processRequest = {
            ...request,
            requestId:
                this.requestContext.currentRequestId
        };

        return this.http.post<ProcessExecutionResponse<TResponse>>(
            `https://localhost:7251/kaleido/processes/steps/${stepName}`,
            processRequest);
    }

    getProcess(
        participantProcessId: string
    ): Observable<ParticipantProcessResult> {

        return this.http.get<ParticipantProcessResult>(
            `https://localhost:7251/kaleido/processes/${participantProcessId}`);
    }
}