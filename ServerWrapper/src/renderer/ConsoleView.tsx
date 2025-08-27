import { Box, Text } from '@chakra-ui/react';
import { useEffect, useState } from 'react';

export default function ConsoleView() {
  const [logs, setLogs] = useState<string[]>([]);

  useEffect(() => {
    window.api.process.onStdout((msg) => {
      setLogs((prev) => [...prev, msg]);
    });

    window.api.process.onStderr((msg) => {
      setLogs((prev) => [...prev, `[ERR] ${msg}`]);
    });

    window.api.process.onExit((code) => {
      setLogs((prev) => [...prev, `Process exited with code ${code}`]);
    });
  }, []);

  return (
    <Box
      as="pre"
      bg="black"
      color="green.400"
      fontFamily="mono"
      p={2}
      w="95vw"
      h="90vh"
      overflowY="auto"
      whiteSpace="pre-wrap"
      borderRadius="md"
    >
      {logs.join('')}
    </Box>
  );
}
