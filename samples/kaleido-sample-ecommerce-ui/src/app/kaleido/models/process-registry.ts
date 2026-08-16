export interface ProcessStepRegistryRecord {
    name: string;
    version: string;
    displayName: string;
    description: string;
    repeatable: boolean;

    fields: ProcessStepFieldMetadata[];

    dependencies: ProcessStepSummary[];
    availableAfter: ProcessStepSummary[];
    availableUntil: ProcessStepSummary[];

    executeUrl: string;
    metadataUrl: string;
}

export interface ProcessStepSummary {
    name: string;
    version: string;
    displayName: string;
    description: string;
    repeatable: boolean;
    executeUrl: string;
    metadataUrl: string;
}

export interface ProcessStepFieldMetadata {
    name: string;
    dataType: ProcessDataTypeMetadata;
    constraints: ProcessFieldConstraintMetadata[];
}

export interface ProcessDataTypeMetadata {
    type: string;
    format: string | null;
    nullable: boolean;
    enumValues: ProcessEnumValueMetadata[] | null;
    itemType: ProcessDataTypeMetadata | null;
}

export interface ProcessEnumValueMetadata {
    value: number | string;
    name: string;
    description: string | null;
}

export interface ProcessFieldConstraintMetadata {
    type: string;
    parameters: ProcessConstraintParameterMetadata[];
}

export interface ProcessConstraintParameterMetadata {
    name: string;
    value: unknown;
}