import { Component, computed, inject, signal } from '@angular/core';
import { NavigationEnd, Router, RouterLink, RouterLinkActive, RouterOutlet } from '@angular/router';
import { filter, map, startWith } from 'rxjs';

import { ProcessStateService } from './process/services/process-state-service';
import { AuthService } from './auth/auth-service';
import { RegistryCatalog } from './registries/registry-catalog';

@Component({
  selector: 'app-root',
  imports: [RouterOutlet, RouterLink, RouterLinkActive],
  templateUrl: './app.html',
  styleUrl: './app.scss'
})
export class App {
  private readonly router = inject(Router);
  private readonly processState = inject(ProcessStateService);
  private readonly registryCatalog = inject(RegistryCatalog);
  protected readonly auth = inject(AuthService);

  protected readonly title = signal('Prior Auth UI');

  readonly isProcessRoute =
    signal(this.router.url.startsWith('/process'));

  readonly processId =
    computed(() => this.processState.state().processId);

  onPersonaChange(event: Event): void {
    const name = (event.target as HTMLSelectElement).value;

    // The registry is filtered per caller — a persona switch must clear the
    // in-flight process state and re-fetch capabilities for the new caller.
    this.processState.reset();

    if (!name) {
      this.auth.logout();
      this.registryCatalog.refresh();
      return;
    }
    this.auth.login(name).subscribe({
      next: () => this.registryCatalog.refresh(),
      error: err => console.error('Login failed', err)
    });
  }

  exitProcess(): void {
    this.processState.reset();
    void this.router.navigate(['/']);
  }

  constructor() {
    this.router.events
      .pipe(
        filter(event => event instanceof NavigationEnd),
        map(() => this.router.url.startsWith('/process')),
        startWith(this.router.url.startsWith('/process')))
      .subscribe(isProcessRoute => {
        this.isProcessRoute.set(isProcessRoute);
      });
  }
}
