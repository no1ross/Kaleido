import { PriorAuthServiceRouteConfig } from '../../../configuration/urlConfig';

export interface ProcessProcessorRegistryRecord {
    name: string;
    version: string;
    displayName: string;
    description: string | null;
    registryUrl: string;
    initialSteps: ProcessStepSummary[];
    steps: ProcessStepRegistryRecord[];
}

export interface ProcessStepRegistryRecord {
    name: string;
    version: string;
    displayName: string;
    description: string | null;
    repeatable: boolean;

    fields: ProcessStepFieldMetadata[];

    dependencies: ProcessStepSummary[];
    availableAfter: ProcessStepSummary[];
    availableUntil: ProcessStepSummary[];

    result: ProcessStepResultMetadata | null;

    executeUrl: string;
    metadataUrl: string;
}

export interface ServiceProcessProcessorRegistryRecord {
    service: PriorAuthServiceRouteConfig;
    processor: ProcessProcessorRegistryRecord;
}

export interface ServiceProcessStepRegistryRecord {
    service: PriorAuthServiceRouteConfig;
    processor: ProcessProcessorRegistryRecord;
    step: ProcessStepRegistryRecord;
}

export interface ProcessStepSummary {
    name: string;
    version: string;
    displayName: string;
    description: string | null;
    repeatable: boolean;
    executeUrl: string;
    metadataUrl: string;
}

export interface ProcessStepFieldMetadata {
    name: string;
    description?: string | null;
    dataType: ProcessDataTypeMetadata;
    constraints: ProcessFieldConstraintMetadata[];
}

export interface ProcessStepResultMetadata {
    outputFields: ProcessOutputFieldMetadata[];
}

export interface ProcessOutputFieldMetadata {
    name: string;
    description: string | null;
    dataType: ProcessDataTypeMetadata;
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