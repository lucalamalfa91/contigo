import { APP_SCHEME, buildNativeRedirectUri, getNativeRedirectUri } from '../../src/config/redirectUri';

describe('buildNativeRedirectUri', () => {
  it('joins scheme and path with "://"', () => {
    expect(buildNativeRedirectUri('contigo', 'callback')).toBe('contigo://callback');
  });

  it('defaults the path to "callback"', () => {
    expect(buildNativeRedirectUri('contigo')).toBe('contigo://callback');
  });

  it('throws on an empty scheme instead of returning a malformed URI', () => {
    expect(() => buildNativeRedirectUri('')).toThrow(/non-empty app scheme/);
  });
});

describe('getNativeRedirectUri', () => {
  it('reads the scheme from app.json ("expo.scheme")', () => {
    expect(APP_SCHEME).toBe('contigo');
  });

  it('produces "contigo://callback" (parent story us-01 AC-1)', () => {
    expect(getNativeRedirectUri()).toBe('contigo://callback');
  });
});
