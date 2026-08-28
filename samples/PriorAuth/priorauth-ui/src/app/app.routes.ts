import { Routes } from '@angular/router';

import { PriorAuthHome } from './home/home';
import { MemberSearch } from './member/member-search/member-search';
import { Registries } from './registries/registries';
import { ProcessRegistryViewer } from './registries/process-registry/process-registry';
import { QueryableRegistryViewer } from './registries/queryable-registry/queryable-registry';
import { ProcessShell } from './process/process-shell';
import { RequestedService } from './process/requested-service';
import { CaptureMriInfo } from './process/capture-mri-info';
import { ConfirmCtInsteadOfMri } from './process/confirm-ct-instead-of-mri';
import { RequestedServicesSummary } from './process/requested-services-summary';
import { RequestingProvider } from './process/requesting-provider';
import { ServicingProvider } from './process/servicing-provider';

export const routes: Routes = [
  {
    path: '',
    component: PriorAuthHome
  },
  {
    path: 'process',
    component: ProcessShell,
    children: [
      {
        path: '',
        redirectTo: 'member-search',
        pathMatch: 'full'
      },
      {
        path: 'member-search',
        component: MemberSearch
      },
      {
        path: 'requested-service',
        component: RequestedService
      },
      {
        path: 'capture-mri-info',
        component: CaptureMriInfo
      },
      {
        path: 'confirm-ct-instead-of-mri',
        component: ConfirmCtInsteadOfMri
      },
      {
        path: 'requested-services',
        component: RequestedServicesSummary
      },
      {
        path: 'requesting-provider',
        component: RequestingProvider
      },
      {
        path: 'servicing-provider',
        component: ServicingProvider
      }
    ]
  },
  {
    path: 'registries',
    component: Registries,
    children: [
      {
        path: '',
        redirectTo: 'process',
        pathMatch: 'full'
      },
      {
        path: 'process',
        component: ProcessRegistryViewer
      },
      {
        path: 'queryable',
        component: QueryableRegistryViewer
      }
    ]
  }
];
