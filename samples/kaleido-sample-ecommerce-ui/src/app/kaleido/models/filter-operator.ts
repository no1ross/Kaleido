import { EnumOption } from './enum-option';

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

export const FILTER_OPERATOR_OPTIONS:
  EnumOption<FilterOperator>[] = [

  { value: FilterOperator.Equals, label: 'Equals' },
  { value: FilterOperator.NotEquals, label: 'Not Equals' },

  { value: FilterOperator.GreaterThan, label: 'Greater Than' },
  { value: FilterOperator.LessThan, label: 'Less Than' },
  { value: FilterOperator.GreaterThanOrEqual, label: 'Greater Than Or Equal' },
  { value: FilterOperator.LessThanOrEqual, label: 'Less Than Or Equal' },

  { value: FilterOperator.Contains, label: 'Contains' },
  { value: FilterOperator.NotContains, label: 'Not Contains' },
  { value: FilterOperator.StartsWith, label: 'Starts With' },
  { value: FilterOperator.EndsWith, label: 'Ends With' },

  { value: FilterOperator.In, label: 'In' },
  { value: FilterOperator.NotIn, label: 'Not In' },
  { value: FilterOperator.Between, label: 'Between' },
  { value: FilterOperator.NotBetween, label: 'Not Between' },

  { value: FilterOperator.IsNull, label: 'Is Null' },
  { value: FilterOperator.IsNotNull, label: 'Is Not Null' },

  { value: FilterOperator.IsTrue, label: 'Is True' },
  { value: FilterOperator.IsFalse, label: 'Is False' }
];