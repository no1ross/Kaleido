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

export interface InformationItem {
    id: string;
    text: string;
    type: InformationItemType;
    repeats: boolean;
    options: InformationOption[];
    items: InformationItem[];
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
