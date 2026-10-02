import { Injectable, inject, signal } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import { Observable, tap } from 'rxjs';

import { environment } from '../../environments/environment';

export interface DevLoginResponse {
    token: string;
}

export interface DevPersona {
    name: string;
    displayName: string;
    roles: string[];
}

@Injectable({ providedIn: 'root' })
export class AuthService {
    private readonly http = inject(HttpClient);
    private readonly tokenKey = 'priorauth.dev.token';
    private readonly personaKey = 'priorauth.dev.persona';

    readonly personas: DevPersona[] = [
        { name: 'alice', displayName: 'Alice (Intake)', roles: ['intake', 'staff'] },
        { name: 'bob', displayName: 'Bob (Radiology)', roles: ['radiology', 'staff'] },
        { name: 'carol', displayName: 'Carol (Admin)', roles: ['intake', 'radiology', 'admin', 'staff'] }
    ];

    readonly token = signal<string | null>(
        localStorage.getItem(this.tokenKey));

    readonly persona = signal<string | null>(
        localStorage.getItem(this.personaKey));

    login(name: string): Observable<DevLoginResponse> {
        return this.http
            .post<DevLoginResponse>(
                `${environment.routerBaseUrl}auth/login`,
                { name })
            .pipe(
                tap(response => {
                    localStorage.setItem(this.tokenKey, response.token);
                    localStorage.setItem(this.personaKey, name);
                    this.token.set(response.token);
                    this.persona.set(name);
                }));
    }

    logout(): void {
        localStorage.removeItem(this.tokenKey);
        localStorage.removeItem(this.personaKey);
        this.token.set(null);
        this.persona.set(null);
    }
}
