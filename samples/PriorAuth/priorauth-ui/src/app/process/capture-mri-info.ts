import { Component, inject, signal } from '@angular/core';
import { FormsModule } from '@angular/forms';

import { ProcessService } from '../kaleido/services/process-service';
import { ProcessStateService } from './services/process-state-service';

type MriBodyPart = 'Spine' | 'Knee';
type Laterality = 'None' | 'Left' | 'Right' | 'Bilateral';
type ContrastOption = 'WithoutContrast' | 'WithContrast' | 'WithAndWithoutContrast';

interface CaptureMriInfoStep {
    bodyPart: MriBodyPart;
    laterality: Laterality;
    contrast: ContrastOption;
}

@Component({
    selector: 'priorauth-capture-mri-info',
    standalone: true,
    imports: [FormsModule],
    templateUrl: './capture-mri-info.html',
    styleUrl: './capture-mri-info.scss'
})
export class CaptureMriInfo {
    private readonly processService =
        inject(ProcessService);

    private readonly processState =
        inject(ProcessStateService);

    bodyPart: MriBodyPart = 'Spine';
    laterality: Laterality = 'None';
    contrast: ContrastOption = 'WithoutContrast';
    readonly isSubmitting =
        signal(false);
    readonly errorMessage =
        signal<string | undefined>(undefined);

    readonly bodyPartOptions: MriBodyPart[] = ['Spine', 'Knee'];
    readonly lateralityOptions: Laterality[] = ['None', 'Left', 'Right', 'Bilateral'];
    readonly contrastOptions: ContrastOption[] = ['WithoutContrast', 'WithContrast', 'WithAndWithoutContrast'];

    submit(): void {
        if (!this.processState.state().processId || this.isSubmitting()) {
            return;
        }

        this.isSubmitting.set(true);
        this.errorMessage.set(undefined);

        this.processService
            .executeStep<CaptureMriInfoStep, object>('CaptureMriInfo', {
                processId: this.processState.state().processId,
                processStep: {
                    bodyPart: this.bodyPart,
                    laterality: this.bodyPart === 'Spine' ? 'None' : this.laterality,
                    contrast: this.contrast
                }
            })
            .subscribe({
                next: () => {
                    this.isSubmitting.set(false);
                },
                error: () => {
                    this.isSubmitting.set(false);
                    this.errorMessage.set('Unable to capture MRI information.');
                }
            });
    }
}
