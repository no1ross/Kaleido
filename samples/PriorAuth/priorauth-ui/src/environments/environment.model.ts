import { PriorAuthServiceRouteConfig } from '../configuration/urlConfig';

export type PriorAuthApiMode = 'router' | 'direct';

export interface PriorAuthUiEnvironment {
    readonly apiMode: PriorAuthApiMode;
    readonly routerBaseUrl: string;
    readonly serviceRoutes: readonly PriorAuthServiceRouteConfig[];
}
