import { Routes } from '@angular/router';

import { PriorAuthHome } from './home/home';
import { MemberSearch } from './member-search/member-search';
import { Registries } from './registries/registries';
import { ProcessRegistryViewer } from './registries/process-registry/process-registry';
import { QueryableRegistryViewer } from './registries/queryable-registry/queryable-registry';

export const routes: Routes = [
  {
    path: '',
    component: PriorAuthHome
  },
  {
    path: 'member-search',
    component: MemberSearch
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
