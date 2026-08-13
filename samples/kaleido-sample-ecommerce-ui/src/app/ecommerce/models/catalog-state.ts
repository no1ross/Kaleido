import { QueryRequest } from "../../kaleido/models/queryable-request";

export interface CatalogState {

    selectedCategory?: string;

    productQuery: QueryRequest;

    productResult?: QueryResponse;
}

export interface QueryResponse {
    totalCount: number,
    offset: number,
    pageSize: number
}
