export interface ValidationResult {

    isValid: boolean;

    messages: ValidationMessage[];
}

export interface ValidationMessage {

    field: string;

    message: string;
}