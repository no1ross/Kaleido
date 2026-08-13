import {
  Component,
  EventEmitter,
  Input,
  Output
} from '@angular/core';

import {
  FormsModule
} from '@angular/forms';

import {
  QueryRequest
} from '../models/queryable-request';

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

  @Input({ required: true })
  queryRequest!: QueryRequest;

  @Output()
  querySearchChanged =
    new EventEmitter<QueryRequest>();

  searchText = '';

  apply(): void {

      const queryRequest =
          structuredClone(
              this.queryRequest);

      queryRequest.query ??= {};

      const value =
          this.searchText.trim();

      queryRequest.query.searchText =
          value.length > 0
              ? value
              : undefined;

      if (queryRequest.query.page) {
          queryRequest.query.page.offset = 0;
      }

      this.querySearchChanged.emit(
          queryRequest);
  }

  clear(): void {

      this.searchText = '';

      const queryRequest =
          structuredClone(
              this.queryRequest);

      queryRequest.query ??= {};

      queryRequest.query.searchText =
          undefined;

      if (queryRequest.query.page) {
          queryRequest.query.page.offset = 0;
      }

      this.querySearchChanged.emit(
          queryRequest);
  }

}