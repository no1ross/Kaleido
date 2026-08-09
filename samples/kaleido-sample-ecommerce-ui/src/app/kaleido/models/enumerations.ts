export enum FilterOperator {
  // Equality
  Equals = 'Equals',
  NotEquals = 'NotEquals',
  // Comparison
  GreaterThan = 'GreaterThan',
  LessThan = 'LessThan',
  GreaterThanOrEqual = 'GreaterThanOrEqual',
  LessThanOrEqual = 'LessThanOrEqual',
  // String
  Contains = 'Contains',
  NotContains = 'NotContains',
  StartsWith = 'StartsWith',
  EndsWith = 'EndsWith',
  // Set
  In = 'In',
  NotIn = 'NotIn',
  Between = 'Between',
  NotBetween = 'NotBetween',
  // Null
  IsNull = 'IsNull',
  IsNotNull = 'IsNotNull',
  // Boolean
  IsTrue = 'IsTrue',
  IsFalse = 'IsFalse'
}
export enum MatchMode {
  Exact = 'Exact',
  StartsWith = 'StartsWith',
  EndsWith = 'EndsWith',
  Contains = 'Contains'
}
export enum SortDirection {
  Ascending = 'Ascending',
  Descending = 'Descending'
}
export enum LogicalOperator {
  And = 'And',
  Or = 'Or'
}

export const FILTER_OPERATOR_LABELS:
  Record<FilterOperator, string> =
{
  [FilterOperator.Equals]:
    'Equals',

  [FilterOperator.NotEquals]:
    'Not Equals',

  [FilterOperator.GreaterThan]:
    'Greater Than',

  [FilterOperator.LessThan]:
    'Less Than',

  [FilterOperator.GreaterThanOrEqual]:
    'Greater Than Or Equal',

  [FilterOperator.LessThanOrEqual]:
    'Less Than Or Equal',

  [FilterOperator.Contains]:
    'Contains',

  [FilterOperator.NotContains]:
    'Not Contains',

  [FilterOperator.StartsWith]:
    'Starts With',

  [FilterOperator.EndsWith]:
    'Ends With',

  [FilterOperator.In]:
    'In',

  [FilterOperator.NotIn]:
    'Not In',

  [FilterOperator.Between]:
    'Between',

  [FilterOperator.NotBetween]:
    'Not Between',

  [FilterOperator.IsNull]:
    'Is Null',

  [FilterOperator.IsNotNull]:
    'Is Not Null',

  [FilterOperator.IsTrue]:
    'Is True',

  [FilterOperator.IsFalse]:
    'Is False'
};

export const MATCH_MODE_LABELS:
  Record<MatchMode, string> =
{
  [MatchMode.Exact]:
    'Exact Match',

  [MatchMode.StartsWith]:
    'Starts With',

  [MatchMode.EndsWith]:
    'Ends With',

  [MatchMode.Contains]:
    'Contains'
};