use std::io::{BufRead, BufReader};
use std::process::{Child, Command, Stdio};
use std::sync::Mutex;

#[cfg(target_os = "windows")]
use std::os::windows::process::CommandExt;

use tauri::menu::{Menu, MenuItem};
use tauri::tray::{MouseButton, TrayIconBuilder, TrayIconEvent};
use tauri::{AppHandle, Emitter, Manager, RunEvent, State, WindowEvent};

/// Owns the running server process so it can be killed on quit.
struct ServerState(Mutex<Option<Child>>);

type SetupResult = Result<(), Box<dyn std::error::Error>>;

#[cfg_attr(mobile, tauri::mobile_entry_point)]
pub fn run() {
    tauri::Builder::default()
        .manage(ServerState(Mutex::new(None)))
        .setup(|app| {
            setup_tray(app.handle())?;
            spawn_server(app.handle())?;
            Ok(())
        })
        .invoke_handler(tauri::generate_handler![kill_server, hide_window])
        // Minimize to tray: Tauri has no "Minimized" event, so we detect it by the window
        // losing focus while it is in the minimized state, then hide it. Closing also hides.
        .on_window_event(|window, event| {
            match event {
                WindowEvent::CloseRequested { api, .. } => {
                    api.prevent_close();
                    let _ = window.hide();
                }
                WindowEvent::Focused(false) => {
                    if window.is_minimized().unwrap_or(false) {
                        let _ = window.hide();
                    }
                }
                _ => {}
            }
        })
        .build(tauri::generate_context!())
        .expect("error while building tauri application")
        .run(|app_handle, event| {
            if let RunEvent::Exit = event {
                if let Some(mut child) = app_handle
                    .state::<ServerState>()
                    .0
                    .lock()
                    .unwrap()
                    .take()
                {
                    let _ = child.kill();
                }
            }
        });
}

// ---------------------------------------------------------------------------
// Server process
// ---------------------------------------------------------------------------

/// Returns (program, args, cwd). In a dev build we run `dotnet run -c Debug` from the
/// repository's Server directory (overridable via MEDIA_SERVER_DIR); in a release build we
/// Locates the bundled server binary.
///
/// Order matters:
///   1. `MEDIA_CONTROL_SERVER` - an explicit override, useful in dev and for a
///      system-wide install that ships the server separately.
///   2. next to the running executable - this is how a Nix package works: the
///      app is `$out/bin/media-control-desktop` and the server is installed
///      alongside it. Tauri's `resource_dir()` is not useful there because a Nix
///      package has no macOS-style `Resources/` directory, so it points at the
///      store root and the server is never found.
///   3. the Tauri resource dir - the bundled installer layout (deb/rpm/AppImage).
fn find_bundled_server(app: &AppHandle, exe_name: &str) -> Option<std::path::PathBuf> {
    if let Ok(explicit) = std::env::var("MEDIA_CONTROL_SERVER") {
        let p = std::path::PathBuf::from(explicit);
        if p.exists() {
            return Some(p);
        }
    }

    if let Ok(exe) = std::env::current_exe() {
        if let Some(dir) = exe.parent() {
            for candidate in [dir.join(exe_name), dir.join("server").join(exe_name)] {
                if candidate.exists() {
                    return Some(candidate);
                }
            }
        }
    }

    if let Ok(resource_dir) = app.path().resource_dir() {
        // `resources/{from: "resources/server", to: "server"}` -> <res>/server/<exe>
        // Older/alternative layouts: <res>/resources/server/<exe>, <res>/<exe>.
        for base in [
            resource_dir.join("server"),
            resource_dir.join("resources").join("server"),
            resource_dir.clone(),
        ] {
            let exe = base.join(exe_name);
            if exe.exists() {
                return Some(exe);
            }
        }
    }

    None
}

