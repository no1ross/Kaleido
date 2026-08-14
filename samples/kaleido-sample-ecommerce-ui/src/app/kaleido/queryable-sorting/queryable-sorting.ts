import {
  Component,
  inject,
  OnChanges,
  SimpleChanges
} from '@angular/core';

import {
  QueryRequest,
  QuerySort
} from '../models/queryable-request';

import {
  QueryableSortField
} from '../models/queryable-sort-field';

import {
  SortDirection
} from '../models/sort-direction';

import {
  SORT_DIRECTION_OPTIONS
} from '../models/sort-direction';

import { QueryExecutionStateService } from '../services/query-state-service';

@Component({
  selector: 'kaleido-queryable-sorting',
  imports: [],
  templateUrl: './queryable-sorting.html',
  styleUrl: './queryable-sorting.scss',
})
export class QueryableSorting {

  private readonly queryState =
      inject(QueryExecutionStateService);

  workingSorts: QuerySort[] = [];

  readonly sortDirections = SORT_DIRECTION_OPTIONS;

  readonly fields: QueryableSortField[] = [
    {
      field: 'productName',
      label: 'Product'
    },
    {
      field: 'supplierName',
      label: 'Supplier'
    },
    {
      field: 'categoryName',
      label: 'Category'
    },
    {
      field: 'price',
      label: 'Price'
    },
    {
      field: 'rating',
      label: 'Rating'
    },
    {
      field: 'reviewCount',
      label: 'Reviews'
    },
    {
      field: 'availableQuantity',
      label: 'Available'
    }
  ];

  ngOnInit(): void {

      this.workingSorts =
          (this.queryState.state.request.query?.sort ?? [])
              .map(sort => ({
                  ...sort
              }));
  }

  addSort(): void {

    if (this.fields.length === 0) {
      return;
    }

    const existingFields =
      new Set(
        this.workingSorts.map(
          sort => sort.field));

    const nextField =
      this.fields.find(
        field =>
          !existingFields.has(
            field.field));

    if (!nextField) {
      return;
    }

    this.workingSorts.push({
      field: nextField.field,
      direction: SortDirection.Ascending,
      sequence:
        this.workingSorts.length + 1
    });
  }

  removeSort(
    index: number): void {

    this.workingSorts =
      this.workingSorts
        .filter(
          (_, currentIndex) =>
            currentIndex !== index)
        .map(
          (sort, currentIndex) => ({
            ...sort,
            sequence: currentIndex + 1
          }));
  }

  fieldChanged(
    index: number,
    event: Event): void {

    const select =
      event.target as HTMLSelectElement;

    this.workingSorts[index] =
    {
      ...this.workingSorts[index],
      field: select.value
    };
  }

  directionChanged(
    index: number,
    event: Event): void {

    const select =
      event.target as HTMLSelectElement;

    this.workingSorts[index] =
    {
      ...this.workingSorts[index],
      direction:
        select.value as SortDirection
    };
  }

  apply(): void {

      this.queryState.state.request.query ??= {};

      this.queryState.state.request.query.sort =
          this.workingSorts.map(
              sort => ({
                  ...sort
              }));

      if (this.queryState.state.request.query.page) {

          this.queryState.state.request.query.page.offset = 0;
      }

      this.queryState.notifyChanged();
  }

  getFieldLabel(
    field: string): string {

    return this.fields.find(
      option =>
        option.field === field)
      ?.label ?? field;
  }

  isFieldSelected(
    field: string,
    currentIndex: number): boolean {

    return this.workingSorts.some(
      (sort, index) =>
        index !== currentIndex &&
        sort.field === field);
  }
}