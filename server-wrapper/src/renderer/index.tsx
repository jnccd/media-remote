import { createRoot } from 'react-dom/client';
import App from './App';
import { ChakraProvider, ColorModeScript } from '@chakra-ui/react';
import { QueryClient, QueryClientProvider } from '@tanstack/react-query';
import theme from './theme';

const container = document.getElementById('root') as HTMLElement;
const root = createRoot(container);
const queryClient = new QueryClient();
root.render(
  <ChakraProvider theme={theme}>
    <QueryClientProvider client={queryClient}>
      <ColorModeScript
        initialColorMode={theme.config.initialColorMode}
      ></ColorModeScript>
      <App />
    </QueryClientProvider>
  </ChakraProvider>,
);

// calling IPC exposed from preload script
window.electron?.ipcRenderer.once('ipc-example', (arg) => {
  // eslint-disable-next-line no-console
  console.log(arg);
});
window.electron?.ipcRenderer.sendMessage('ipc-example', ['ping']);
