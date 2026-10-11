import { useEffect, useRef, useState } from "react";
import { getAuthHeaderForMediaControlAPI } from "./useEncryption";
import { serverWebSocketUrl } from "./serverAddress";

export type MouseButtonName = "left" | "right" | "middle";

/** Server -> client frames. */
type ServerMessage = {
  type?: string;
  message?: string;
  command?: string;
};

/**
 * The single control channel to the server: mouse/keyboard streaming plus the media and
 * navigation buttons. One socket is opened per password (by App, which passes the returned
 * handle to the input pad), so a button press is a frame on an already-authenticated socket
 * instead of a fresh request that has to re-derive the AES token first.
 */
export function useInputSocket(password: string | null) {
  const [ready, setReady] = useState(false);
  /** Connection-level problem: the socket is down, or the password was rejected. */
  const [error, setError] = useState<string | null>(null);
  /** The last command frame the host rejected; cleared when a new one is sent. */
  const [commandError, setCommandError] = useState<string | null>(null);
  const wsRef = useRef<WebSocket | null>(null);
  const reconnectTimer = useRef<number | null>(null);
  const passwordRef = useRef(password);
  passwordRef.current = password;

  // Mirrors `ready` for the send path. State lags by a render, and the handshake has a real
  // gap (deriving the token takes a moment), so a press during it must not land on the wire
  // before the auth frame - the server would reject the connection as unauthenticated.
  const authenticatedRef = useRef(false);

  useEffect(() => {
    if (!password) {
      authenticatedRef.current = false;
      setReady(false);
      setError(null);
      setCommandError(null);
      return;
    }

    let disposed = false;

    const connect = () => {
      if (disposed) return;
      // Absolute, not window.location: under Tauri the page origin is the
      // webview's asset protocol, so a host-relative URL would open a socket
      // back into the webview instead of to the server.
      const ws = new WebSocket(serverWebSocketUrl("/inputws"));
      wsRef.current = ws;

      ws.onopen = async () => {
        const token = await getAuthHeaderForMediaControlAPI(passwordRef.current ?? "");
        ws.send(JSON.stringify({ type: "auth", authorization: token }));
      };

      ws.onmessage = (e) => {
        if (typeof e.data !== "string") return;
        try {
          const msg = JSON.parse(e.data) as ServerMessage;
          if (msg.type === "ready") {
            authenticatedRef.current = true;
            setReady(true);
            setError(null);
            setCommandError(null);
          } else if (msg.type === "error") {
            // Sent only for a rejected handshake, immediately before the server closes
            // the socket, so onclose clears `ready` again.
            setError(msg.message ?? "error");
          } else if (msg.type === "commandError") {
            // A button press the host could not carry out (e.g. the input backend is
            // missing). The socket is still fine, so this is not a connection error.
            setCommandError(msg.message ?? "The command failed.");
          }
        } catch {
          /* ignore malformed frames */
        }
      };

      ws.onclose = () => {
        authenticatedRef.current = false;
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

  /**
   * Returns false when the frame could not be handed to the server. Nothing is sent before
   * the handshake completes, so streaming input and button presses can never overtake the
   * auth frame.
   */
  const send = (msg: Record<string, unknown>): boolean => {
    const ws = wsRef.current;
    if (!authenticatedRef.current || !ws || ws.readyState !== WebSocket.OPEN) return false;
    ws.send(JSON.stringify(msg));
    return true;
  };

  return {
    ready,
    error,
    commandError,
    moveMouse: (dx: number, dy: number) => send({ type: "mousemove", dx, dy }),
    click: (button: MouseButtonName) => send({ type: "click", button }),
    scroll: (delta: number) => send({ type: "scroll", delta }),
    typeText: (text: string) => send({ type: "text", text }),
    pressKey: (key: string) => send({ type: "key", key }),
    /**
     * Runs one of the server's whitelisted buttons ("play", "arrow-left", "volume-up", ...;
     * see Server/Src/Input/RemoteCommands.cs). Anything else is ignored by the server.
     */
    runCommand: (command: string) => {
      if (send({ type: "command", command })) setCommandError(null);
      else setCommandError("the connection is not ready yet");
    },
  };
}

export type InputSocket = ReturnType<typeof useInputSocket>;
