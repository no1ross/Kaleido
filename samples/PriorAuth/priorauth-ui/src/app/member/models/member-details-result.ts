export interface MemberDetailsResult {
    memberId: string;
    memberEnrollmentId: string;
    memberNumber: string;
    firstName: string;
    lastName: string;
    displayName: string;
    dateOfBirth: string;
    gender: string;
    emailAddress: string;
    phoneNumber: string;
    planId: string;
    planName: string;
    lineOfBusiness: string;
    effectiveDate: string;
    terminationDate?: string;
    relationshipToSubscriber: string;
    issuanceState: string;
    addressLine1: string;
    addressLine2: string;
    city: string;
    addressState: string;
    postalCode: string;
}
