# PriorauthUi

The Angular UI of the PriorAuth sample. It drives the Intake and Radiology processes and the
reference searches over the services' Kaleido HTTP endpoints; see
[`../AGENTS.md`](../AGENTS.md) for the services, ports, personas and the cross-processor handoff the
UI follows (`ProcessService.executeStep()`).

- **Backend first.** The UI only renders what the services return: run the services (or the compose
  file under `../Compose/`) before `ng serve`.
- **How it reaches them** is set in `src/environments/`: `environment.ts` calls each service directly
  on its local port (`apiMode: 'direct'`), `environment.container.ts` goes through the router
  (`apiMode: 'router'`), as in compose. `environment.local.ts` and `environment.hybrid.ts` currently
  re-export `environment.ts`.
- **Sign in** with a dev persona (`alice`, `bob`, `carol`) at the router's `/auth/login`.

The rest of this file is the Angular CLI's standard guide.

This project was generated using [Angular CLI](https://github.com/angular/angular-cli) version 21.2.21.

## Development server

To start a local development server, run:

```bash
ng serve
```

Once the server is running, open your browser and navigate to `http://localhost:4200/`. The application will automatically reload whenever you modify any of the source files.

## Code scaffolding

Angular CLI includes powerful code scaffolding tools. To generate a new component, run:

```bash
ng generate component component-name
```

For a complete list of available schematics (such as `components`, `directives`, or `pipes`), run:

```bash
ng generate --help
```

## Building

To build the project run:

```bash
ng build
```

This will compile your project and store the build artifacts in the `dist/` directory. By default, the production build optimizes your application for performance and speed.

## Running unit tests

To execute unit tests with the [Vitest](https://vitest.dev/) test runner, use the following command:

```bash
ng test
```

## Running end-to-end tests

For end-to-end (e2e) testing, run:

```bash
ng e2e
```

Angular CLI does not come with an end-to-end testing framework by default. You can choose one that suits your needs.

## Additional Resources

For more information on using the Angular CLI, including detailed command references, visit the [Angular CLI Overview and Command Reference](https://angular.dev/tools/cli) page.
