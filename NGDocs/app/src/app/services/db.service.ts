import { Injectable } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import { DbStatus, DbTablesResponse, DbTableResponse } from '../models';
import { Observable } from 'rxjs';

@Injectable({ providedIn: 'root' })
export class DbService {
  constructor(private http: HttpClient) {}

  status(): Observable<DbStatus> {
    return this.http.get<DbStatus>(`/api/db/status`);
  }

  tables(): Observable<DbTablesResponse> {
    return this.http.get<DbTablesResponse>(`/api/db/tables`);
  }

  table(name: string, limit = 50, offset = 0): Observable<DbTableResponse> {
    return this.http.get<DbTableResponse>(
      `/api/db/table/${encodeURIComponent(name)}?limit=${limit}&offset=${offset}`
    );
  }
}
