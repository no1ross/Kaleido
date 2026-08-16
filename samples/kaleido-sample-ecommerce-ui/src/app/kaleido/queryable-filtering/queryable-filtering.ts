import {
  Component,
  inject,
  Input,
  OnInit
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

import {
  QueryableRegistry
} from '../services/queryable-registry';

import {
  QueryableField
} from '../models/queryable-registry';

@Component({
  selector: 'kaleido-queryable-filtering',
  imports: [],
  templateUrl: './queryable-filtering.html',
  styleUrl: './queryable-filtering.scss'
})
export class QueryableFiltering implements OnInit {

  @Input({ required: true })
  contextName!: string;

  private readonly queryState =
    inject(QueryExecutionStateService);

  private readonly queryableRegistry =
    inject(QueryableRegistry);

  fields: QueryableFilterField[] = [];

  private readonly fieldsByName =
    new Map<string, QueryableField>();

  private readonly valueLessOperators: FilterOperator[] = [
    FilterOperator.IsTrue,
    FilterOperator.IsFalse,
    FilterOperator.IsNull,
    FilterOperator.IsNotNull
  ];

  private readonly rangeOperators: FilterOperator[] = [
    FilterOperator.Between,
    FilterOperator.NotBetween
  ];

  readonly filterOperators =
    FILTER_OPERATOR_OPTIONS;

  readonly logicalOperators =
    LOGICAL_OPERATOR_OPTIONS;

  workingFilter: QueryFilterNode =
    this.createRootGroup();

  ngOnInit(): void {

    this.loadFieldsFromRegistry();

    this.workingFilter =
      this.queryState.state.request.query?.filter
        ? structuredClone(
            this.queryState.state.request.query.filter)
        : this.createRootGroup();

    this.normalizeExistingFilter();
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

    const operator =
      this.getDefaultOperatorForField(
        field);

    this.rootGroup.filters.push({
      condition: {
        field,
        operator,
        values: this.createInitialValues(
          operator)
      }
    });
  }

  removeFilter(
    index: number
  ): void {

    this.rootGroup.filters.splice(
      index,
      1);
  }

  groupOperatorChanged(
    event: Event
  ): void {

    const select =
      event.target as HTMLSelectElement;

    this.rootGroup.operator =
      select.value as LogicalOperator;
  }

  fieldChanged(
    condition: QueryFilterCondition,
    event: Event
  ): void {

    const select =
      event.target as HTMLSelectElement;

    condition.field =
      select.value;

    condition.operator =
      this.getDefaultOperatorForField(
        condition.field);

    condition.values =
      this.createInitialValues(
        condition.operator);
  }

  operatorChanged(
    condition: QueryFilterCondition,
    event: Event
  ): void {

    const select =
      event.target as HTMLSelectElement;

    condition.operator =
      select.value as FilterOperator;

    condition.values =
      this.createInitialValues(
        condition.operator);
  }

  valueChanged(
    condition: QueryFilterCondition,
    event: Event
  ): void {

    this.valueAtIndexChanged(
      condition,
      0,
      event);
  }

  valueAtIndexChanged(
    condition: QueryFilterCondition,
    index: number,
    event: Event
  ): void {

    const input =
      event.target as HTMLInputElement;

    this.setValueAtIndex(
      condition,
      index,
      input.value);
  }

  booleanValueChanged(
    condition: QueryFilterCondition,
    event: Event
  ): void {

    const select =
      event.target as HTMLSelectElement;

    this.setValueAtIndex(
      condition,
      0,
      select.value);
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

  getFilterOperators(
    condition: QueryFilterCondition
  ): typeof FILTER_OPERATOR_OPTIONS {

    const fieldMetadata =
      this.fieldsByName.get(
        condition.field);

    if (!fieldMetadata) {

      return [];
    }

    const allowedOperators =
      fieldMetadata.filterOperators.map(
        operator =>
          operator.toString());

    return FILTER_OPERATOR_OPTIONS.filter(
      option =>
        allowedOperators.includes(
          option.value.toString()));
  }

  requiresValue(
    operator: FilterOperator
  ): boolean {

    return !this.valueLessOperators.includes(
      operator);
  }

  requiresSingleValue(
    operator: FilterOperator
  ): boolean {

    return this.getValueCount(
      operator) === 1;
  }

  requiresRangeValue(
    operator: FilterOperator
  ): boolean {

    return this.getValueCount(
      operator) === 2;
  }

  isRangeOperator(
    operator: FilterOperator
  ): boolean {

    return this.rangeOperators.includes(
      operator);
  }

  getValueCount(
    operator: FilterOperator
  ): number {

    if (!this.requiresValue(
      operator)) {

      return 0;
    }

    if (this.isRangeOperator(
      operator)) {

      return 2;
    }

    return 1;
  }

  getInputType(
    condition: QueryFilterCondition
  ): string {

    const field =
      this.fieldsByName.get(
        condition.field);

    if (!field) {

      return 'text';
    }

    if (
      field.dataType.format === 'date' ||
      field.dataType.type === 'date'
    ) {

      return 'date';
    }

    if (
      field.dataType.format === 'date-time' ||
      field.dataType.type === 'datetime'
    ) {

      return 'datetime-local';
    }

    switch (field.dataType.type) {

      case 'integer':
      case 'number':
        return 'number';

      default:
        return 'text';
    }
  }

  isBooleanField(
    condition: QueryFilterCondition
  ): boolean {

    const field =
      this.fieldsByName.get(
        condition.field);

    return field?.dataType.type === 'boolean';
  }

  shouldShowBooleanValueSelector(
    condition: QueryFilterCondition
  ): boolean {

    return this.requiresSingleValue(
      condition.operator) &&
      this.isBooleanField(
        condition);
  }

  shouldShowSingleValueInput(
    condition: QueryFilterCondition
  ): boolean {

    return this.requiresSingleValue(
      condition.operator) &&
      !this.isBooleanField(
        condition);
  }

  shouldShowRangeValueInputs(
    condition: QueryFilterCondition
  ): boolean {

    return this.requiresRangeValue(
      condition.operator);
  }

  getFirstValue(
    condition: QueryFilterCondition
  ): unknown {

    return condition.values[0] ?? '';
  }

  getSecondValue(
    condition: QueryFilterCondition
  ): unknown {

    return condition.values[1] ?? '';
  }

  getBooleanValue(
    condition: QueryFilterCondition
  ): string {

    const value =
      condition.values[0];

    if (value === true) {

      return 'true';
    }

    if (value === false) {

      return 'false';
    }

    return '';
  }

  getFieldMetadata(
    fieldName: string
  ): QueryableField | undefined {

    return this.fieldsByName.get(
      fieldName);
  }

  private loadFieldsFromRegistry(): void {

    const filterableFields =
      this.queryableRegistry.getFilterableFields(
        this.contextName);

    this.fieldsByName.clear();

    for (const field of filterableFields) {

      this.fieldsByName.set(
        field.name,
        field);
    }

    this.fields =
      filterableFields.map(
        field => ({
          field: field.name,
          label: this.getFieldLabel(
            field)
        }));
  }

  private normalizeExistingFilter(): void {

    for (const node of this.rootGroup.filters) {

      const condition =
        node.condition;

      if (!condition) {

        continue;
      }

      if (!this.fieldsByName.has(
        condition.field)) {

        condition.field =
          this.fields[0]?.field ?? '';
      }

      condition.operator =
        this.normalizeOperator(
          condition.field,
          condition.operator);

      condition.values =
        this.normalizeValues(
          condition);
    }
  }

  private normalizeOperator(
    fieldName: string,
    operator: FilterOperator
  ): FilterOperator {

    const allowedOperators =
      this.getAllowedOperatorsForField(
        fieldName);

    if (allowedOperators.length === 0) {

      return FilterOperator.Equals;
    }

    if (allowedOperators.includes(
      operator)) {

      return operator;
    }

    return allowedOperators[0];
  }

  private normalizeValues(
    condition: QueryFilterCondition
  ): unknown[] {

    const valueCount =
      this.getValueCount(
        condition.operator);

    if (valueCount === 0) {

      return [];
    }

    if (valueCount === 1) {

      return [
        this.coerceValue(
          condition.field,
          condition.values[0] ?? '')
      ];
    }

    return [
      this.coerceValue(
        condition.field,
        condition.values[0] ?? ''),
      this.coerceValue(
        condition.field,
        condition.values[1] ?? '')
    ];
  }

  private getDefaultOperatorForField(
    fieldName: string
  ): FilterOperator {

    const allowedOperators =
      this.getAllowedOperatorsForField(
        fieldName);

    return allowedOperators[0] ??
      FilterOperator.Equals;
  }

  private getAllowedOperatorsForField(
    fieldName: string
  ): FilterOperator[] {

    const fieldMetadata =
      this.fieldsByName.get(
        fieldName);

    if (!fieldMetadata) {

      return [];
    }

    return fieldMetadata.filterOperators.map(
      operator =>
        operator as FilterOperator);
  }

  private createInitialValues(
    operator: FilterOperator
  ): unknown[] {

    const valueCount =
      this.getValueCount(
        operator);

    if (valueCount === 0) {

      return [];
    }

    if (valueCount === 1) {

      return [''];
    }

    return [
      '',
      ''
    ];
  }

  private setValueAtIndex(
    condition: QueryFilterCondition,
    index: number,
    value: string
  ): void {

    const expectedValueCount =
      this.getValueCount(
        condition.operator);

    if (expectedValueCount === 0) {

      condition.values = [];
      return;
    }

    while (condition.values.length < expectedValueCount) {

      condition.values.push(
        '');
    }

    condition.values[index] =
      this.coerceValue(
        condition.field,
        value);
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
    value: unknown
  ): unknown {

    const fieldMetadata =
      this.fieldsByName.get(
        field);

    if (!fieldMetadata) {

      return value;
    }

    if (value === '') {

      return value;
    }

    const dataType =
      fieldMetadata.dataType;

    if (
      dataType.type === 'integer' ||
      dataType.type === 'number'
    ) {

      return Number(value);
    }

    if (dataType.type === 'boolean') {

      if (value === true || value === false) {

        return value;
      }

      return value?.toString().toLowerCase() === 'true';
    }

    return value;
  }

  private getFieldLabel(
    field: QueryableField
  ): string {

    return field.name.replace(
      /([a-z])([A-Z])/g,
      '$1 $2');
  }
}