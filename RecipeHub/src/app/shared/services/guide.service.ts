import { Injectable } from '@angular/core';
import { Subject } from 'rxjs';

@Injectable({ providedIn: 'root' })
export class GuideService {
    private readonly startRequestSubject = new Subject<void>();
    readonly startRequest$ = this.startRequestSubject.asObservable();

    requestStart(): void {
        this.startRequestSubject.next();
    }
}
