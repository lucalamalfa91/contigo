/// <reference types="vitest/config" />
import { defineConfig } from "vite";
import react from "@vitejs/plugin-react";

// Plain Vite React SPA (ADR-012): static build output (`dist/`), no SSR/server
// runtime to operate. `test` config lives here (not a separate vitest.config)
// so there is exactly one build/test tool config to keep in sync.
export default defineConfig({
  plugins: [react()],
  server: {
    port: 5173,
  },
  test: {
    environment: "jsdom",
    setupFiles: ["./tests/setup.ts"],
    css: false,
  },
});
