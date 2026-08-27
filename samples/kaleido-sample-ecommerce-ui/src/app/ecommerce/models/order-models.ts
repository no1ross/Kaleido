export interface OrderReviewView {

    orderId: string;

    customerId: string;

    processId: string;

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

    processId?: string;

    customerId?: string;

    orderId?: string;
}

export interface OrderDetailsView {

    orderId: string;

    customerId: string;

    processId: string;

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

    processId?: string;

    customerId?: string;

    orderId?: string;
}