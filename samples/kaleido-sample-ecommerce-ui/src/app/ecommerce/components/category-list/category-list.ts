import {
  Component,
  inject,
  ChangeDetectorRef,
  OnInit,
  OnDestroy
} from '@angular/core';
import { Subscription } from 'rxjs';
import { CategoryCatalogView } from '../../models/category-catalog-view';
import { QueryableService } from '../../../kaleido/services/queryable-service';
import { ProductsByCategoryParameters } from '../../models/product-catalog-view';

import { ProductContextStateService } from '../../services/product-context-state-service';

@Component({
  selector: 'ecommerce-category-list',
  standalone: true,
  templateUrl: './category-list.html',
  styleUrl: './category-list.scss'
})
export class CategoryList implements OnInit, OnDestroy {

  private readonly queryState =
      inject(ProductContextStateService);

  private readonly queryableService =
      inject(QueryableService);

  private readonly changeDetector =
      inject(ChangeDetectorRef);

  private querySubscription?: Subscription;
      
  categories: CategoryCatalogView[] = [];

  isLoading = false;

  errorMessage?: string;

  ngOnInit(): void {
      this.querySubscription =
          this.queryState.changed
              .subscribe(() => {
                  this.loadCategories();
              });
    this.loadCategories();
  }

  ngOnDestroy(): void {
      this.querySubscription?.unsubscribe();
  }

  clearCategory(): void {
      delete this.queryState.state.request.parameters;

      if (this.queryState.state.request.query?.page) {
          this.queryState.state.request.query.page.offset = 0;
      }

      this.queryState.notifyChanged();

      this.loadCategories();
  }

  get hasCategories(): boolean {
    return this.categories.length > 0;
  }

  get selectedCategory(): string | undefined {
      const parameters =
          this.queryState.state.request
              ?.parameters as
                  ProductsByCategoryParameters
                  | undefined;

      return parameters?.categoryPath;
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

  categorySelected(
      categoryPath: string): void {

      this.queryState.state.request.parameters = {
          categoryPath
      };

      if (this.queryState.state.request.query?.page) {
          this.queryState.state.request.query.page.offset = 0;
      }

      this.queryState.notifyChanged();

      this.loadCategories();
  }

    private get viewName(): string {

        const parameters =
            this.queryState.state.request.parameters as
                ProductsByCategoryParameters | undefined;

        return parameters?.categoryPath
            ? 'product-by-category'
            : 'product-list';
    }

  private loadCategories(): void {
        const context = 'products';
        const viewName = 'categories';
        const request = this.queryState.state.request;

      this.queryableService
          .query<CategoryCatalogView>(
              context,
              viewName,
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