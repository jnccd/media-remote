import { ipcMain, BrowserWindow } from 'electron';
import fs from 'fs';

export function registerIpc() {
  ipcMain.handle('window:minimize', () => {
    BrowserWindow.getFocusedWindow()?.minimize();
  });

  ipcMain.handle('fs:readFile', (_e, filePath: string) => {
    return fs.readFileSync(filePath, 'utf8');
  });
}
