import { ElectronHandler } from '../main/preload';

declare global {
  // eslint-disable-next-line no-unused-vars
  interface Window {
    electron: ElectronHandler;
    api: {
      window: {
        minimize: () => void;
      };
      fs: {
        readFile: (path: string) => Promise<string>;
      };
    };
  }
}

export {};
