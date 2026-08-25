import { Component, inject } from '@angular/core';
import { RouterLink, RouterOutlet } from '@angular/router';

import { WorkflowStateService } from './services/workflow-state-service';

@Component({
    selector: 'priorauth-workflow-shell',
    standalone: true,
    imports: [RouterOutlet, RouterLink],
    templateUrl: './workflow-shell.html',
    styleUrl: './workflow-shell.scss'
})
export class WorkflowShell {
    readonly workflowState =
        inject(WorkflowStateService);
}
