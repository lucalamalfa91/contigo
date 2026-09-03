import { StrictMode } from "react";
import { createRoot } from "react-dom/client";
import { PublicClientApplication } from "@azure/msal-browser";
import { MsalProvider } from "@azure/msal-react";
import App from "./App";
import { AppConfigError, loadAppConfig } from "./config/appConfig";
import { buildMsalConfig } from "./auth/msalConfig";
import "./index.css";

const rootElement = document.getElementById("root");
if (!rootElement) {
  throw new Error("#root element not found in index.html");
}
const root = createRoot(rootElement);

// Config must resolve before MSAL can be constructed (ADR-012: client id,
// authority and redirect URI are runtime config, not source). MsalProvider
// (@azure/msal-react) owns calling `instance.initialize()` and
// `instance.handleRedirectPromise()` itself once mounted -- this bootstrap
// only needs to hand it an already-configured, un-initialized
// PublicClientApplication.
async function bootstrap() {
  let appConfig;
  try {
    appConfig = await loadAppConfig();
  } catch (error) {
    const message =
      error instanceof AppConfigError
        ? error.message
        : "Unexpected startup error while loading runtime config. See console for details.";
    // eslint-disable-next-line no-console
    console.error("Contigo web client failed to start:", error);
    root.render(
      <StrictMode>
        <div role="alert" className="startup-error">
          <h1>Contigo could not start</h1>
          <p>{message}</p>
        </div>
      </StrictMode>,
    );
    return;
  }

  const msalInstance = new PublicClientApplication(buildMsalConfig(appConfig));

  root.render(
    <StrictMode>
      <MsalProvider instance={msalInstance}>
        <App appConfig={appConfig} />
      </MsalProvider>
    </StrictMode>,
  );
}

void bootstrap();
