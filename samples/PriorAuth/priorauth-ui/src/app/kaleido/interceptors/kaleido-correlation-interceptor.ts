import { HttpInterceptorFn } from '@angular/common/http';
import { inject } from '@angular/core';

import { RequestContextService } from '../services/request-context-service';
import { ProcessStateService } from '../../process/services/process-state-service';

export const KALEIDO_REQUEST_ID_HEADER = 'X-Kaleido-Request-Id';
export const KALEIDO_PROCESS_ID_HEADER = 'X-Kaleido-Process-Id';

export const kaleidoCorrelationInterceptor: HttpInterceptorFn = (req, next) => {
    const requestContext = inject(RequestContextService);
    const processState = inject(ProcessStateService);

    let headers = req.headers.set(
        KALEIDO_REQUEST_ID_HEADER,
        requestContext.currentRequestId);

    const processId = processState.state().processId;
    if (processId) {
        headers = headers.set(
            KALEIDO_PROCESS_ID_HEADER,
            processId);
    }

    return next(req.clone({ headers }));
};
