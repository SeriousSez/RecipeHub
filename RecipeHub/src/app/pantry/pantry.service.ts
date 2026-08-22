import { HttpClient, HttpHeaders } from '@angular/common/http';
import { Injectable } from '@angular/core';
import { Observable } from 'rxjs';
import { ConfigService } from '../shared/utils/config.service';
import { PantryItem } from './pantry-item.interface';

@Injectable({ providedIn: 'root' })
export class PantryService {
    private readonly baseUrl: string;

    constructor(private http: HttpClient, configService: ConfigService) {
        this.baseUrl = configService.getApiURI();
    }

    public getItems(userId: string): Observable<PantryItem[]> {
        return this.http.get<PantryItem[]>(`${this.baseUrl}/account/getpantry?userId=${encodeURIComponent(userId)}`, this.httpOptions);
    }

    public updateItems(userId: string, items: PantryItem[]): Observable<PantryItem[]> {
        return this.http.post<PantryItem[]>(`${this.baseUrl}/account/updatepantry`, { userId, items }, this.httpOptions);
    }

    private get httpOptions(): { headers: HttpHeaders } {
        return { headers: new HttpHeaders({ 'Content-Type': 'application/json', 'Authorization': `Bearer ${localStorage.getItem('authToken') ?? ''}` }) };
    }
}