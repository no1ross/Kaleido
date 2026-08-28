import { createServiceRoutes } from '../configuration/serviceRoutes';
import { PriorAuthUiEnvironment } from './environment.model';

export const environment: PriorAuthUiEnvironment = {
    apiMode: 'router',
    routerBaseUrl: '/',
    serviceRoutes: createServiceRoutes({
        member: {
            baseUrl: '/'
        },
        referenceData: {
            baseUrl: '/'
        },
        provider: {
            baseUrl: '/'
        },
        codeSet: {
            baseUrl: '/'
        },
        configuration: {
            baseUrl: '/'
        },
        intake: {
            baseUrl: '/'
        }
    })
};
