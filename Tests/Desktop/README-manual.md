# Operator Desktop — Manual Test Checklist (Phases 9-10)

> Presentation and wiring only. Automated review already confirms the invariants:
> the project compiles, the palette/elevation tokens live on the `:root` of
> `UnityDefaultRuntimeTheme.tss`, `Core/` stays engine-free, `KernelBridge` is the
> single `MonoBehaviour` and the single `scheduler.Tick()` caller, and
> `dotnet test Siegebox.sln` is 681/681 green. The *look itself* — the wallpaper, the
> chrome, the palette, the panel — is accepted here, by eye. This file consolidates the
> Phase-9 (visual identity) and Phase-10 (documentation) desktop checks that previously
> lived in `Tests/WindowManager/README-manual.md`, and is the desktop's home going
> forward. Run in Unity 6000.4.0f1.

## Prerequisite — the monospace font (Fira Code, bundled)

- [ ] Give the Unity editor window focus so `Assets/Unity/UI/FiraCode-Regular.ttf`
      auto-imports (import triggers on focus). The `.tss` references the font by guid;
      the imported Font's main object id (`fileID 12800000`) is constant, so the
      `-unity-font-definition` binding resolves as soon as the `.ttf` is imported.
- [ ] Confirm ALL UI text — terminal output, prompt, window titlebars, the system panel,
      the doc-browser — renders in Fira Code (fixed-width; no programming ligatures, since
      UI Toolkit TextCore does not apply them).
- [ ] (Optional refinement) A scaled/crisp SDF FontAsset can be baked later from the
      `.ttf` (Window ▸ Text ▸ Font Asset Creator) and `-unity-font-definition` repointed at
      it — not required; the dynamic Font renders correctly at the terminal's fixed size.

## Top system panel (tray)

- [ ] The top panel shows: the Siegebox menu mark (left), network + volume indicator
      shapes, the current user (`$ player (1000)`), a live `HH:mm` clock, and a power
      control.
- [ ] The clock advances each minute while the app runs (it is driven by the UI panel
      scheduler `root.schedule.Execute(...).Every(1000)`, NOT a second `MonoBehaviour` or
      the kernel tick). Power quits the app (standalone build).

## Dock from the registry

- [ ] The dock launchers are populated from `AppRegistry.Descriptors` (Id-sorted), not a
      hardcoded list — the `terminal`, `files`, `docs`, `about` launchers appear;
      registering a new disk-mod app makes it show up automatically.

## Window chrome

- [ ] Titlebars are rounded, carry an app-icon mark, and min/max/close are GRAPHIC glyphs
      (no text characters). Maximizing a window flips the maximize glyph to a distinct
      restore (double-square) look.
- [ ] Close-button hover turns red (`--sb-danger`); other title-button hovers are neutral.
- [ ] (Known limitation) The app icon is a UNIFORM Siegebox mark, not per-app — expected
      (recorded debt), not a checklist failure.

## Focus / selection

- [ ] Clicking a window raises it and highlights its border/titlebar with the single teal
      accent; its taskbar/dock entry highlights. Typing goes only to the focused terminal.
- [ ] In the doc-browser, selecting a command in the nav shows that page in the viewer.

## Identity indicator

- [ ] Each window's chrome shows its LAUNCH identity: `$ player (1000)`; a root-launched
      window would show `# root (0)`, mirroring the terminal prompt `#`/`$`. The `about`
      window (no session) shows no identity label.
- [ ] (Known limitation) The indicator reflects the identity the window was OPENED as, not
      a live `su` switch inside the terminal (recorded debt) — the terminal prompt remains
      authoritative for live privilege. The `WindowIdentity` glyph/label logic has no Unity
      test host, so THIS visual check is its coverage.

Note: the reserved amber accent (`--sb-accent-amber`) is intended to denote a "remote box /
foreign territory" in a future networking slice; it is DECLARED but NOT applied anywhere this
iteration, so there is nothing amber to verify yet — the home-vs-remote distinction today is
only the `#`/`$` identity label above.

## Palette & elevation from tokens

- [ ] Every surface colour comes from the `:root` tokens in `UnityDefaultRuntimeTheme.tss`
      — the desktop reads as four distinct dark blue-gray elevation levels (void → window →
      panel → raised) with NO shadows or gradients.
- [ ] Exactly ONE teal accent is visible (focus border, terminal prompt, app-icon edge).
      No green prompt; no red/blue in ordinary chrome (reserved for future game semantics).

## Siege wallpaper & trademark check

- [ ] The desktop backdrop is an original Siegebox "siege" motif (a crenellated curtain
      wall with three towers, in USS shapes) behind the windows, drawn behind the top panel
      and taskbar; it never captures pointer events (dragging a window over it works
      normally).
- [ ] No Kali/OffSec dragon, logo, or theme anywhere.

## Regression

- [ ] `dotnet test Siegebox.sln` → 681/681 green (Core untouched — the desktop/doc layers
      are presentation + Core-doc additions with no invariant change).
- [ ] `KernelBridge` is the sole `MonoBehaviour`:
      `grep -rn ": MonoBehaviour" UnityProject/Assets/Unity` → only `KernelBridge.cs`.
- [ ] `KernelBridge` is the sole `scheduler.Tick()` caller:
      `grep -rn "scheduler.Tick()" UnityProject/Assets` → the only match is in
      `KernelBridge.cs` (do NOT pin a line number — it drifts).
- [ ] No UI VFS read outside the resolver/Credentials:
      `grep -rn "new Credentials(0)" UnityProject/Assets/Unity` → no matches (seeding lives
      in Core; the panel/doc-browser/file-manager all read under the launching session's
      Credentials).
- [ ] Colours only via tokens:
      `grep -riE "rgb\(|rgba\(" UnityProject/Assets/Unity/UI/*.uss` → no output (component
      USS use `var(--sb-*)`; literals live only in `UnityDefaultRuntimeTheme.tss` `:root`).
- [ ] Core stays engine-free:
      `grep -rn "UnityEngine" Core --include="*.cs"` → no output (scope to `.cs`; a prose
      mention in `Core/package.json` is not code).
- [ ] Zero trademarks:
      `grep -rniE "kali|offsec|dragon" UnityProject/Assets/Unity` → no output.
