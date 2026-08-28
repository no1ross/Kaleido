import { Component, inject } from '@angular/core';
import { Router } from '@angular/router';

@Component({
    selector: 'priorauth-confirm-ct-instead-of-mri',
    standalone: true,
    templateUrl: './confirm-ct-instead-of-mri.html',
    styleUrl: './confirm-ct-instead-of-mri.scss'
})
export class ConfirmCtInsteadOfMri {
    private readonly router =
        inject(Router);

    continueWithCt(): void {
        void this.router.navigate(['/process', 'requested-services']);
    }

    switchToMri(): void {
        void this.router.navigate(['/process', 'capture-mri-info']);
    }
}
