import ConsoleView from './ConsoleView';

export default function App() {
  // The whole surface is the server console. The native titlebar's minimize and close
  // buttons both hide the window to the tray (handled in src-tauri), and the tray's
  // "Show App" / double-click restores it.
  return <ConsoleView />;
}
