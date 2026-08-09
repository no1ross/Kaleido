import { Routes } from '@angular/router';

import { ECommerce } from './ecommerce/ecommerce';

export const routes: Routes = [
  {
    path: '',
    redirectTo: 'ecommerce',
    pathMatch: 'full'
  },
  {
    path: 'ecommerce',
    component: ECommerce
  }
];