import { Injectable } from '@angular/core';
import { Subject } from 'rxjs';

@Injectable({
    providedIn: 'root'
})
export class ECommerceStateService {

    readonly state: ECommerceState = { };

    readonly changed =
        new Subject<ECommerceState>();

    notifyChanged(): void {

        this.changed.next(this.state);
    }

    reset(): void {

        this.state.participantProcessId = undefined;
        this.state.customerId = undefined;

        this.notifyChanged();
    }

    replace(
        state: ECommerceState): void {

        Object.assign(
            this.state,
            state);

        this.notifyChanged();
    }
}


export interface ECommerceState {
    participantProcessId?: string;

    customerId?: string;
}

