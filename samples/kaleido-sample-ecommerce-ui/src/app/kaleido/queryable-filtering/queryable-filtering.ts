import {
  Component,
  EventEmitter,
  Input,
  OnChanges,
  Output,
  SimpleChanges
} from '@angular/core';

import {
  QueryFilterCondition,
  QueryFilterGroup,
  QueryFilterNode,
  QueryRequest
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

@Component({
  selector: 'kaleido-queryable-filtering',
  imports: [],
  templateUrl: './queryable-filtering.html',
  styleUrl: './queryable-filtering.scss'
})
export class QueryableFiltering implements OnChanges {

  @Input({ required: true })
  queryRequest!: QueryRequest;

  @Output()
  queryRequestChanged =
    new EventEmitter<QueryRequest>();

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

  ngOnChanges(
    changes: SimpleChanges): void {

    if (changes['queryRequest']) {

      this.workingFilter =
        this.queryRequest.query.filter
          ? structuredClone(
              this.queryRequest.query.filter)
          : this.createRootGroup();
    }
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

    const queryRequest =
      structuredClone(
        this.queryRequest);

    queryRequest.query.filter =
      structuredClone(
        this.workingFilter);

    queryRequest.query.page.offset =
      0;

    this.queryRequestChanged.emit(
      queryRequest);
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
