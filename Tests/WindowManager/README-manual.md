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
