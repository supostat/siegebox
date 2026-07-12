# Window Manager — Manual Test Checklist (Phase 5)

Manual/visual spec per ADR 2026-07-09: the windowing layer lives in the Unity assembly
and is exercised in the editor, not by `dotnet test`. Run in Unity 6000.4.0f1.

## Setup

- [ ] Rewire `Assets/Scenes/Main.unity`: delete the scene and re-run
      `Siegebox → Create Desktop Scene`, or hand-wire the two new `KernelBridge`
      fields (`desktopTemplate` → `Desktop.uxml`, `windowTemplate` → `Window.uxml`);
      existing `uiDocument`/`terminalTemplate` references survive (field names unchanged).
- [ ] Commit the `.meta` files the editor generates for the new assets
      (`WindowManager/` folder, `Desktop.uxml/.uss`, `Window.uxml/.uss`, new `.cs` files).

## Create

- [ ] Press Play: the desktop fills the screen, one terminal window opens focused.
- [ ] Taskbar launcher `terminal` clicked twice more → three windows total, cascaded
      (each offset down-right from the previous); each new window opens focused,
      on top of the others, and typing goes to it.
- [ ] Taskbar launcher `about` → a placeholder window with the message, no terminal UI.
- [ ] Each opened window gets a taskbar entry showing its title (`about` included).

## Focus / z-order

- [ ] Click a background window: it raises above the others, its border/titlebar
      highlight, its taskbar entry highlights.
- [ ] Typing goes ONLY to the focused terminal; unfocused terminals receive nothing.
- [ ] Clicking anywhere inside a window (content included) focuses it.

## Drag

- [ ] Titlebar drag moves the window; it cannot be dragged above the top edge and at
      least 40px of the titlebar stays reachable inside the layer on every side.
- [ ] A window dragged partially outside clips at the desktop edge and never draws
      over the taskbar.
- [ ] Dragging does nothing while the window is maximized.

## Resize

- [ ] Right edge handle resizes width only; bottom edge height only; corner both.
- [ ] Resize clamps at the 240×160 minimum.
- [ ] Resizing past the desktop edge: the window clips at the edge and never draws
      over the taskbar; no console errors.
- [ ] Resize does nothing while the window is maximized.

## Minimize

- [ ] Minimize button hides the window; its taskbar entry dims.
- [ ] Focus falls to the topmost remaining visible window (or none if all hidden).
- [ ] With every window minimized, no taskbar entry stays highlighted.
- [ ] Restoring via the taskbar entry shows the window in its previous state,
      focuses it and raises it above the other windows; its taskbar entry un-dims
      and highlights.

## Maximize

- [ ] Maximize fills the window layer (taskbar stays visible), button glyph flips;
      the taskbar entry does not dim (dimming is for minimized only).
- [ ] Toggling again restores the exact previous geometry.
- [ ] Maximize → minimize → restore returns to the maximized state.

## Taskbar entry click cycle

- [ ] Entry of a minimized window → restores it.
- [ ] Entry of the focused window → minimizes it.
- [ ] Entry of a visible unfocused window → focuses and raises it above the other
      windows.

## Close

- [ ] ✕ removes the window and its taskbar entry.
- [ ] Closing the focused window with several windows visible → focus and highlight
      move to the topmost remaining window, and typing goes to it.
- [ ] Per-terminal isolation: `cd` / `export` in one terminal are invisible in another.
- [ ] Closing a terminal mid-command (`cat` running) runs the hangup cascade (exit 129);
      the remaining terminal keeps working.
- [ ] Double-close is idempotent (no errors when close races a state change).
- [ ] Closing all windows leaves the desktop usable: launchers still open new windows.
- [ ] Stop Play mode with at least one window minimized → console stays clean,
      no teardown errors (covers CloseAll over a minimized window).

## Reusability

- [ ] The `about` window drags, resizes, minimizes, maximizes, focuses, closes and
      cycles through its taskbar entry exactly like a terminal window
      (code check: `grep -ri terminal UnityProject/Assets/Unity/WindowManager/*.cs`
      matches only doc comments — no Terminal types referenced).

## Regression

- [ ] `dotnet test Siegebox.sln` → 340/340 green.
- [ ] Play-mode console clean: no errors or warnings during the whole checklist.

# Application framework (Phase 6)

## Setup

- [ ] Open the project in the editor so it imports `FileManager.uxml`/`.uss` and the new
      `.cs` files (generates their `.meta` files), then wire the new `KernelBridge` field:
      either delete `Assets/Scenes/Main.unity` and re-run `Siegebox → Create Desktop Scene`,
      or text-edit the scene YAML — insert after the `terminalTemplate:` line, 2-space
      indented: `fileManagerTemplate: {fileID: 9197481963319205126, guid: <guid from
      FileManager.uxml.meta>, type: 3}` — and reopen the scene in the editor afterwards
      (do NOT save a stale in-memory copy over the edit).
- [ ] Commit the `.meta` files the editor generates (`Core/App/*.cs`, `Unity/Apps/`,
      `FileManager.uxml/.uss`) — mirrors the Phase-5 Setup step.

## Launchers and open

- [ ] Taskbar launchers appear in order `files`, `terminal`, `about` (registry
      Descriptors sorted by Id, `about` appended last) — expected change from Phase 5.
