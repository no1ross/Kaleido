import { Routes } from '@angular/router';

import { ECommerce } from './ecommerce/ecommerce';
import { ProductCatalog } from './ecommerce/product-catalog/product-catalog';
import { ShoppingCart } from './ecommerce/shopping-cart/shopping-cart';
import { OrderReview } from './ecommerce/order-review/order-review';
import { OrderDetails } from './ecommerce/order-details/order-details';
import { BasicQueryTest } from './ecommerce/diagnostics/basic-query-test';
import { Registries } from './registries/registries';
import { ProcessRegistryViewer } from './registries/process-registry/process-registry';
import { QueryableRegistryViewer } from './registries/queryable-registry/queryable-registry';

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
          path: 'shopping-cart',
          component: ShoppingCart
      },
      {
          path: 'order-review',
          component: OrderReview
      },
      {
          path: 'order-details',
          component: OrderDetails
      },
      {
          path: 'diagnostics/basic-query',
          component: BasicQueryTest
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