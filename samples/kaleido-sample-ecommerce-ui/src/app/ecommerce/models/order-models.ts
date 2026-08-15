export interface OrderReviewView {

    orderId: string;

    customerId: string;

    participantProcessId: string;

    status: string;

    orderItemId: string;

    productId: string;

    productName: string;

    supplierName: string;

    familyName: string;

    modelName: string;

    productSku: string;

    description: string;

    quantity: number;

    unitPrice: number;

    extendedPrice: number;
}

export interface OrderReviewViewParameters {

    participantProcessId?: string;

    customerId?: string;

    orderId?: string;
}

export interface OrderDetailsView {

    orderId: string;

    customerId: string;

    participantProcessId: string;

    orderNumber: string;

    status: string;

    createdUtc: string;

    submittedUtc?: string;

    orderItemId: string;

    productId: string;

    productName: string;

    productSku: string;

    supplierName: string;

    familyName: string;

    modelName: string;

    description: string;

    quantity: number;

    unitPrice: number;

    extendedPrice: number;
}

export interface OrderDetailsViewParameters {

    participantProcessId?: string;

    customerId?: string;

    orderId?: string;
}