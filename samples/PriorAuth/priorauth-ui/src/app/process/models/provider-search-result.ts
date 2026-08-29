export interface ProviderSearchResult {
    providerLocationId: string;
    providerId: string;
    providerName: string;
    locationName: string;
    city: string;
    stateCode: string;
    postalCode: string;
    phoneNumber?: string;
    primaryTin?: string;
    primaryNpi?: string;
    primaryMedicalSpecialtyId?: string;
    primaryMedicalSpecialtyName?: string;
    primaryMedicalSpecialtyCode?: string;
    isInNetwork?: boolean;
}
