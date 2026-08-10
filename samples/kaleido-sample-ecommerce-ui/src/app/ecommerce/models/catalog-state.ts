import { QueryRequest } from "../../kaleido/models/queryable-request";

export interface CatalogState {

    selectedCategoryPath?: string;

    productQuery: QueryRequest;
}