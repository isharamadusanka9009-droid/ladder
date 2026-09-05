# Ladder to Arduino

Windows desktop app (C# / WPF) - draw ladder-logic rungs, map them to Arduino pins,
and compile + upload straight to your board over USB.

## 1. Install requirements (one time)

1. **.NET 6 SDK** (or later) - https://dotnet.microsoft.com/download
2. **Visual Studio 2022** (Community edition is free) with the ".NET desktop development"
   workload - needed to open/build the WPF project. (Or use `dotnet build` from the CLI.)
3. **arduino-cli** - https://arduino.github.io/arduino-cli/latest/installation/
   - After installing, add it to your PATH, then run once:
     ```
     arduino-cli core update-index
     arduino-cli core install arduino:avr
     ```
     (installs the Uno/Nano/Mega compiler toolchain. For ESP32 boards run
     `arduino-cli core install esp32:esp32` instead, and add the ESP32 board index URL
     per the espressif docs.)

## 2. Open and run the app

- Open `LadderToArduino.sln` in Visual Studio and press F5, **or**
- From a terminal: `cd LadderToArduino && dotnet run`

## 3. What's new (v2)

- **Multiple outputs per rung** — click **+ Output** to add more than one coil to a rung
  (e.g. drive a Q and set an M flag from the same logic).
- **Timer / Counter reset input** — each output has an optional **Reset addr** (Kind + Index).
  Set Index to `-1` for "no reset wired" (default); otherwise that address force-resets the
  timer/counter any time it's true, regardless of the rung's own logic.
- **Analog I/O** — a contact's `Kind` can be `AnalogInput` (`AI0`, `AI1`, ...): it becomes a
  comparator (`>`, `>=`, `<`, `<=`, `==`) against a threshold you type in, reading
  `analogRead()` under the hood. A coil's type can be `AnalogOutput` (writes PWM via
  `analogWrite()`), sourced from either a constant 0–255 value or copied/scaled from an
  `AnalogInput`. Map `AI`/`AO` addresses to real analog/PWM pins on the **Pin Mapping** tab
  just like `I`/`Q`.
- **Simulate tab** — runs the exact same logic as the generated sketch, entirely inside the
  app (no Arduino needed). Click **Load / Reset**, toggle input buttons / type analog values,
  then **Step** once or **Run** continuously to watch outputs, memory bits, timers, and
  counters update live. Great for testing logic before you ever plug in a board.
- **Live Monitor tab** — the generated sketch now also prints a compact state line over
  Serial every ~150 ms. After uploading, click **Start Monitor** (same COM port) to watch
  real I/Q/M/T/C values from the actual hardware. Only one program can own the port at a
  time — the app stops the monitor automatically before a re-upload.
- **Cross Reference tab** — click **Refresh** to see every address used in the program and
  which rung(s) use it as a contact vs. as a coil — handy for tracking down "which rung sets
  this bit" as programs grow.
- **Undo / Redo** — toolbar buttons; covers all structural edits (add/delete/move rungs,
  branches, contacts, outputs, pin mappings).
- **Rung copy / paste / reorder** — **Copy** a rung, then **Paste Rung** to append an
  independent duplicate; **▲ / ▼** reorder a rung within the scan order.
- Grid-based visual layout instead of the old plain list (still not a full free-form
  drag-and-drop wiring canvas — see note below).

## 3b. Build a program

1. **Ladder Editor tab** — click **New Rung** to add a rung. Each rung is:
   `branch OR branch OR ...  ->  one output element`, and each branch is a series
   (AND) chain of contacts. Click **+ Contact** / **+ Branch** to grow it.
   - Contact = `Mode` (Normally Open / Normally Closed) + `Kind` (I/Q/M/T/C) + `Index`.
   - Output = `Coil type` (Output / Set / Reset / TON / TOF / CTU / CTD) + address + Preset
     (milliseconds for timers, pulse count for counters).
   - `I` = physical input, `Q` = physical output, `M` = internal memory relay (no pin),
     `T` = timer done-bit, `C` = counter done-bit. A contact can reference `T`/`C` to use
     a timer or counter's done-bit elsewhere in the ladder, exactly like a real PLC.
2. **Pin Mapping tab** — for every `I` and `Q` address you used, add a row and give it
   a real Arduino pin number (and tick "Use INPUT_PULLUP" for buttons wired to ground).
3. Pick your **Board** FQBN (Uno/Nano/Mega/ESP32...) and your **Port**, then
   **Build & Upload**. The log panel at the bottom shows the arduino-cli output.
   **Build Only** compiles without touching the board (good for checking for errors).
   **Export .ino** saves the generated sketch if you want to open/tweak it in the
   normal Arduino IDE.

