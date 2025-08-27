import { spawn } from 'child_process';
import { ipcMain, BrowserWindow } from 'electron';
import fs from 'fs';
import { mainWindow } from './main';
import path from 'path';

export function registerIpc() {
  ipcMain.handle('window:minimize', () => {
    BrowserWindow.getFocusedWindow()?.minimize();
  });

  ipcMain.handle('fs:readFile', (_e, filePath: string) => {
    //return fs.readFileSync(filePath, 'utf8');
    return '';
  });

  const dotnetServerPath = `${__dirname.split('ServerWrapper')[0]}Server${path.sep}`;
  console.log(dotnetServerPath);
  const proc = spawn('dotnet', ['run', '-c', 'Release'], {
    cwd: dotnetServerPath,
  });

  proc.stdout.on('data', (data) => {
    mainWindow?.webContents.send('process:stdout', data.toString());
  });

  proc.stderr.on('data', (data) => {
    mainWindow?.webContents.send('process:stderr', data.toString());
  });

  proc.on('close', (code) => {
    mainWindow?.webContents.send('process:exit', code);
  });

  ipcMain.handle('process:kill', () => {
    proc.kill();
  });
}
