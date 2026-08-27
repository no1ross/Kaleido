import { Injectable } from '@angular/core';
import { Subject } from 'rxjs';

import { QueryRequest } from '../models/queryable-request';

// @Injectable({
//     providedIn: 'root'
// })
export abstract class QueryExecutionStateService<TParameters = unknown> {

    readonly state: QueryExecutionState<TParameters> = {
        request: {
            query: {}
        }
    };

    readonly changed =
        new Subject<QueryExecutionState<TParameters>>();

    notifyChanged(): void {

        console.log('QUERY STATE CHANGED');

        this.changed.next(this.state);
    }

    reset(): void {

        this.state.request = {
            query: {}
        };

        this.notifyChanged();
    }

    replace(
        state: QueryExecutionState<TParameters>): void {

        Object.assign(
            this.state,
            state);

        this.notifyChanged();
    }
}

export interface QueryExecutionState<TParameters = unknown> {

    request: QueryRequest<TParameters>;
}

@Injectable({
    providedIn: 'root'
})
export class QueryResultStateService {

    readonly state: QueryResultState = {
        totalCount: 0,
        pageSize: 25,
        offset: 0
    };

    readonly changed =
        new Subject<QueryResultState>();

    notifyChanged(): void {

        this.changed.next(this.state);
    }

    reset(): void {

        this.state.totalCount = 0;
        this.state.pageSize = 0;
        this.state.offset = 0;

        this.notifyChanged();
    }

    replace(
        state: QueryResultState): void {

        this.state.totalCount =
            state.totalCount;

        this.state.pageSize =
            state.pageSize;

        this.state.offset =
            state.offset;

        this.notifyChanged();
    }
}

export interface QueryResultState {
    totalCount: number;
    pageSize: number;
    offset: number;
}
