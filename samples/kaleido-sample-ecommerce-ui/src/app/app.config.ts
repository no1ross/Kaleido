import { ApplicationConfig, provideAppInitializer, provideBrowserGlobalErrorListeners, inject } from '@angular/core';
import { provideRouter } from '@angular/router';
import { provideHttpClient } from '@angular/common/http';
import { firstValueFrom } from 'rxjs';
import { ProcessRegistry } from './kaleido/services/process-registry';
import { QueryableRegistry } from './kaleido/services/queryable-registry';

import { routes } from './app.routes';

export const appConfig: ApplicationConfig = {
  providers: [
    provideBrowserGlobalErrorListeners(),
    provideRouter(routes),
    provideHttpClient(),
        provideAppInitializer(async () => {

            const processRegistry =
                inject(ProcessRegistry);

            const queryableRegistry =
                inject(QueryableRegistry);

            await Promise.all([
                firstValueFrom(
                    processRegistry.initialize()),
                firstValueFrom(
                    queryableRegistry.initialize())
            ]);
        })
  ]
};
