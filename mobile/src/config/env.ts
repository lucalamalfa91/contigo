/**
 * Per-environment, non-secret mobile config (ADR-013: "Mobile reads API base
 * URL + OIDC authority from per-environment config, exactly as `web/` does;
 * no client secret is ever placed in the app/bundle").
 *
 * Values are supplied at build time via `EXPO_PUBLIC_*` environment
 * variables, which Expo/Metro inlines into the JS bundle for both native and
 * web targets (no native module, no `app.config.ts` + `Constants` indirection
 * needed). See `.env.example` for the variables each environment must set.
 *
 * A public OIDC client ID is not a secret (ADR-010: PKCE public clients carry
 * no client secret), so it is safe to read the same way as the API base URL.
 */
export interface MobileEnvConfig {
  apiBaseUrl: string;
  oidcAuthority: string;
  oidcClientId: string;
}

type RequiredKey = 'EXPO_PUBLIC_API_BASE_URL' | 'EXPO_PUBLIC_OIDC_AUTHORITY' | 'EXPO_PUBLIC_OIDC_CLIENT_ID';

// An index signature (not named optional properties) so that
// `NodeJS.ProcessEnv` — itself index-signature-only — is directly
// assignable as the default parameter value below.
type EnvSource = Record<string, string | undefined>;

function readRequiredEnvVar(name: RequiredKey, source: EnvSource): string {
  const value = source[name];
  if (!value) {
    throw new Error(
      `Missing required environment variable: ${name}. Set it via per-environment ` +
        'EXPO_PUBLIC_* build-time config (see mobile/.env.example); there is no ' +
        'hardcoded default so a missing value fails fast instead of silently ' +
        'pointing the app at the wrong environment.'
    );
  }
  return value;
}

/**
 * Reads the mobile app's per-environment config. Defaults to `process.env`
 * (populated by Metro/Expo from `EXPO_PUBLIC_*` vars at build time); a
 * `source` can be injected for testing without mutating global state.
 */
export function getMobileEnvConfig(source: EnvSource = process.env): MobileEnvConfig {
  return {
    apiBaseUrl: readRequiredEnvVar('EXPO_PUBLIC_API_BASE_URL', source),
    oidcAuthority: readRequiredEnvVar('EXPO_PUBLIC_OIDC_AUTHORITY', source),
    oidcClientId: readRequiredEnvVar('EXPO_PUBLIC_OIDC_CLIENT_ID', source),
  };
}
