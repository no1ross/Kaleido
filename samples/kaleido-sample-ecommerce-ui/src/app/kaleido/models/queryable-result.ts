export interface QueryableResult<TRecord> {
  totalCount: number;
  offset: number;
  pageSize: number;
  records: TRecord[];
}