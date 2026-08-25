import { EnumOption } from './enum-option';

export enum MatchMode {

  Exact = 'Exact',
  StartsWith = 'StartsWith',
  EndsWith = 'EndsWith',
  Contains = 'Contains'
}

export const MATCH_MODE_OPTIONS:
  EnumOption<MatchMode>[] = [

  {
    value: MatchMode.Exact,
    label: 'Exact Match'
  },

  {
    value: MatchMode.StartsWith,
    label: 'Starts With'
  },

  {
    value: MatchMode.EndsWith,
    label: 'Ends With'
  },

  {
    value: MatchMode.Contains,
    label: 'Contains'
  }
];