import { Component, inject } from '@angular/core';

import { ProcessMessage } from '../kaleido/models/processor-process-result';
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
        // Kaleido serializes MessageType as camelCase ("error"); compare case-insensitively.
        switch (type?.toLowerCase()) {
            case 'error':
                return 'process-messages__item--error';
            case 'warning':
                return 'process-messages__item--warning';
            case 'information':
                return 'process-messages__item--information';
            default:
                return 'process-messages__item--default';
        }
    }
}
