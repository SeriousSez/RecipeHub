import { HttpClient, HttpHeaders } from '@angular/common/http';
import { Injectable } from '@angular/core';
import { Observable } from 'rxjs';
import { ConfigService } from '../shared/utils/config.service';
import { FoodPlanEntry, FoodPlanEntryRequest } from './food-plan-entry.interface';

@Injectable()
export class FoodPlanService {
    private baseUrl: string;

    private get httpOptions() {
        const authToken = localStorage.getItem('authToken');
        return authToken ? { headers: new HttpHeaders({ Authorization: `Bearer ${authToken}` }) } : {};
    }

    constructor(private http: HttpClient, configService: ConfigService) {
        this.baseUrl = configService.getApiURI();
    }

    getEntries(userId: string, start: string, end: string): Observable<FoodPlanEntry[]> {
        return this.http.get<FoodPlanEntry[]>(this.baseUrl + `/foodplan?userId=${encodeURIComponent(userId)}&start=${encodeURIComponent(start)}&end=${encodeURIComponent(end)}`, this.httpOptions);
    }

    create(entry: FoodPlanEntryRequest): Observable<FoodPlanEntry> {
        return this.http.post<FoodPlanEntry>(this.baseUrl + '/foodplan', entry, this.httpOptions);
    }

    update(entry: FoodPlanEntryRequest): Observable<FoodPlanEntry> {
        return this.http.put<FoodPlanEntry>(this.baseUrl + `/foodplan/${encodeURIComponent(entry.id ?? '')}`, entry, this.httpOptions);
    }

    delete(id: string, userId: string): Observable<void> {
        return this.http.delete<void>(this.baseUrl + `/foodplan/${encodeURIComponent(id)}?userId=${encodeURIComponent(userId)}`, this.httpOptions);
    }
}