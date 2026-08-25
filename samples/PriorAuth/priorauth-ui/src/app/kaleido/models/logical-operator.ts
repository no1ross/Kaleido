import { EnumOption } from './enum-option';

export enum LogicalOperator {

  And = 'And',

  Or = 'Or'
}

export const LOGICAL_OPERATOR_OPTIONS:
  EnumOption<LogicalOperator>[] = [

  {
    value: LogicalOperator.And,
    label: 'And'
  },

  {
    value: LogicalOperator.Or,
    label: 'Or'
  }
];