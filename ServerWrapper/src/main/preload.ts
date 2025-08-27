// Disable no-unused-vars, broken for spread args
/* eslint no-unused-vars: off */
import { contextBridge, ipcRenderer, IpcRendererEvent } from 'electron';

export type Channels = 'ipc-example';

const electronHandler = {
  ipcRenderer: {
    sendMessage(channel: Channels, ...args: unknown[]) {
      ipcRenderer.send(channel, ...args);
    },
    on(channel: Channels, func: (...args: unknown[]) => void) {
      const subscription = (_event: IpcRendererEvent, ...args: unknown[]) =>
        func(...args);
      ipcRenderer.on(channel, subscription);

      return () => {
        ipcRenderer.removeListener(channel, subscription);
      };
    },
    once(channel: Channels, func: (...args: unknown[]) => void) {
      ipcRenderer.once(channel, (_event, ...args) => func(...args));
    },
  },
};

contextBridge.exposeInMainWorld('electron', electronHandler);
contextBridge.exposeInMainWorld('api', {
  window: {
    minimize: () => ipcRenderer.invoke('window:minimize'),
  },
  fs: {
    readFile: (path: string) => ipcRenderer.invoke('fs:readFile', path),
  },
  process: {
    onStdout: (callback: (msg: string) => void) =>
      ipcRenderer.on('process:stdout', (_event, msg) => callback(msg)),

    onStderr: (callback: (msg: string) => void) =>
      ipcRenderer.on('process:stderr', (_event, msg) => callback(msg)),

    onExit: (callback: (code: number) => void) =>
      ipcRenderer.on('process:exit', (_event, code) => callback(code)),

    kill: () => ipcRenderer.invoke('process:kill'),
  },
});

export type ElectronHandler = typeof electronHandler;
