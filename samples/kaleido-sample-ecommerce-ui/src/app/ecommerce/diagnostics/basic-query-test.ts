import { CurrencyPipe } from '@angular/common';
import { Component, OnInit, inject, signal } from '@angular/core';

import { ProductCatalogView } from '../models/product-catalog-view';
import { QueryRequest } from '../../kaleido/models/queryable-request';
import { QueryableService } from '../../kaleido/services/queryable-service';

@Component({
    selector: 'ecommerce-basic-query-test',
    standalone: true,
    imports: [
        CurrencyPipe
    ],
    templateUrl: './basic-query-test.html'
})
export class BasicQueryTest implements OnInit {
    private readonly queryableService =
        inject(QueryableService);

    readonly products =
        signal<ProductCatalogView[]>([]);

    readonly totalCount =
        signal(0);

    readonly isLoading =
        signal(false);

    readonly errorMessage =
        signal<string | undefined>(undefined);

    ngOnInit(): void {
        this.loadProducts();
    }

    private loadProducts(): void {
        this.isLoading.set(true);
        this.errorMessage.set(undefined);

        const request: QueryRequest = {
            query: {
                page: {
                    offset: 0,
                    size: 25
                }
            }
        };

        this.queryableService
            .query<ProductCatalogView>(
                'product-list',
                request)
            .subscribe({
                next: result => {
                    this.products.set(result.results);
                    this.totalCount.set(result.totalCount);
                    this.isLoading.set(false);
                },
                error: error => {
                    console.error(error);
                    this.errorMessage.set('Failed to load products.');
                    this.isLoading.set(false);
                }
            });
    }
}