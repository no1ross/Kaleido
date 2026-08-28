import { Component, inject } from '@angular/core';

import { ProcessMessage } from '../kaleido/models/participant-process-result';
import { ProcessStateService } from './services/process-state-service';

@Component({
    selector: 'priorauth-process-messages',
    standalone: true,
    templateUrl: './process-messages.html',
    styleUrl: './process-messages.scss'
})
export class ProcessMessages {
    readonly processState =
        inject(ProcessStateService);

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
