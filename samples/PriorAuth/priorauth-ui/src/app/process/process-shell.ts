import { Component, inject } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { RouterLink, RouterOutlet } from '@angular/router';

import { ProcessMessages } from './process-messages';
import { ProcessStateService } from './services/process-state-service';

@Component({
    selector: 'priorauth-process-shell',
    standalone: true,
    imports: [FormsModule, RouterOutlet, RouterLink, ProcessMessages],
    templateUrl: './process-shell.html',
    styleUrl: './process-shell.scss'
})
export class ProcessShell {
    readonly processState =
        inject(ProcessStateService);

    updateDateOfService(
        value: string
    ): void {
        this.processState.setDateOfService(value);
    }
}
