export function buildProcessRoute(
    processId: string | undefined,
    route: string
): string[] {
    return processId
        ? ['/process', processId, route]
        : ['/'];
}
