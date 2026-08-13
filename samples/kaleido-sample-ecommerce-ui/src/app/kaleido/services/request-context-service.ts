import { Injectable } from '@angular/core';

@Injectable({
    providedIn: 'root'
})
export class RequestContextService {

    private requestId?: string;

    beginAction(): string {

        this.requestId =
            crypto.randomUUID();

        return this.requestId;
    }

    get currentRequestId(): string {

        return this.requestId ??
            crypto.randomUUID();
    }
}