import {
  Component,
  Input,
  Output,
  EventEmitter,
  inject,
  ChangeDetectorRef
} from '@angular/core';

import { CatalogState } from '../../models/catalog-state';
import { CategoryCatalogView } from '../../models/category-catalog-view';
import { QueryableService } from '../../../kaleido/services/queryable-service';
import { QueryRequest } from '../../../kaleido/models/queryable-request';
import { ProductsByCategoryParameters } from '../../models/product-catalog-view';

@Component({
  selector: 'ecommerce-category-list',
  standalone: true,
  templateUrl: './category-list.html',
  styleUrl: './category-list.scss'
})
export class CategoryList {

  @Input({ required: true })
  productQuery!: QueryRequest<any>;

  @Output()
  queryCategoryChanged =
    new EventEmitter<QueryRequest>();

    private readonly queryableService =
        inject(QueryableService);

    private readonly changeDetector =
        inject(ChangeDetectorRef);
        
    categories: CategoryCatalogView[] = [];
    
    isLoading = false;

    errorMessage?: string;

    ngOnChanges()
    {
        this.loadCategories();
    }

  clearCategory(): void {

      const queryRequest =
          structuredClone(
              this.productQuery);

      delete queryRequest.parameters;

      if (queryRequest.query?.page) {
          queryRequest.query.page.offset = 0;
      }

      this.queryCategoryChanged.emit(
          queryRequest);
  }

  get hasCategories(): boolean {
    return this.categories.length > 0;
  }

  get selectedCategory(): string | undefined {

      return this.productQuery
          .parameters?.categoryPath;
  }

  getIndent(
    category: CategoryCatalogView): number {

    return category.level * 20;
  }

  isSelected(
    category: CategoryCatalogView): boolean {

    return this.selectedCategory ===
      category.categoryPath;
  }

selectCategory(
    category: CategoryCatalogView): void {

    const queryRequest =
        structuredClone(
            this.productQuery);

    queryRequest.parameters =
    {
        categoryPath:
            category.categoryPath
    };

    if (queryRequest.query?.page) {
        queryRequest.query.page.offset = 0;
    }

    this.queryCategoryChanged.emit(
        queryRequest);
}

  private loadCategories(): void {

    const request: QueryRequest<any> = {
        ...this.productQuery
    };

    if (this.selectedCategory) {
        request.parameters = {
            categoryPath: this.selectedCategory
        };
    }
    else {
        delete request.parameters;
    }
      
    this.queryableService
      .query<CategoryCatalogView>(
        'products/categories',
        request)
      .subscribe({
        next: result => {

          this.categories =
            result.records;

          this.changeDetector.detectChanges();
        },

        error: error => {

          this.handleError(error);
        }        
      });
  }

  private handleError(
    error: any): void {

    console.error(error);

    if (error.errors?.length > 0) {

      this.errorMessage =
        error.errors[0].message;

    } else {

      this.errorMessage =
        'An unexpected error occurred.';
    }

    this.isLoading =
      false;
  }
}