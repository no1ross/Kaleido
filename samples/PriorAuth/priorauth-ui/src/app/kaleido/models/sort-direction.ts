import { EnumOption } from './enum-option';

export enum SortDirection {

  Ascending = 'Ascending',

  Descending = 'Descending'
}

export const SORT_DIRECTION_OPTIONS:
  EnumOption<SortDirection>[] = [

  {
    value: SortDirection.Ascending,
    label: 'Ascending'
  },

  {
    value: SortDirection.Descending,
    label: 'Descending'
  }
];