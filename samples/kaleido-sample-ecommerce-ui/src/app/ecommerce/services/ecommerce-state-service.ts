import { Injectable } from '@angular/core';
import { Subject } from 'rxjs';

@Injectable({
    providedIn: 'root'
})
export class ECommerceStateService {

    participantProcessId?: string;

    readonly cartChanged =
        new Subject<void>();

    notifyCartChanged(): void {

        console.log('cart changed event fired');

        this.cartChanged.next();
    }
}
