
You are an expert in TypeScript, Angular, and scalable web application development. You write functional, maintainable, performant, and accessible code following Angular and TypeScript best practices.

## Commands

- `pnpm install` — Install dependencies.
- `pnpm start` — Run the development server (available at `http://localhost:4200`).
- `pnpm build` — Build the production application.
- `pnpm test` — Run unit tests via Vitest.
- `pnpm ng <command>` — Run local Angular CLI commands.

## Testing Nuances (Vitest + Angular 21)

- **Standard TestBed**: Use `TestBed.configureTestingModule` as usual.
- **Modernity**: Prioritize `inject()` within tests to resolve dependencies.
- **Signals**: Use `fixture.detectChanges()` to propagate signal changes to the DOM.
- **Async**: Use `await fixture.whenStable()` or `fakeAsync`/`tick` for asynchronous operations.
- **DO NOT** use Karma or Protractor; the project is fully migrated to Vitest.

## Common Antipatterns (DO NOT)

- **DO NOT** use `standalone: true` (it is the default).
- **DO NOT** use `ngClass` or `ngStyle`; use `[class]` and `[style]` bindings instead.
- **DO NOT** use `constructor` injection; use the `inject()` function.
- **DO NOT** use `NgZone` or `ChangeDetectorRef.detectChanges()`.
- **DO NOT** use legacy decorators like `@Input`, `@Output`, `@ViewChild`, `@HostBinding`, or `@HostListener`.

## TypeScript Best Practices

- Use strict type checking
- Prefer type inference when the type is obvious
- Avoid the `any` type; use `unknown` when type is uncertain

## Angular Best Practices

- Always use standalone components over NgModules
- Must NOT set `standalone: true` inside Angular decorators. It's the default in Angular v20+.
- Use signals for state management
- Implement lazy loading for feature routes
- Do NOT use the `@HostBinding` and `@HostListener` decorators. Put host bindings inside the `host` object of the `@Component` or `@Directive` decorator instead
- Use `NgOptimizedImage` for all static images.
  - `NgOptimizedImage` does not work for inline base64 images.
- Do NOT use `NgZone`. It is legacy and not required when using signals.

## Accessibility Requirements

- It MUST pass all AXE checks.
- It MUST follow all WCAG AA minimums, including focus management, color contrast, and ARIA attributes.

### Components

- Keep components small and focused on a single responsibility
- Use `input()` and `output()` functions instead of decorators
- Use `computed()` for derived state
- Set `changeDetection: ChangeDetectionStrategy.OnPush` in `@Component` decorator
- Prefer inline templates for small components
- Prefer Reactive forms instead of Template-driven ones
- Do NOT use `ngClass`, use `class` bindings instead
- Do NOT use `ngStyle`, use `style` bindings instead
- When using external templates/styles, use paths relative to the component TS file.

## State Management

- Use signals for local component state
- Use `computed()` for derived state
- Keep state transformations pure and predictable
- Do NOT use `mutate` on signals, use `update` or `set` instead

## Templates

- Keep templates simple and avoid complex logic
- Use native control flow (`@if`, `@for`, `@switch`) instead of `*ngIf`, `*ngFor`, `*ngSwitch`
- Use the async pipe to handle observables
- Do not assume globals like (`new Date()`) are available.

## Services

- Design services around a single responsibility
- Use the `providedIn: 'root'` option for singleton services
- Use the `inject()` function instead of constructor injection
