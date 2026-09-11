/**
 * Where the MediaControl server lives.
 *
 * The page is *not* served by the server in every context:
 *
 *  - In a browser served by the server itself (phone / PC on the LAN), the page
 *    origin IS the server, so relative URLs work.
 *  - In the Tauri desktop wrapper the page is served by Tauri's own asset
 *    protocol, so `window.location` is something like `http://tauri.localhost`.
 *    Relative URLs then point at the webview, not the server, and every request
 *    fails as if the server were down ("Could not connect to localhost:
 *    Connection refused"). The server is actually running, spawned by the app.
 *
 * So: use the page origin only when it really is http(s); otherwise fall back to
 * the server's default address. `VITE_API_BASE` overrides both, for a
 * non-default port or a remote host.
 */

/** Must match the server's default port (Server/Src/Configuration.cs). */
export const DEFAULT_SERVER_ORIGIN = "http://localhost:7779";

function resolveServerOrigin(): string {
  const override = import.meta.env.VITE_API_BASE as string | undefined;
  if (override) return override.replace(/\/+$/, "");

  // http/https means the page came from the server (or the Vite dev server),
  // so same-origin relative URLs are correct. Anything else (tauri:, file:,
  // null) has no server behind it and must use the explicit address.
  if (window.location.protocol === "http:" || window.location.protocol === "https:") {
    return window.location.origin;
  }
  return DEFAULT_SERVER_ORIGIN;
}

export const serverOrigin = resolveServerOrigin();

/** Absolute URL for an API path, e.g. "/play" -> "http://localhost:7779/play". */
export function serverUrl(path: string): string {
  return `${serverOrigin}${path.startsWith("/") ? path : `/${path}`}`;
}

/** ws:// or wss:// address for the input socket. */
export function serverWebSocketUrl(path: string): string {
  const url = new URL(serverUrl(path));
  url.protocol = url.protocol === "https:" ? "wss:" : "ws:";
  return url.toString();
}
