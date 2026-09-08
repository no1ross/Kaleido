import { environment } from '../environments/environment';
import { PriorAuthApiMode } from '../environments/environment.model';

export interface PriorAuthServiceRouteConfig {
    readonly key: string;
    readonly displayName: string;
    readonly baseUrl: string;
    // Unified registry path (GET /{prefix}/registry). When set, a single request
    // returns both processes and queryables. Takes precedence over the two
    // individual paths below.
    readonly registryPath?: string;
    readonly processRegistryPath?: string;
    readonly queryableRegistryPath?: string;
    // Marks the processor that owns the application entry step (e.g. StartIntake).
    // The UI sets currentProcessorName from this on registry load.
    readonly isEntryProcessor?: boolean;
}

export function getApiMode(): PriorAuthApiMode {
    return environment.apiMode;
}

export function getRouterBaseUrl(): string {
    return environment.routerBaseUrl;
}

export function getServiceRoutes(): readonly PriorAuthServiceRouteConfig[] {
    return environment.serviceRoutes;
}

export function buildRegistryUrl(
    service: PriorAuthServiceRouteConfig,
    path: string
): string {
    if (environment.apiMode === 'direct') {
        return buildUrl(
            service.baseUrl,
            path);
    }

    return buildUrl(
        environment.routerBaseUrl,
        path);
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
