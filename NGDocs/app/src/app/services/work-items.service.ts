import { Injectable } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import { WorkItem } from '../models';
import { Observable } from 'rxjs';

@Injectable({ providedIn: 'root' })
export class WorkItemsService {
  constructor(private http: HttpClient) {}

  // via proxy: /data/workItems -> http://localhost:3002/workItems
  list(): Observable<WorkItem[]> {
    return this.http.get<WorkItem[]>('/data/workItems');
  }
}
