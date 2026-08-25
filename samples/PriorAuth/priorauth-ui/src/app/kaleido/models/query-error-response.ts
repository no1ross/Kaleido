export interface QueryError {
    code: string;
    message: string;
    field?: string;
}

export interface QueryErrorResponse {
    errors: QueryError[];
}
