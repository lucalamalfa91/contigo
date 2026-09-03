import { afterEach } from "vitest";
import { cleanup } from "@testing-library/react";
import "@testing-library/jest-dom/vitest";

// vite.config.ts does not set `test.globals`, so Testing Library's own
// auto-cleanup (which detects an *ambient* `afterEach`) never registers.
// Do it explicitly instead, so each test starts from an empty document body.
afterEach(() => {
  cleanup();
});
