import {
  Component,
  Input
} from '@angular/core';

import {
  CurrencyPipe
} from '@angular/common';

import {
  ProductCatalogRecord
} from '../models/product-catalog-record';

@Component({
  selector: 'ecommerce-product-results',
  standalone: true,
  imports: [
    CurrencyPipe
  ],
  templateUrl: './product-results.html',
  styleUrl: './product-results.scss'
})
export class ProductResults {

  @Input({ required: true })
  products!: ProductCatalogRecord[];
}