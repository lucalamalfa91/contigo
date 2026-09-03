import { afterEach, describe, expect, it, vi } from "vitest";
import { createApiClient } from "../../src/api/client";

describe("createApiClient().getHealth", () => {
  afterEach(() => {
    vi.unstubAllGlobals();
  });

  it("calls GET <baseUrl>/health without caching", async () => {
    const fetchMock = vi.fn().mockResolvedValue(new Response("Healthy", { status: 200 }));
    vi.stubGlobal("fetch", fetchMock);

    await createApiClient("https://api.dev.contigo.example").getHealth();

    expect(fetchMock).toHaveBeenCalledTimes(1);
    const [url, init] = fetchMock.mock.calls[0];
    expect(String(url)).toBe("https://api.dev.contigo.example/health");
    expect(init).toEqual({ cache: "no-store" });
  });

  it("reports ok:true with the response body on 200 Healthy", async () => {
    vi.stubGlobal("fetch", vi.fn().mockResolvedValue(new Response("Healthy", { status: 200 })));

    const result = await createApiClient("https://api.dev.contigo.example").getHealth();

    expect(result).toEqual({ ok: true, statusCode: 200, body: "Healthy" });
  });

  it("reports ok:true on 200 Degraded (still a 2xx, per Program.cs's default HealthCheckOptions)", async () => {
    vi.stubGlobal("fetch", vi.fn().mockResolvedValue(new Response("Degraded", { status: 200 })));

    const result = await createApiClient("https://api.dev.contigo.example").getHealth();

    expect(result).toEqual({ ok: true, statusCode: 200, body: "Degraded" });
  });

  it("reports ok:false with the response body on 503 Unhealthy, without throwing", async () => {
    vi.stubGlobal("fetch", vi.fn().mockResolvedValue(new Response("Unhealthy", { status: 503 })));

    const result = await createApiClient("https://api.dev.contigo.example").getHealth();

    expect(result).toEqual({ ok: false, statusCode: 503, body: "Unhealthy" });
  });

  it("resolves (does not throw) with statusCode null and a descriptive body when the network request fails", async () => {
    vi.stubGlobal("fetch", vi.fn().mockRejectedValue(new TypeError("network down")));

    const result = await createApiClient("https://api.dev.contigo.example").getHealth();

    expect(result.ok).toBe(false);
    expect(result.statusCode).toBeNull();
    expect(result.body).toContain("https://api.dev.contigo.example/health");
    expect(result.body).toContain("network down");
  });
});
