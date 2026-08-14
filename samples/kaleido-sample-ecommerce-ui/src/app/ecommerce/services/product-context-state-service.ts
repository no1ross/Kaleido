import { Injectable } from '@angular/core';
import { Subject } from 'rxjs';
import { QueryExecutionStateService } from '../../kaleido/services/query-state-service';

@Injectable({
    providedIn: 'root'
})
export class ProductContextStateService
    extends QueryExecutionStateService {

    constructor() {

        super();
    }
}
