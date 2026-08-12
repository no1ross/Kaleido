import { Routes } from '@angular/router';

import { ECommerce } from './ecommerce/ecommerce';
import { ProductCatalog } from './ecommerce/product-catalog/product-catalog';
import { ShoppingCart } from './ecommerce/shopping-cart/shopping-cart';

export const routes: Routes = [
  {
    path: '',
    redirectTo: 'ecommerce/products',
    pathMatch: 'full'
  },
  {
    path: 'ecommerce',
    component: ECommerce,
    children: [
      {
        path: '',
        redirectTo: 'products',
        pathMatch: 'full'
      },
      {
        path: 'products',
        component: ProductCatalog
      },
      {
        path: 'cart',
        component: ShoppingCart
      }
    ]
  }
];