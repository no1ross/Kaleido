import { Component } from '@angular/core';

import {
    RouterLink,
    RouterLinkActive,
    RouterOutlet
} from '@angular/router';

import { ShoppingCartSummary } from './components/shopping-cart-summary/shopping-cart-summary';

@Component({
    selector: 'app-ecommerce',
    imports: [
        RouterLink,
        RouterLinkActive,
        RouterOutlet,
        ShoppingCartSummary
    ],
    templateUrl: './ecommerce.html',
    styleUrl: './ecommerce.scss'
})
export class ECommerce {

}
