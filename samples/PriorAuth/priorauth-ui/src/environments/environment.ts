import { createServiceRoutes } from '../configuration/serviceRoutes';
import { PriorAuthUiEnvironment } from './environment.model';

export const environment: PriorAuthUiEnvironment = {
    apiMode: 'direct',
    routerBaseUrl: '/',
    serviceRoutes: createServiceRoutes({
        member: {
            baseUrl: 'http://localhost:8084'
        },
        referenceData: {
            baseUrl: 'http://localhost:8081'
        },
        provider: {
            baseUrl: 'http://localhost:8083'
        },
        codeSet: {
            baseUrl: 'http://localhost:8082'
        },
        configuration: {
            baseUrl: 'http://localhost:8087'
        },
        intake: {
            baseUrl: 'http://localhost:8085'
        }
    })
};
