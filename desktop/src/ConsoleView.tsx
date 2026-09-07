import { useEffect, useState } from 'react';
import { listen } from '@tauri-apps/api/event';

const MAX_LOGS = 500;

export default function ConsoleView() {
  const [logs, setLogs] = useState<string[]>([]);

  // Functional update keeps this stable across renders, so the effect can run once.
  const push = (msg: string) =>
    setLogs((prev) => [...prev, msg].slice(-MAX_LOGS));

  useEffect(() => {
    const un1 = listen<string>('server:stdout', (e) => push(e.payload));
    const un2 = listen<string>('server:stderr', (e) => push(`[ERR] ${e.payload}`));
    const un3 = listen<string>('server:exit', (e) => push(`Process exited: ${e.payload}`));

    return () => {
      un1.then((un) => un());
      un2.then((un) => un());
      un3.then((un) => un());
    };
  }, []);

  return <pre className="console">{logs.join('\n')}</pre>;
}
