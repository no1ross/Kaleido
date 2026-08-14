import {
  Component,
  inject
} from '@angular/core';

import {
  QueryFilterCondition,
  QueryFilterGroup,
  QueryFilterNode
} from '../models/queryable-request';

import {
  FILTER_OPERATOR_OPTIONS,
  FilterOperator
} from '../models/filter-operator';

import {
  LOGICAL_OPERATOR_OPTIONS,
  LogicalOperator
} from '../models/logical-operator';

import {
  QueryableFilterField
} from './queryable-filter-field';

import {
  QueryExecutionStateService
} from '../services/query-state-service';

@Component({
  selector: 'kaleido-queryable-filtering',
  imports: [],
  templateUrl: './queryable-filtering.html',
  styleUrl: './queryable-filtering.scss'
})
export class QueryableFiltering {

    private readonly queryState =
      inject(QueryExecutionStateService);
    
    readonly fields: QueryableFilterField[] = [
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
    },
    {
      field: 'isActive',
      label: 'Active'
    }
  ];

  private readonly valueLessOperators = [
    FilterOperator.IsTrue,
    FilterOperator.IsFalse,
    FilterOperator.IsNull,
    FilterOperator.IsNotNull
  ];

  requiresValue(
    operator: FilterOperator): boolean {

    return !this.valueLessOperators.includes(
      operator);
  }

  readonly filterOperators =
    FILTER_OPERATOR_OPTIONS;

  readonly logicalOperators =
    LOGICAL_OPERATOR_OPTIONS;

  workingFilter: QueryFilterNode =
    this.createRootGroup();

  ngOnInit(): void {

      this.workingFilter =
          this.queryState.state.request.query?.filter
              ? structuredClone(
                  this.queryState.state.request.query.filter)
              : this.createRootGroup();
  }

  get rootGroup(): QueryFilterGroup {

    return this.workingFilter.group!;
  }

  get hasFilters(): boolean {

    return this.rootGroup.filters.length > 0;
  }

  get showLogicalOperator(): boolean {

    return this.rootGroup.filters.length > 1;
  }

  addFilter(): void {

    const field =
      this.fields[0]?.field ?? '';

    this.rootGroup.filters.push({
      condition: {
        field,
        operator: FilterOperator.Equals,
        values: ['']
      }
    });
  }

  removeFilter(
    index: number): void {

    this.rootGroup.filters.splice(
      index,
      1);
  }

  groupOperatorChanged(
    event: Event): void {

    const select =
      event.target as HTMLSelectElement;

    this.rootGroup.operator =
      select.value as LogicalOperator;
  }

  fieldChanged(
    condition: QueryFilterCondition,
    event: Event): void {

    const select =
      event.target as HTMLSelectElement;

    condition.field =
      select.value;
  }

  operatorChanged(
    condition: QueryFilterCondition,
    event: Event): void {

    const select =
      event.target as HTMLSelectElement;

    condition.operator =
      select.value as FilterOperator;

    if (!this.requiresValue(
      condition.operator)) {

      condition.values = [];
    }
  }

  valueChanged(
    condition: QueryFilterCondition,
    event: Event): void {

    const input =
      event.target as HTMLInputElement;

    condition.values =
      [
        this.coerceValue(
          condition.field,
          input.value)
      ];
  }

  apply(): void {

      this.queryState.state.request.query ??= {};

      const hasFilters =
          this.rootGroup.filters.length > 0;

      this.queryState.state.request.query.filter =
          hasFilters
              ? structuredClone(
                  this.workingFilter)
              : undefined;

      if (this.queryState.state.request.query.page) {

          this.queryState.state.request.query.page.offset = 0;
      }

      this.queryState.notifyChanged();
  }

  private createRootGroup():
    QueryFilterNode {

    return {
      group: {
        operator: LogicalOperator.And,
        filters: []
      }
    };
  }

  private coerceValue(
    field: string,
    value: string): unknown {

    if (
      field === 'price' ||
      field === 'rating'
    ) {
      return Number(value);
    }

    if (
      field === 'reviewCount' ||
      field === 'availableQuantity'
    ) {
      return Number(value);
    }

    return value;
  }
}
