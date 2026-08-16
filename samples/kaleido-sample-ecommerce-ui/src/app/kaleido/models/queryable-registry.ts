export interface QueryableRecord {
    name: string;
    description: string;
    displayName: string;
    version: string;
    source: string;

    fields: QueryableField[];

    views: QueryableView[];
}

export interface QueryableField {
    name: string;

    dataType: QueryableDataType;

    isFilterable: boolean;
    filterOperators: string[];

    isSearchable: boolean;
    searchPriority: number | null;
    matchMode: string | null;

    isSortable: boolean;
}

export interface QueryableView {
    name: string;
    description: string;
    displayName: string | null;

    pageable: QueryablePagingMetadata | null;

    queryUrl: string;

    parameters: QueryableParameter[];

    fields: QueryableViewField[];
}

export interface QueryableParameter {
    name: string;

    dataType: QueryableDataType;

    constraints: QueryableConstraint[];
}

export interface QueryableViewField {
    name: string;

    dataType: QueryableDataType;
}

export interface QueryablePagingMetadata {
    defaultSize: number;
    maxSize: number;
}

export interface QueryableDataType {
    type: string;

    format: string | null;

    nullable: boolean;

    enumValues: QueryableEnumValue[] | null;

    itemType: QueryableDataType | null;
}

export interface QueryableEnumValue {
    value: string | number;

    name: string;

    description: string | null;
}

export interface QueryableConstraint {
    type: string;

    parameters: QueryableConstraintParameter[];
}

export interface QueryableConstraintParameter {
    name: string;
    value: unknown;
}

export interface QueryableViewRegistration {

    context: QueryableRecord;

    view: QueryableView;
}