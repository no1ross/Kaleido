import { HttpErrorResponse, provideHttpClient } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { TestBed } from '@angular/core/testing';
import { Router } from '@angular/router';
import { of, throwError } from 'rxjs';

import { ProcessStateService } from '../../process/services/process-state-service';
import { RegistryCatalog } from '../../registries/registry-catalog';
import { IDEMPOTENCY_OUTCOME_UNKNOWN, IdempotentExecuteService } from './idempotent-execute-service';
import { ProcessRegistry } from './process-registry';
import { ProcessRequestValidator } from './process-request-validator';
import { ProcessService } from './process-service';

const step = { name: 'CaptureMemberStep', executeUrl: '/intake/processes/steps/capturememberstep' };

describe('ProcessService', () => {
    let http: HttpTestingController;
    const idempotentExecute = { post: vi.fn() };
    const processState = {
        state: vi.fn(() => ({ currentProcessorName: 'intake', processId: 'p1' })),
        setProcessId: vi.fn(),
        setProcessMessages: vi.fn(),
        setProcessFlow: vi.fn(),
        setInformationRequest: vi.fn()
    };

    beforeEach(() => {
        vi.clearAllMocks();
        TestBed.configureTestingModule({
            providers: [
                provideHttpClient(),
                provideHttpClientTesting(),
                { provide: IdempotentExecuteService, useValue: idempotentExecute },
                { provide: ProcessStateService, useValue: processState },
                {
                    provide: ProcessRegistry,
                    useValue: {
                        ensureCurrentProcessorSet: vi.fn(),
                        getRegistration: vi.fn(() => ({ serviceName: 'intake', step })),
                        getAnyEntryForProcessor: vi.fn(() => ({ serviceName: 'intake', step }))
                    }
                },
                { provide: ProcessRequestValidator, useValue: { validate: vi.fn(() => ({ isValid: true, messages: [] })) } },
                { provide: Router, useValue: { navigate: vi.fn(() => Promise.resolve(true)) } },
                { provide: RegistryCatalog, useValue: { loadState: vi.fn(() => of(undefined)) } }
            ]
        });
        http = TestBed.inject(HttpTestingController);
    });

    afterEach(() => http.verify());

    it('sends execute calls through the idempotent sender, with the current process id', () => {
        idempotentExecute.post.mockReturnValue(of({ processId: 'p1', outcome: 'completed', businessMessages: [], frameworkMessages: [], availableSteps: [] }));

        TestBed.inject(ProcessService).executeStep('CaptureMemberStep', { processStep: {} } as never).subscribe();

        expect(idempotentExecute.post).toHaveBeenCalledWith(expect.stringContaining(step.executeUrl), { processStep: {} }, 'p1');
    });

    it('reloads the process state and says so when the outcome of an earlier attempt is unknown', () => {
        idempotentExecute.post.mockReturnValue(throwError(() => new HttpErrorResponse({
            status: 409,
            error: { errors: [{ code: IDEMPOTENCY_OUTCOME_UNKNOWN, message: 'Unknown outcome.' }] }
        })));
        let failure: unknown;

        TestBed.inject(ProcessService).executeStep('CaptureMemberStep', { processStep: {} } as never).subscribe({ error: error => (failure = error) });
        http.expectOne(request => request.url.endsWith('/intake/processes/p1'))
            .flush({ processId: 'p1', requiredStep: { name: 'CaptureRequestedServiceStep' }, availableSteps: [] });

        expect(processState.setProcessFlow).toHaveBeenCalledWith('intake', 'CaptureRequestedServiceStep', []);
        expect(processState.setProcessMessages).toHaveBeenCalledWith([expect.objectContaining({ code: IDEMPOTENCY_OUTCOME_UNKNOWN, type: 'warning' })]);
        expect(failure).toEqual(expect.objectContaining({ outcome: 'unknown' }));
    });
});
