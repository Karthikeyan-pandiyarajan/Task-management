import { HttpClient, HttpParams } from '@angular/common/http';
import { Injectable, inject } from '@angular/core';
import { Observable } from 'rxjs';
import { SaveTaskRequest, Task, TaskStatus } from '../../core/models/task.models';

const BASE_URL = '/api/tasks';

/** Thin typed wrapper over the Tasks API; the user id is never sent, the API takes it from the token. */
@Injectable({ providedIn: 'root' })
export class TaskApiService {
  private readonly http = inject(HttpClient);

  list(status?: TaskStatus): Observable<Task[]> {
    const params = status ? new HttpParams().set('status', status) : undefined;
    return this.http.get<Task[]>(BASE_URL, { params });
  }

  get(id: string): Observable<Task> {
    return this.http.get<Task>(`${BASE_URL}/${encodeURIComponent(id)}`);
  }

  create(request: SaveTaskRequest): Observable<Task> {
    return this.http.post<Task>(BASE_URL, request);
  }

  update(id: string, request: SaveTaskRequest): Observable<Task> {
    return this.http.put<Task>(`${BASE_URL}/${encodeURIComponent(id)}`, request);
  }

  delete(id: string): Observable<void> {
    return this.http.delete<void>(`${BASE_URL}/${encodeURIComponent(id)}`);
  }
}
