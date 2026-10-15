import { Injectable, inject, signal } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import { Observable, tap } from 'rxjs';

import { buildRouterUrl } from '../../configuration/urlConfig';

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
        { name: 'alice', displayName: 'Alice (no domain role)', roles: [] },
        { name: 'bob', displayName: 'Bob (Radiology)', roles: ['radiology'] },
        { name: 'carol', displayName: 'Carol (Admin)', roles: ['admin', 'radiology'] }
    ];

    readonly token = signal<string | null>(null);

    readonly persona = signal<string | null>(null);

    constructor() {
        // Restore the session only when the token and its persona are both there and the token
        // has not expired; otherwise the picker would say Anonymous while a token is still sent.
        const token = localStorage.getItem(this.tokenKey);
        const persona = localStorage.getItem(this.personaKey);

        if (token && persona && !this.isExpired(token)) {
            this.token.set(token);
            this.persona.set(persona);
        } else {
            this.logout();
        }
    }

    login(name: string): Observable<DevLoginResponse> {
        return this.http
            .post<DevLoginResponse>(
                buildRouterUrl('auth/login'),
                { name })
            .pipe(
                tap(response => {
                    localStorage.setItem(this.tokenKey, response.token);
                    localStorage.setItem(this.personaKey, name);
                    this.token.set(response.token);
                    this.persona.set(name);
                }));
    }

    // Dev token: dev.<base64url(name|roles|actor|expiryUnixSeconds)>.<signature>
    private isExpired(token: string): boolean {
        try {
            const payload = atob(token.split('.')[1].replace(/-/g, '+').replace(/_/g, '/'));
            const expiry = Number(payload.split('|')[3]);
            return !Number.isFinite(expiry) || expiry * 1000 <= Date.now();
        } catch {
            return true;
        }
    }

    logout(): void {
        localStorage.removeItem(this.tokenKey);
        localStorage.removeItem(this.personaKey);
        this.token.set(null);
        this.persona.set(null);
    }
}
