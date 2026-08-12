import { Component } from '@angular/core';

import { RouterLink } from '@angular/router';
import { RouterLinkActive } from '@angular/router';
import { RouterOutlet } from '@angular/router';

@Component({
  selector: 'app-ecommerce',
  imports: [ RouterLink, RouterLinkActive, RouterOutlet ],
  templateUrl: './ecommerce.html',
  styleUrl: './ecommerce.scss',
})
export class ECommerce {
}