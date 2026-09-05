import {
  Component,
  computed,
  inject,
  OnInit,
  OnDestroy,
  signal
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

  private querySubscription?: Subscription;
      
  readonly categories =
      signal<CategoryCatalogView[]>([]);

  readonly isLoading =
      signal(false);

  readonly errorMessage =
      signal<string | undefined>(undefined);

  readonly selectedCategory =
      computed(() => {
          const parameters =
              this.queryState.state.request
                  ?.parameters as
                      ProductsByCategoryParameters
                      | undefined;

          return parameters?.categoryPath;
      });

  ngOnInit(): void {
      this.querySubscription =
          this.queryState.changed
              .subscribe(() => {
                  this.loadCategories();
              });
    this.queryState.notifyChanged();
  }

  ngOnDestroy(): void {
      this.querySubscription?.unsubscribe();
  }

    clearCategory(): void {

        this.queryState.state.request.parameters = {
            categoryPath: ''
        };

        if (this.queryState.state.request.query?.page) {

            this.queryState.state.request.query.page.offset = 0;
        }

        this.queryState.notifyChanged();
    }

  get hasCategories(): boolean {
    return this.categories().length > 0;
  }

  getIndent(
    category: CategoryCatalogView): number {

    return category.level * 20;
  }

  isSelected(
    category: CategoryCatalogView): boolean {

    return this.selectedCategory() ===
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
  }

  private loadCategories(): void {
    this.isLoading.set(true);
    this.errorMessage.set(undefined);

    const request =
        this.queryState.state.request;

    request.parameters ??= {
        categoryPath: ''
    };

      this.queryableService
          .query<CategoryCatalogView>(
              'categories',
              request)
          .subscribe({
              next: result => {

                  this.categories.set(
                      result.results);

                  this.isLoading.set(false);
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

      this.errorMessage.set(
        error.errors[0].message);

    } else {

      this.errorMessage.set(
        'An unexpected error occurred.');
    }

    this.isLoading.set(false);
  }
}
