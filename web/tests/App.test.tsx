import { describe, expect, it, vi } from "vitest";
import { render, screen } from "@testing-library/react";
import userEvent from "@testing-library/user-event";
import { InteractionStatus } from "@azure/msal-browser";
import type { ReactNode } from "react";
import App from "../src/App";
import type { AppConfig } from "../src/config/appConfig";

// App.tsx only owns the sign-in/sign-out shell around @azure/msal-react's
// hooks and templates (MSAL's own redirect/PKCE plumbing is exercised by the
// library's own test suite, not re-tested here). Mocking useMsal + the two
// templates isolates exactly that shell logic.
const useMsalMock = vi.fn();

vi.mock("@azure/msal-react", () => ({
  useMsal: () => useMsalMock(),
  AuthenticatedTemplate: ({ children }: { children: ReactNode }) =>
    useMsalMock().accounts.length > 0 ? children : null,
  UnauthenticatedTemplate: ({ children }: { children: ReactNode }) =>
    useMsalMock().accounts.length === 0 ? children : null,
}));

const appConfig: AppConfig = {
  apiBaseUrl: "https://api.dev.contigo.example",
  oidcAuthority: "https://login.microsoftonline.com/00000000-0000-0000-0000-000000000000",
  oidcClientId: "11111111-1111-1111-1111-111111111111",
  oidcRedirectUri: "https://web.dev.contigo.example",
  oidcApiScopes: ["api://11111111-1111-1111-1111-111111111111/Contigo.Read"],
};

describe("App", () => {
  it("shows a sign-in affordance and no account when unauthenticated", () => {
    useMsalMock.mockReturnValue({
      instance: { loginRedirect: vi.fn(), logoutRedirect: vi.fn() },
      accounts: [],
      inProgress: InteractionStatus.None,
    });

    render(<App appConfig={appConfig} />);

    expect(screen.getByRole("button", { name: /sign in/i })).toBeInTheDocument();
    expect(screen.queryByRole("button", { name: /sign out/i })).not.toBeInTheDocument();
  });

  it("starts the redirect flow with the configured scopes when Sign in is clicked", async () => {
    const loginRedirect = vi.fn();
    useMsalMock.mockReturnValue({
      instance: { loginRedirect, logoutRedirect: vi.fn() },
      accounts: [],
      inProgress: InteractionStatus.None,
    });

    render(<App appConfig={appConfig} />);
    await userEvent.click(screen.getByRole("button", { name: /sign in/i }));

    expect(loginRedirect).toHaveBeenCalledWith(
      expect.objectContaining({ scopes: appConfig.oidcApiScopes }),
    );
  });

  it("shows the signed-in account and a sign-out affordance when authenticated", () => {
    useMsalMock.mockReturnValue({
      instance: { loginRedirect: vi.fn(), logoutRedirect: vi.fn() },
      accounts: [{ username: "user@example.test" }],
      inProgress: InteractionStatus.None,
    });

    render(<App appConfig={appConfig} />);

    expect(screen.getByText(/user@example\.test/)).toBeInTheDocument();
    expect(screen.getByRole("button", { name: /sign out/i })).toBeInTheDocument();
    expect(screen.queryByRole("button", { name: /sign in/i })).not.toBeInTheDocument();
  });

  it("disables the sign-in button while an interaction is already in flight", () => {
    useMsalMock.mockReturnValue({
      instance: { loginRedirect: vi.fn(), logoutRedirect: vi.fn() },
      accounts: [],
      inProgress: InteractionStatus.AcquireToken,
    });

    render(<App appConfig={appConfig} />);

    expect(screen.getByRole("button", { name: /sign in/i })).toBeDisabled();
  });
});
