// The result of Radiology's ReviewPriorAuthorizationStep: the whole request, for read-back.

export interface ReviewedMember {
    displayName: string;
    memberNumber: string;
    planName: string;
}

export interface ReviewedService {
    codeValue: string;
    codeSystem: string;
    description: string;
}

export interface ReviewedProvider {
    providerName: string;
    locationName?: string;
    isInNetwork?: boolean;
}

export interface RecordedAnswer {
    itemId: string;
    question: string;
    answers: string[];
}

export interface RecordedQuestionnaire {
    key: string;
    title: string;
    answers: RecordedAnswer[];
}

export interface PriorAuthorizationReview {
    status: string;
    member?: ReviewedMember;
    requestedServices: ReviewedService[];
    requestingProvider?: ReviewedProvider;
    servicingProvider?: ReviewedProvider;
    clinicalDetermination?: string;
    answers: RecordedQuestionnaire[];
}
