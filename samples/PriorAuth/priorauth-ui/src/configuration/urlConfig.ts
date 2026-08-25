export interface PriorAuthServiceRouteConfig {
    readonly key: string;
    readonly displayName: string;
    readonly baseUrl: string;
    readonly processRegistryPath?: string;
    readonly queryableRegistryPath?: string;
}

export type PriorAuthApiMode = 'router' | 'direct';

const apiMode: PriorAuthApiMode = 'direct';
const routerBaseUrl = '/';

const serviceRoutes = [
    {
        key: 'member',
        displayName: 'Member Service',
        //baseUrl: 'https://localhost:7015',
        baseUrl: 'http://localhost:8084',
        processRegistryPath: '/member/processes/registry',
        queryableRegistryPath: '/member/queryable/registry'
    },
    {
        key: 'reference-data',
        displayName: 'Reference Data',
        baseUrl: 'http://localhost:8081',
        queryableRegistryPath: '/reference-data/queryable/registry'
    },
    {
        key: 'provider',
        displayName: 'Provider Search',
        baseUrl: 'http://localhost:8083',
        queryableRegistryPath: '/provider/queryable/registry'
    },
    {
        key: 'code-set',
        displayName: 'Code Set',
        baseUrl: 'http://localhost:8082',
        queryableRegistryPath: '/code-set/queryable/registry'
    },
    {
        key: 'intake',
        displayName: 'Intake',
        baseUrl: 'http://localhost:8085'
    }
] as const satisfies readonly PriorAuthServiceRouteConfig[];

export function getApiMode(): PriorAuthApiMode {
    return apiMode;
}

export function getRouterBaseUrl(): string {
    return routerBaseUrl;
}

export function getServiceRoutes(): readonly PriorAuthServiceRouteConfig[] {
    return serviceRoutes;
}

export function buildRegistryUrl(
    service: PriorAuthServiceRouteConfig,
    path: string
): string {
    if (apiMode === 'direct') {
        return buildUrl(
            service.baseUrl,
            path);
    }

    return buildUrl(
        routerBaseUrl,
        path);
}

export function buildServiceUrl(
    service: PriorAuthServiceRouteConfig,
    path: string
): string {
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
