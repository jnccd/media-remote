import { useEffect, useRef, useState } from "react";
import { getAuthHeaderForMediaControlAPI } from "./useEncryption";

export type MouseButtonName = "left" | "right" | "middle";

/**
 * Manages a WebSocket to the server's /inputws channel for low-latency mouse/keyboard input.
 * Authenticates with the shared password on connect, auto-reconnects, and exposes typed
 * send functions.
 */
export function useInputSocket(password: string | null) {
  const [ready, setReady] = useState(false);
  const [error, setError] = useState<string | null>(null);
  const wsRef = useRef<WebSocket | null>(null);
  const reconnectTimer = useRef<number | null>(null);
  const passwordRef = useRef(password);
  passwordRef.current = password;

  useEffect(() => {
    if (!password) {
      setReady(false);
      setError(null);
      return;
    }

    let disposed = false;

    const connect = () => {
      if (disposed) return;
      const proto = window.location.protocol === "https:" ? "wss" : "ws";
      const ws = new WebSocket(`${proto}://${window.location.host}/inputws`);
      wsRef.current = ws;

      ws.onopen = async () => {
        const token = await getAuthHeaderForMediaControlAPI(passwordRef.current ?? "");
        ws.send(JSON.stringify({ type: "auth", authorization: token }));
      };

      ws.onmessage = (e) => {
        if (typeof e.data !== "string") return;
        try {
          const msg = JSON.parse(e.data) as { type?: string; message?: string };
          if (msg.type === "ready") {
            setReady(true);
            setError(null);
          } else if (msg.type === "error") {
            setReady(false);
            setError(msg.message ?? "error");
          }
        } catch {
          /* ignore malformed frames */
        }
      };

      ws.onclose = () => {
        setReady(false);
        if (!disposed) reconnectTimer.current = window.setTimeout(connect, 2000);
      };

      ws.onerror = () => {
        /* close handler drives reconnection */
      };
    };

    connect();

    return () => {
      disposed = true;
      if (reconnectTimer.current != null) window.clearTimeout(reconnectTimer.current);
      wsRef.current?.close();
      wsRef.current = null;
    };
  }, [password]);

  const send = (msg: Record<string, unknown>) => {
    const ws = wsRef.current;
    if (ws && ws.readyState === WebSocket.OPEN) ws.send(JSON.stringify(msg));
  };

  return {
    ready,
    error,
    moveMouse: (dx: number, dy: number) => send({ type: "mousemove", dx, dy }),
    click: (button: MouseButtonName) => send({ type: "click", button }),
    scroll: (delta: number) => send({ type: "scroll", delta }),
    typeText: (text: string) => send({ type: "text", text }),
    pressKey: (key: string) => send({ type: "key", key }),
  };
}
