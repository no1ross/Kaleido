export interface QuestionnaireDefinition {
    questionnaireId: string;
    version: string;
    name: string;
    title: string;
    description?: string;
    items: QuestionnaireItem[];
}

export interface QuestionnaireItem {
    linkId: string;
    text: string;
    type: string;
    bindingKey: string;
    required: boolean;
    repeats: boolean;
    defaultValue?: string;
    order: number;
    answerOptions: QuestionnaireAnswerOption[];
    enableWhen: QuestionnaireEnableWhen[];
}

export interface QuestionnaireAnswerOption {
    value: string;
    displayText: string;
    order: number;
}

export interface QuestionnaireEnableWhen {
    questionBindingKey: string;
    operator: string;
    answerValue: string;
}

export interface CaptureRequestedServiceResponse {
    questionnaireId?: string;
    questionnaireVersion?: string;
    questionnaire?: QuestionnaireDefinition;
}
