import {
  Component,
  inject
} from '@angular/core';

import {
  FormsModule
} from '@angular/forms';

import { QueryExecutionStateService } from '../services/query-state-service';

@Component({
  selector: 'queryable-search',
  standalone: true,
  imports: [
    FormsModule
  ],
  templateUrl: './queryable-search.html',
  styleUrl: './queryable-search.scss'
})
export class QueryableSearch {

  private readonly queryState =
    inject(QueryExecutionStateService);

  searchText = '';

  apply(): void {

    this.queryState.state.request.query ??= {};

    const value =
      this.searchText.trim();

    this.queryState.state.request.query.searchText =
      value.length > 0
        ? value
        : undefined;

    if (this.queryState.state.request.query.page) {

      this.queryState.state.request.query.page.offset = 0;
    }

    this.queryState.notifyChanged();
  }

  clear(): void {

    this.searchText = '';

    this.queryState.state.request.query ??= {};

    this.queryState.state.request.query.searchText =
      undefined;

    if (this.queryState.state.request.query.page) {

      this.queryState.state.request.query.page.offset = 0;
    }

    this.queryState.notifyChanged();
  }
}