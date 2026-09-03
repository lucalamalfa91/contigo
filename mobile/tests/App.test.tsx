import { act, create, type ReactTestInstance, type ReactTestRenderer } from 'react-test-renderer';
import App from '../App';
import { useOidcAuth } from '../src/auth';
import type { OidcAuthState } from '../src/auth';

// App.tsx only owns the sign-in/sign-out shell around useOidcAuth's state
// (expo-auth-session's own request/PKCE/token-exchange plumbing is exercised
// by src/auth/oidcConfig — the pure pieces this codebase wrote — and by the
// library's own test suite, not re-tested here). Mocking useOidcAuth isolates
// exactly that shell logic, mirroring web/tests/App.test.tsx's approach of
// mocking @azure/msal-react.
jest.mock('../src/auth', () => ({
  useOidcAuth: jest.fn(),
}));

const mockUseOidcAuth = useOidcAuth as jest.MockedFunction<typeof useOidcAuth>;

function renderApp(): ReactTestRenderer {
  let renderer: ReactTestRenderer;
  act(() => {
    renderer = create(<App />);
  });
  return renderer!;
}

function state(overrides: Partial<OidcAuthState>): OidcAuthState {
  return {
    isAuthenticated: false,
    isLoading: false,
    tokens: null,
    error: null,
    signIn: jest.fn(),
    signOut: jest.fn(),
    ...overrides,
  };
}

describe('App', () => {
  it('shows a sign-in affordance and no sign-out button when unauthenticated', () => {
    mockUseOidcAuth.mockReturnValue(state({}));

    const root: ReactTestInstance = renderApp().root;

    expect(root.findByProps({ title: 'Sign in' })).toBeTruthy();
    expect(root.findAllByProps({ title: 'Sign out' })).toHaveLength(0);
  });

  it('starts the PKCE sign-in flow when "Sign in" is pressed (AC-1/AC-2)', () => {
    const signIn = jest.fn();
    mockUseOidcAuth.mockReturnValue(state({ signIn }));

    const root = renderApp().root;
    act(() => {
      root.findByProps({ title: 'Sign in' }).props.onPress();
    });

    expect(signIn).toHaveBeenCalledTimes(1);
  });

  it('shows a sign-out affordance and no sign-in button when authenticated', () => {
    mockUseOidcAuth.mockReturnValue(state({ isAuthenticated: true }));

    const root = renderApp().root;

    expect(root.findByProps({ title: 'Sign out' })).toBeTruthy();
    expect(root.findAllByProps({ title: 'Sign in' })).toHaveLength(0);
  });

  it('forgets the session when "Sign out" is pressed', () => {
    const signOut = jest.fn();
    mockUseOidcAuth.mockReturnValue(state({ isAuthenticated: true, signOut }));

    const root = renderApp().root;
    act(() => {
      root.findByProps({ title: 'Sign out' }).props.onPress();
    });

    expect(signOut).toHaveBeenCalledTimes(1);
  });

  it('disables the sign-in button while a request/exchange is in flight', () => {
    mockUseOidcAuth.mockReturnValue(state({ isLoading: true }));

    const root = renderApp().root;

    expect(root.findByProps({ title: 'Sign in' }).props.disabled).toBe(true);
  });

  it('surfaces a sign-in error message', () => {
    mockUseOidcAuth.mockReturnValue(state({ error: 'OIDC sign-in failed: invalid_client' }));

    const root = renderApp().root;

    expect(root.findByProps({ children: 'OIDC sign-in failed: invalid_client' })).toBeTruthy();
  });
});
