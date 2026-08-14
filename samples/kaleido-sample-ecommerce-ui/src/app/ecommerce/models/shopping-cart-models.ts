export interface ShoppingCartSummaryView {

    participantProcessId?: string;

    shoppingCartId?: string;

    itemCount: number;

    totalPrice: number;
}

export interface ShoppingCartViewParameters {

    participantProcessId?: string;

    customerId?: string;
}

export interface ShoppingCartDetailView {

    shoppingCartId: string;

    shoppingCartItemId: string;

    productId: string;

    productName: string;

    supplierName: string;

    familyName: string;

    modelName: string;

    description: string;

    quantity: number;

    unitPrice: number;

    extendedPrice: number;
}