/// run the self-contained binary bundled as a resource. The resource layout depends on how
/// the bundler mapped `bundle.resources`, so we probe a few candidate locations.
fn server_command(app: &AppHandle) -> (String, Vec<String>, Option<String>) {
    if cfg!(debug_assertions) {
        let dir = std::env::var("MEDIA_SERVER_DIR")
            .unwrap_or_else(|_| "../../Server".to_string());
        (
            "dotnet".to_string(),
            vec!["run".to_string(), "-c".to_string(), "Debug".to_string()],
            Some(dir),
        )
    } else {
        let exe_name = if cfg!(target_os = "windows") {
            "MediaControlServer.exe"
        } else {
            "MediaControlServer"
        };

        if let Some(exe) = find_bundled_server(app, exe_name) {
            // Run the server from its own directory so its content root (and thus
            // the Frontend/dist + appsettings.json next to it) resolve correctly.
            let cwd = exe.parent().map(|p| p.to_string_lossy().to_string());
            return (exe.to_string_lossy().to_string(), Vec::new(), cwd);
        }

        // Nothing found: fall back to the bare name so the spawn error is clear.
        (exe_name.to_string(), Vec::new(), None)
    }
}

/// Spawns the server, streams its stdout/stderr to the frontend as events, and keeps a
/// handle so it can be killed.
fn spawn_server(app: &AppHandle) -> SetupResult {
    let (program, args, cwd) = server_command(app);

    let mut cmd = Command::new(&program);
    cmd.args(&args);
    if let Some(dir) = &cwd {
        cmd.current_dir(dir);
    }
    cmd.stdout(Stdio::piped()).stderr(Stdio::piped());

    // On Windows the server is a console-subsystem exe; without this flag it flashes an
    // empty console window. CREATE_NO_WINDOW keeps it running windowless (its stdout/stderr
    // still reach us via the piped handles above).
    #[cfg(target_os = "windows")]
    cmd.creation_flags(0x08000000);

    let mut child = cmd.spawn()?;

    let out_app = app.clone();
    if let Some(out) = child.stdout.take() {
        std::thread::spawn(move || {
            let reader = BufReader::new(out);
            for line in reader.lines() {
                match line {
                    Ok(l) => {
                        let _ = out_app.emit("server:stdout", l);
                    }
                    Err(_) => break,
                }
            }
            let _ = out_app.emit("server:exit", "stdout closed");
        });
    }

    let err_app = app.clone();
    if let Some(err) = child.stderr.take() {
        std::thread::spawn(move || {
            let reader = BufReader::new(err);
            for line in reader.lines() {
                match line {
                    Ok(l) => {
                        let _ = err_app.emit("server:stderr", l);
                    }
                    Err(_) => break,
                }
            }
        });
    }

    app.state::<ServerState>().0.lock().unwrap().replace(child);
    Ok(())
}

#[tauri::command]
fn kill_server(state: State<'_, ServerState>) -> Result<(), String> {
    if let Some(mut child) = state.0.lock().unwrap().take() {
        child.kill().map_err(|e| e.to_string())?;
    }
    Ok(())
}

#[tauri::command]
fn hide_window(app: AppHandle) {
    if let Some(window) = app.get_webview_window("main") {
        let _ = window.hide();
    }
}

// ---------------------------------------------------------------------------
// System tray
// ---------------------------------------------------------------------------

fn setup_tray(app: &AppHandle) -> SetupResult {
    let show = MenuItem::with_id(app, "show", "Show App", true, None::<&str>)?;
    let quit = MenuItem::with_id(app, "quit", "Quit", true, None::<&str>)?;
    let menu = Menu::with_items(app, &[&show, &quit])?;

    let icon = app
        .default_window_icon()
        .cloned()
        .ok_or("no default window icon")?;

    TrayIconBuilder::with_id("main-tray")
        .icon(icon)
        .tooltip("Media Remote Control")
        .menu(&menu)
        .on_menu_event(|app, event| match event.id().as_ref() {
            "show" => show_window(app),
            "quit" => app.exit(0),
            _ => {}
        })
        .on_tray_icon_event(|tray, event| {
            if let TrayIconEvent::DoubleClick { button: MouseButton::Left, .. } = event {
                show_window(tray.app_handle());
            }
        })
        .build(app)?;

    Ok(())
}

fn show_window(app: &AppHandle) {
    if let Some(window) = app.get_webview_window("main") {
        // If the window was hidden while minimized, unminimize so show() restores cleanly.
        let _ = window.unminimize();
        let _ = window.show();
        let _ = window.set_focus();
    }
}