**Save Project / Open Project** stores your ladder as a `.json` file so you can keep
working on it later.

## Example: motor start/stop with seal-in (classic 3-wire control)

- Rung 1: branches = [ `I0` (NO, Start) ] OR [ `Q0` (NO, seal-in) ], both branches
  ANDed with `/I1` (NC, Stop) in series → output `Q0` (Output). Wire Start button to
  pin mapped as I0, Stop button to I1, motor relay/output to the pin mapped as Q0.

## Example: blink an LED every 1 second using a timer

- Rung 1: contact `/T0` (NC) → output `T0` (TON, preset 1000 ms)
- Rung 2: contact `T0` (NO) → output `Q0` (Output)
- Map `Q0` to the pin your LED is on. This is a standard ladder "flasher" circuit —
  T0's own done-bit disables itself once it times out, retriggering every scan cycle.

## 4. Build a Windows installer (setup.exe)

A ready-made **Inno Setup** script (`setup.iss`) is included at the repo root, so you
can produce a real `Setup.exe` with a Start Menu shortcut and uninstaller:

1. Install the free **Inno Setup** compiler: https://jrsoftware.org/isdl.php
2. From `LadderToArduino\LadderToArduino`, publish a single-file exe:
   ```
   dotnet publish -c Release -r win-x64 --self-contained true -p:PublishSingleFile=true -o publish
   ```
3. Move (or copy) the `publish` folder next to `setup.iss` (i.e. into the repo root,
   alongside `LadderToArduino.sln`), or edit the `Source:` paths in `setup.iss` to
   point at wherever `publish` ended up.
4. Right-click `setup.iss` → **Compile** (or open it in Inno Setup and press F9).
5. Your installer appears at `Output\LadderToArduino-Setup.exe` — double-click to
   install it like any normal Windows app.

## Notes / limits

- **No free-form drag-and-drop wiring canvas.** Rungs/branches/contacts are placed and
  reordered with buttons and dropdowns rather than dragging elements around a blank canvas
  and drawing wires — that's a materially bigger UI undertaking (custom hit-testing,
  snapping, wire routing) and was left out of this pass. If you want it, the model layer
  (`Rung`/`Branch`/`Contact`/`CoilOutput`) already cleanly separates data from rendering, so
  a canvas-based `ItemsControl` replacement can be dropped in later without touching
  `CodeGenerator`/`Simulator`.
- Scan is a plain `loop()` with a configurable delay (`ScanDelayMs` in the project,
  default 10 ms) — fine for switches/relays/timers/analog sensors in the tens-of-ms range,
  not for microsecond-precision control.
- `arduino-cli` must be installed separately; the app just shells out to it
  (`compile` then `upload`). If you get a "could not launch arduino-cli" error, put
  its full path in `ArduinoCliService.ArduinoCliExePath` in
  `Services/ArduinoCliService.cs`, or add it to your Windows PATH and restart the app.
- The Live Monitor and Build & Upload both need the COM port; only one can hold it at a
  time (the app stops the monitor automatically before uploading, but stop it manually
  before opening the Arduino IDE's own Serial Monitor too).

## Project layout

```
LadderToArduino.sln
setup.iss                    - Inno Setup script -> Setup.exe (see section 4 above)
LadderToArduino/
  MainWindow.xaml(.cs)        - UI: editor, pin mapping, simulate, monitor, cross-ref, undo/redo
  Models/
    Enums.cs                  - AddressKind (incl. AnalogInput/PWMOutput), ContactMode,
                                 ComparatorOp, CoilType (incl. AnalogOutput), AnalogSource
    LadderModels.cs           - Contact, Branch, CoilOutput, Rung, PinMapping, LadderProgram
    SimViewModels.cs          - SimBoolItem/SimIntItem/XRefRow (small UI-bindable wrappers)
  Services/
    CodeGenerator.cs          - LadderProgram -> Arduino .ino text (+ live-monitor printState())
    Simulator.cs               - mirrors CodeGenerator's semantics in C#, for the Simulate tab
    SimulationViewModel.cs     - bridges Simulator <-> the Simulate tab's bindable collections
    MonitorService.cs          - reads the sketch's Serial state line for the Live Monitor tab
    ArduinoCliService.cs       - shells out to arduino-cli (compile/upload/list ports)
    ProjectFileService.cs      - save/load project as JSON, + undo/redo & rung-clone helpers
```
