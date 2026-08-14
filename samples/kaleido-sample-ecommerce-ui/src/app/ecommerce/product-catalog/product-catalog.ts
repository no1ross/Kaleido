import {
  Component,
  inject,
} from '@angular/core';

import { QueryablePager } from '../../kaleido/queryable-pager/queryable-pager';
import { QueryableSorting } from '../../kaleido/queryable-sorting/queryable-sorting';
import { QueryableFiltering } from '../../kaleido/queryable-filtering/queryable-filtering';
import { QueryableSearch } from '../../kaleido/queryable-search/queryable-search';
import { ProductResults } from '../components/product-results/product-results';
import { CategoryList } from '../components/category-list/category-list';
import { ProductContextStateService } from '../services/product-context-state-service';
import { QueryExecutionStateService } from '../../kaleido/services/query-state-service';
import { QueryResultStateService } from '../../kaleido/services/query-state-service';

@Component({
  selector: 'ecommerce-product-catalog',
  imports: [
    QueryablePager,
    QueryableSorting,
    QueryableFiltering,
    QueryableSearch,
    ProductResults,
    CategoryList
  ],
  providers: [
    ProductContextStateService,
    {
      provide: QueryExecutionStateService,
      useExisting: ProductContextStateService
    },
    QueryResultStateService
  ],
  templateUrl: './product-catalog.html',
  styleUrl: './product-catalog.scss',
})
export class ProductCatalog {

  ngOnInit(): void {
  }

  productsLoaded() {

  }
}