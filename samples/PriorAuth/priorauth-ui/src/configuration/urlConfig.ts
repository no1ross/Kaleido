import { environment } from '../environments/environment';

export interface PriorAuthServiceRouteConfig {
    readonly key: string;
    readonly baseUrl: string;
    // Unified registry path (GET /{prefix}/registry). Only meaningful on the
    // router entry — a single request returns both processes and queryables.
    readonly registryPath?: string;
}

export function getRouterBaseUrl(): string {
    return environment.routerBaseUrl;
}

export function getServiceRoutes(): readonly PriorAuthServiceRouteConfig[] {
    return environment.serviceRoutes;
}

export function buildRegistryUrl(path: string): string {
    return buildUrl(environment.routerBaseUrl, path);
}

export function buildServiceUrl(
    service: PriorAuthServiceRouteConfig,
    path: string
): string {
    if (environment.apiMode === 'router') {
        return buildUrl(
            environment.routerBaseUrl,
            path);
    }

    return buildUrl(
        service.baseUrl,
        path);
}

function buildUrl(
    baseUrl: string,
    path: string
): string {
    const normalizedBaseUrl =
        baseUrl.replace(/\/+$/, '');

    const relativePath =
        path.replace(/^\/+/, '');

    if (!normalizedBaseUrl) {
        return `/${relativePath}`;
    }

    return `${normalizedBaseUrl}/${relativePath}`;
}
