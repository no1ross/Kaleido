export interface QueryableResult<TView> {
    totalCount: number;
    offset: number;
    pageSize: number;
    results: TView[];
}
