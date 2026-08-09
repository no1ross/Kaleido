import { Component } from '@angular/core';

import { ProductCatalog } from './product-catalog/product-catalog';

@Component({
  selector: 'app-ecommerce',
  imports: [ ProductCatalog ],
  templateUrl: './ecommerce.html',
  styleUrl: './ecommerce.scss',
})
export class ECommerce {
}