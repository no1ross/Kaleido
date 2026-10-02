
const urlConfig = {
    kaleidoApiUrl: 'https://localhost:7251',
    registryPath: '/kaleido/registry',
    processStepPath: 'kaleido/processes/steps/${stepName}',
    queryableQueryPath: 'kaleido/queryable/${context}/${view}/query'
} as const;

export function buildApiUrl(
    path: string
): string {
    const baseUrl =
        urlConfig.kaleidoApiUrl.replace(/\/+$/, '');

    const relativePath =
        path.replace(/^\/+/, '');

    return `${baseUrl}/${relativePath}`;
}

export function getRegistryUrl(
): string {
    return buildApiUrl(urlConfig.registryPath);
}

export function getProcessStepUrl(
    stepName: string
): string {

    return buildApiUrl(formatTemplate(urlConfig.processStepPath, {stepName}));
}

export function getQueryableQueryUrl(
    context: string,
    view: string
): string {
    return buildApiUrl(formatTemplate(urlConfig.queryableQueryPath, {context, view}));
}

function formatTemplate(
    template: string,
    values: Record<string, string>
): string {
    return template.replace(
        /\$\{([^}]+)\}/g,
        (_, key) => values[key] ?? ''
    );
}