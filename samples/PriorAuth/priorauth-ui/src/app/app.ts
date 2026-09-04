import { Component, computed, inject, signal } from '@angular/core';
import { NavigationEnd, Router, RouterLink, RouterLinkActive, RouterOutlet } from '@angular/router';
import { filter, map, startWith } from 'rxjs';

import { ProcessStateService } from './process/services/process-state-service';

@Component({
  selector: 'app-root',
  imports: [RouterOutlet, RouterLink, RouterLinkActive],
  templateUrl: './app.html',
  styleUrl: './app.scss'
})
export class App {
  private readonly router = inject(Router);
  private readonly processState = inject(ProcessStateService);

  protected readonly title = signal('Prior Auth UI');

  readonly isProcessRoute =
    signal(this.router.url.startsWith('/process'));

  readonly processId =
    computed(() => this.processState.state().processId);

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
