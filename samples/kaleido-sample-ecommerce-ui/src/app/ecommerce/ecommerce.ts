import { Component } from '@angular/core';

import { RouterOutlet } from '@angular/router';

import { NavBar } from './components/nav-bar/nav-bar';

@Component({
    selector: 'app-ecommerce',
    imports: [
        RouterOutlet,
        NavBar
    ],
    templateUrl: './ecommerce.html',
    styleUrl: './ecommerce.scss'
})
export class ECommerce {

}