- [ ] `files` launcher opens the file manager focused, with a taskbar entry.
- [ ] `open files` / `open terminal` typed in a terminal open windows identically
      to the taskbar launchers.
- [ ] `open files &` still opens the window and announces a job entry (`[1] pid`) —
      open is a process command, not a builtin.
- [ ] `open ghost` prints `open: ghost: no such app` on stderr, exit code 1.
- [ ] `help` lists `open` among the commands.

## File manager

- [ ] `mkdir /d ; touch /d/a` in a terminal, then refocus the file manager →
      the new entries appear.
- [ ] Clicking a directory row navigates into it; `up` at `/` stays at `/`.
- [ ] Directory rows show a trailing `/` and no size; file rows show the size
      with a ` B` suffix; only directory rows react to clicks.
- [ ] `mkdir /empty`, click into it → path bar shows `/empty`, the list is empty,
      no error status.
- [ ] Delete the file manager's current directory in a terminal, refocus the file
      manager → the status line shows the error, no exception in the console.
- [ ] `touch '/<b>x</b>'` → the name renders literally in the file manager rows
      (rich text off).
- [ ] Closing the file manager window is clean: no errors, taskbar entry removed.

## Teardown

- [ ] Stop Play mode with several windows open (file manager included) → console
      stays clean (CloseAll focus-fallback suppression).

## Regression

- [ ] Windowing-layer grep check still passes:
      `grep -ri terminal UnityProject/Assets/Unity/WindowManager/*.cs` matches only
      doc comments; same for App types — the windowing layer keeps zero kernel and
      zero app references.
- [ ] `dotnet test Siegebox.sln` → 355/355 green.

# Extensibility & Scripting (Phase 7)

## Setup

- [ ] Open the project in the editor so it imports the new `Core/Events`, `Core/Scripting`,
      and `Unity/Apps/TextAppContent.cs` files (generates their `.meta`) and resolves the
      MoonSharp UPM package (`org.moonsharp.moonsharp`, pinned commit in
      `Packages/manifest.json`) into `packages-lock.json` — both are already committed with
      `5678167`; a clean editor open should show no import errors and no re-resolution diff.
- [ ] The `mods/` folder sits at the repo root (next to `Core/`, `UnityProject/`), containing
      `mods/example/{manifest.json, hello_command.lua, hello_app.lua}`. In the editor the
      mods root resolves to `<repo>/mods` (`Application.dataPath/../../mods`).

## Mods, Lua command, and Lua app

- [ ] On Play, the taskbar shows launchers `about`, `files`, `hello`, `terminal` (registry
      Descriptors sorted by Id — `hello` is the Lua app from `mods/example`, proving a disk
      mod loaded and registered through the same registry as C# content).
- [ ] In a terminal, `hello` prints `hello from lua` (exit 0); `hello world` prints
      `hello, world`. `echo hi | hello` runs a Lua command inside a pipe. `help` lists
      `hello` among the commands — a Lua command is indistinguishable from a C# one.
- [ ] `open hello-app` (or the `hello` taskbar launcher) opens the Lua app's window showing
      `hello from a lua app`; refocusing it a few times updates the text to `focused N times`
      (on_focus hook fires). Closing it is clean, taskbar entry removed.
- [ ] `about` still opens (now a registered `StaticTextApp`, not the last taskbar hardcode).

## Sandbox and mod-loader behavior

- [ ] Temporarily add a second mod dir `mods/bad/` with `manifest.json` naming a script that
      calls `siegebox.register_command("<b>x</b>", ...)` or `error(...)` → on Play the console
      shows a single `mod 'bad' failed: …` line and the game still boots with `example` loaded
      (one broken mod never aborts boot). Remove `mods/bad/` afterward.
- [ ] A Lua app calling `app.set_text("<b>x</b>")` renders the markup literally in its window
      (TextAppContent has rich text off — no mod-controlled markup is applied).

## Regression

- [ ] `dotnet test Siegebox.sln` → 520/520 green.

# User model & authentication (Phase 7 follow-up)

## Launch identity

- [ ] The starting terminal opens as the unprivileged `player`, not root: the prompt ends
      with ` $ ` (a root session would end with ` # `), and the working directory is
      `/home/player`.
- [ ] `cat /etc/passwd` succeeds (world-readable, shows `root` and `player`); `cat /etc/shadow`
      fails with a permission error (root-only 0600 — an unprivileged process cannot read the
      password hashes).

## su authentication

- [ ] `su root`, then at the `Password:` prompt type `root` → the session becomes root and the
      prompt switches to ` # `. (Password echo is not suppressed yet — a known limitation.)
- [ ] `su root` with a wrong password → `su: authentication failure`, exit 1, you stay
      `player` (prompt still ` $ `).
- [ ] From a root session, `su player` switches to player with NO password prompt (root
      switches to anyone freely); the prompt returns to ` $ `.
- [ ] `su ghost` (unknown user) → `su: user 'ghost' does not exist`, exit 1, identity unchanged.
- [ ] After dropping to `player`, a root-only action (e.g. `mkdir /root/x`) fails with a
      permission error — the identity is real, not cosmetic.

## Regression

- [ ] `dotnet test Siegebox.sln` → 557/557 green.
