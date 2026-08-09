import { SortDirection } from './sort-direction';
import { FilterOperator, LogicalOperator } from './enumerations';

export interface QueryRequest {
  query: QueryBody;
}

export interface QueryBody {
  //search?: SearchDefinition;
  filter?: QueryFilterNode;
  sort?: QuerySort[];
  page: QueryPage;
}

export interface QueryPage {
  size: number;
  offset: number;
}

export interface QuerySort {
    field: string;
    direction: SortDirection;
    sequence: number;
}

export interface QueryFilterNode {
  condition?: QueryFilterCondition;
  group?: QueryFilterGroup;
}

export interface QueryFilterCondition {
  field: string;
  operator: FilterOperator;
  values: unknown[];
}

export interface QueryFilterGroup {
  operator: LogicalOperator;
  filters: QueryFilterNode[];
}