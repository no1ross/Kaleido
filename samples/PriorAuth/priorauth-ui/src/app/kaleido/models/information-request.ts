// Kaleido information request contracts (modelled on the FHIR Questionnaire structure).
// The questions are the service's; the UI only renders them verbatim and returns the answers.

export type InformationItemType =
    'text'
    | 'longText'
    | 'boolean'
    | 'wholeNumber'
    | 'number'
    | 'date'
    | 'dateTime'
    | 'choice'
    | 'display'
    | 'group';

export interface InformationOption {
    value: string;
    display?: string;
}

export type InformationConditionOperator =
    'exists'
    | 'equals'
    | 'notEquals'
    | 'greaterThan'
    | 'lessThan'
    | 'greaterThanOrEqual'
    | 'lessThanOrEqual';

/** A condition on an earlier question's answer under which an item applies (FHIR's enableWhen). */
export interface InformationCondition {
    /** The id of the earlier question whose answer is tested. */
    question: string;
    operator: InformationConditionOperator;
    /** The value compared with; for 'exists', 'true' (answered) or 'false' (not answered). */
    answer?: string;
}

export interface InformationItem {
    id: string;
    text: string;
    type: InformationItemType;
    repeats: boolean;
    options: InformationOption[];
    items: InformationItem[];
    /** Answers proposed from what was stated earlier: shown pre-filled, submitted only as the user leaves them. */
    proposedAnswers?: InformationAnswer[];
    /** Empty or absent: always applies. Otherwise only when these conditions hold (all, or any). */
    enableWhen?: InformationCondition[];
    enableBehavior?: 'all' | 'any';
}

export interface InformationRequest {
    informationRequestId: string;
    title?: string;
    items: InformationItem[];
}

export interface InformationAnswer {
    value: string;
}

export interface InformationResponseItem {
    itemId: string;
    answers: InformationAnswer[];
}

/** The payload of an information step: the answers to the pending request. */
export interface InformationResponse {
    informationRequestId: string;
    items: InformationResponseItem[];
}
