import { HttpClient } from '@angular/common/http';
import { Injectable, inject } from '@angular/core';
import { Observable } from 'rxjs';

import { ProcessSummary } from '../models/event-summary';
import { EventDetail } from '../models/event-detail';

@Injectable({
    providedIn: 'root'
})
export class EventCollectorService {
    private readonly http =
        inject(HttpClient);

    private readonly baseUrl =
        'http://localhost:8086';

    getProcesses(): Observable<ProcessSummary[]> {
        return this.http.get<ProcessSummary[]>(`${this.baseUrl}/processes`);
    }

    getProcessEvents(processId: string): Observable<EventDetail[]> {
        return this.http.get<EventDetail[]>(`${this.baseUrl}/process-events/${processId}`);
    }
}
