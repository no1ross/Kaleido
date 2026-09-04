import { ApplicationConfig, provideBrowserGlobalErrorListeners, provideAppInitializer, inject } from '@angular/core';
import { provideRouter } from '@angular/router';
import { provideHttpClient, withInterceptors } from '@angular/common/http';
import { firstValueFrom } from 'rxjs';

import { routes } from './app.routes';
import { RegistryCatalog } from './registries/registry-catalog';
import { kaleidoCorrelationInterceptor } from './kaleido/interceptors/kaleido-correlation-interceptor';

export const appConfig: ApplicationConfig = {
  providers: [
    provideBrowserGlobalErrorListeners(),
    provideRouter(routes),
    provideHttpClient(
      withInterceptors([kaleidoCorrelationInterceptor])
    ),
    provideAppInitializer(async () => {
      const registryCatalog =
        inject(RegistryCatalog);

      await firstValueFrom(
        registryCatalog.loadAll());
    })
  ]
};
