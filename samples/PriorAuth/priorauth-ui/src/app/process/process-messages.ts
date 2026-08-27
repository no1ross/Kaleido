import { ChangeDetectorRef, Component, inject } from '@angular/core';

import { ProcessMessage } from '../kaleido/models/participant-process-result';
import { ProcessStateService } from './services/process-state-service';

@Component({
    selector: 'priorauth-process-messages',
    standalone: true,
    templateUrl: './process-messages.html',
    styleUrl: './process-messages.scss'
})
export class ProcessMessages {
    private readonly changeDetector =
        inject(ChangeDetectorRef);

    readonly processState =
        inject(ProcessStateService);

    constructor() {
        queueMicrotask(() =>
            this.changeDetector.detectChanges());
    }

    trackMessage(
        index: number,
        message: ProcessMessage
    ): string {
        return `${message.code}:${index}`;
    }

    getMessageClass(
        type: string
    ): string {
        switch (type) {
            case 'Error':
                return 'process-messages__item--error';
            case 'Warning':
                return 'process-messages__item--warning';
            case 'Information':
                return 'process-messages__item--information';
            default:
                return 'process-messages__item--default';
        }
    }
}
