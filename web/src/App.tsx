import { AuthenticatedTemplate, UnauthenticatedTemplate, useMsal } from "@azure/msal-react";
import { InteractionStatus } from "@azure/msal-browser";
import type { AppConfig } from "./config/appConfig";
import { buildLoginRequest } from "./auth/msalConfig";

interface AppProps {
  appConfig: AppConfig;
}

// Minimal shell proving the OIDC Authorization Code + PKCE flow end to end
// (AC-1): sign in redirects to the Entra authority named in runtime config,
// sign out clears the local session. Screens for the actual product surfaces
// (workspace, portfolio, Contract 360, ...) land in later feature tasks.
export default function App({ appConfig }: AppProps) {
  const { instance, accounts, inProgress } = useMsal();
  const account = accounts[0];
  const interactionInFlight = inProgress !== InteractionStatus.None;

  const signIn = () => {
    void instance.loginRedirect(buildLoginRequest(appConfig));
  };

  const signOut = () => {
    void instance.logoutRedirect();
  };

  return (
    <main>
      <h1>Contigo</h1>
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
