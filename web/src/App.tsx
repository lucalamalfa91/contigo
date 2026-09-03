import { useEffect, useState } from "react";
import { AuthenticatedTemplate, UnauthenticatedTemplate, useMsal } from "@azure/msal-react";
import { InteractionStatus } from "@azure/msal-browser";
import type { AppConfig } from "./config/appConfig";
import { buildLoginRequest } from "./auth/msalConfig";
import type { ApiClient } from "./api/client";

interface AppProps {
  appConfig: AppConfig;
  apiClient: ApiClient;
}

// Task E01/F07/US01/T02: mirrors the generated API client's HealthCheckResult
// (src/api/client.ts) but adds the "still in flight" phase a UI needs and the
// wrapper's own result did not: a health check that has not resolved yet is
// not the same thing as an unreachable API.
type HealthState =
  | { phase: "checking" }
  | { phase: "ok"; body: string }
  | { phase: "unreachable"; statusCode: number | null; body: string };

// Minimal shell proving the OIDC Authorization Code + PKCE flow end to end
// (AC-1): sign in redirects to the Entra authority named in runtime config,
// sign out clears the local session. Screens for the actual product surfaces
// (workspace, portfolio, Contract 360, ...) land in later feature tasks.
export default function App({ appConfig, apiClient }: AppProps) {
  const { instance, accounts, inProgress } = useMsal();
  const account = accounts[0];
  const interactionInFlight = inProgress !== InteractionStatus.None;

  // "wire /health" (task E01/F07/US01/T02) and the parent story's Definition
  // of Done ("curl on /health via the API client succeeds"): a static SPA has
  // no shell to literally run curl in, so this effect is the equivalent
  // proof-of-connectivity -- every load of the deployed bundle calls /health
  // through the generated-type-backed client and surfaces the result,
  // independent of sign-in state (a reachability probe, not an authenticated
  // call).
  const [health, setHealth] = useState<HealthState>({ phase: "checking" });
  useEffect(() => {
    let cancelled = false;
    setHealth({ phase: "checking" });
    void apiClient.getHealth().then((result) => {
      if (cancelled) return;
      setHealth(
        result.ok
          ? { phase: "ok", body: result.body }
          : { phase: "unreachable", statusCode: result.statusCode, body: result.body },
      );
    });
    return () => {
      cancelled = true;
    };
  }, [apiClient]);

  const signIn = () => {
    void instance.loginRedirect(buildLoginRequest(appConfig));
  };

  const signOut = () => {
    void instance.logoutRedirect();
  };

  return (
    <main>
      <h1>Contigo</h1>
      <p data-testid="api-health-status">
        {health.phase === "checking" && "API: checking…"}
        {health.phase === "ok" && `API: reachable (${health.body})`}
        {health.phase === "unreachable" &&
          `API: unreachable (${health.statusCode ?? "network error"}: ${health.body})`}
      </p>
      <AuthenticatedTemplate>
        <p>
          Signed in as <strong>{account?.username}</strong>.
        </p>
        <button type="button" onClick={signOut} disabled={interactionInFlight}>
          Sign out
        </button>
      </AuthenticatedTemplate>
      <UnauthenticatedTemplate>
        <p>Sign in with your organization account to continue.</p>
        <button type="button" onClick={signIn} disabled={interactionInFlight}>
          Sign in
        </button>
      </UnauthenticatedTemplate>
    </main>
  );
}
