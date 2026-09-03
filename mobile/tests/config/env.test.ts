import { getMobileEnvConfig } from '../../src/config/env';

const validSource = {
  EXPO_PUBLIC_API_BASE_URL: 'https://api.dev.contigo.example',
  EXPO_PUBLIC_OIDC_AUTHORITY: 'https://login.microsoftonline.com/00000000-0000-0000-0000-000000000000/v2.0',
  EXPO_PUBLIC_OIDC_CLIENT_ID: 'dev-public-client-id',
};

describe('getMobileEnvConfig', () => {
  it('reads apiBaseUrl, oidcAuthority and oidcClientId from the given env source', () => {
    expect(getMobileEnvConfig(validSource)).toEqual({
      apiBaseUrl: validSource.EXPO_PUBLIC_API_BASE_URL,
      oidcAuthority: validSource.EXPO_PUBLIC_OIDC_AUTHORITY,
      oidcClientId: validSource.EXPO_PUBLIC_OIDC_CLIENT_ID,
    });
  });

  it.each(Object.keys(validSource))(
    'fails fast when %s is missing, instead of silently pointing at the wrong environment',
    (missingKey) => {
      const partialSource = { ...validSource, [missingKey]: undefined };
      expect(() => getMobileEnvConfig(partialSource)).toThrow(new RegExp(missingKey));
    }
  );

  it('never falls back to a hardcoded default (no secrets/URLs baked into the bundle)', () => {
    expect(() => getMobileEnvConfig({})).toThrow(/Missing required environment variable/);
  });
});
