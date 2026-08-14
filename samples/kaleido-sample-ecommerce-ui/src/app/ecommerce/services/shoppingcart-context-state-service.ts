import { Injectable } from '@angular/core';
import { QueryExecutionStateService } from '../../kaleido/services/query-state-service';

@Injectable({
    providedIn: 'root'
})
export class ShoppingCartContextStateService
    extends QueryExecutionStateService {

    constructor() {

        super();
    }
}