import { beforeEach, describe, expect, it, vi } from "vitest";
import { render, screen } from "@testing-library/react";
import userEvent from "@testing-library/user-event";
import { InteractionStatus } from "@azure/msal-browser";
import type { ReactNode } from "react";
import App from "../src/App";
import type { AppConfig } from "../src/config/appConfig";
import type { ApiClient, HealthCheckResult } from "../src/api/client";

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

/** src/api/client.ts (task E01/F07/US01/T02) is exercised by its own tests/api/client.test.ts; here it is a plain mock so App's rendering of the three health phases is isolated. */
function mockApiClient(result: Promise<HealthCheckResult> | HealthCheckResult): ApiClient {
  return { getHealth: vi.fn().mockReturnValue(Promise.resolve(result)) };
}

const healthyClient = () => mockApiClient({ ok: true, statusCode: 200, body: "Healthy" });

describe("App", () => {
  it("shows a sign-in affordance and no account when unauthenticated", () => {
    useMsalMock.mockReturnValue({
      instance: { loginRedirect: vi.fn(), logoutRedirect: vi.fn() },
      accounts: [],
      inProgress: InteractionStatus.None,
    });

    render(<App appConfig={appConfig} apiClient={healthyClient()} />);

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

    render(<App appConfig={appConfig} apiClient={healthyClient()} />);
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

    render(<App appConfig={appConfig} apiClient={healthyClient()} />);

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

    render(<App appConfig={appConfig} apiClient={healthyClient()} />);

    expect(screen.getByRole("button", { name: /sign in/i })).toBeDisabled();
  });

  describe("API health status (task E01/F07/US01/T02, 'wire /health')", () => {
    beforeEach(() => {
      useMsalMock.mockReturnValue({
        instance: { loginRedirect: vi.fn(), logoutRedirect: vi.fn() },
        accounts: [],
        inProgress: InteractionStatus.None,
      });
    });

    it("calls apiClient.getHealth() once on mount", () => {
      const apiClient = healthyClient();
      render(<App appConfig={appConfig} apiClient={apiClient} />);
      expect(apiClient.getHealth).toHaveBeenCalledTimes(1);
    });

    it("renders the reachable status once the health check resolves ok", async () => {
      render(<App appConfig={appConfig} apiClient={healthyClient()} />);

      expect(await screen.findByText(/API: reachable \(Healthy\)/)).toBeInTheDocument();
    });

    it("renders the unreachable status (with status code) when the health check resolves not-ok", async () => {
      const apiClient = mockApiClient({ ok: false, statusCode: 503, body: "Unhealthy" });
      render(<App appConfig={appConfig} apiClient={apiClient} />);

      expect(await screen.findByText(/API: unreachable \(503: Unhealthy\)/)).toBeInTheDocument();
    });

    it("renders the unreachable status (network error) when the health check never got a status code", async () => {
      const apiClient = mockApiClient({
        ok: false,
        statusCode: null,
        body: "Unable to reach https://api.dev.contigo.example/health. Cause: network down",
      });
      render(<App appConfig={appConfig} apiClient={apiClient} />);

      const status = await screen.findByTestId("api-health-status");
      expect(status).toHaveTextContent("API: unreachable (network error:");
      expect(status).toHaveTextContent("network down");
    });
  });
});
