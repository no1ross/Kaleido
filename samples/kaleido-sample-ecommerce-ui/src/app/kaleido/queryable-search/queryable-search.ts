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
  queryRequestChanged =
    new EventEmitter<QueryRequest>();

  searchText = '';

  apply(): void {

    const value =
      this.searchText.trim();

    this.queryRequest.query.searchText =
      value.length > 0
        ? value
        : undefined;

    this.emitChange();
  }

  clear(): void {

    this.searchText = '';

    this.queryRequest.query.searchText =
      undefined;

    this.emitChange();
  }

  private emitChange(): void {

    this.queryRequestChanged.emit(
      structuredClone(
        this.queryRequest));
  }
}