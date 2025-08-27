import { Box, Text } from '@chakra-ui/react';
import { useEffect, useState } from 'react';

const MAX_LOGS = 100;

export default function ConsoleView() {
  const [logs, setLogs] = useState<string[]>([]);

  const pushLog = (msg: string) => {
    setLogs((prev) => [...prev, msg].slice(-MAX_LOGS));
  };

  useEffect(() => {
    window.api.process.onStdout(pushLog);

    window.api.process.onStderr((msg) => {
      pushLog(`[ERR] ${msg}`);
    });

    window.api.process.onExit((code) => {
      pushLog(`Process exited with code ${code}`);
    });
  }, []);

  return (
    <Box
      as="pre"
      bg="black"
      color="green.400"
      fontFamily="mono"
      p={2}
      w="100vw"
      h="100vh"
      overflowY="auto"
      whiteSpace="pre-wrap"
      borderRadius="md"
    >
      {logs.join('')}
    </Box>
  );
}
