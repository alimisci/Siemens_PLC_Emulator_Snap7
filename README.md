# Siemens_PLC_Emulator_Snap7# S7 PLC Emulator (Snap7 S7Server + WinForms)

## Contents
- `S7Types.cs` — Data types (Bool, Byte, Word, Int, DWord, DInt, Real, String) and DB/field definitions, automatic offset calculation (following Siemens word-alignment convention)
- `S7ByteConverter.cs` — Big-endian byte ↔ type conversions (custom implementation, no dependency on library internals)
- `DbManager.cs` — Wraps `S7Server`; DB add/remove/rebuild logic
- `ProjectFile.cs` — Saves/loads DB definitions (and optionally live values) as JSON
- `MainForm.cs` — DB list, field designer grid, live value monitor/editor grid (refreshes every 200ms), and project save/open buttons
- `Program.cs`, `S7Emulator.csproj`

## Usage
1. Click "Add DB" and give it a number and a name.
2. In the "Field Designer" tab, add rows and pick a name/type for each field (enter a length for String fields).
3. Click "Apply (Rebuild)" — this recalculates the DB's offsets, rebuilds the buffer, and (if the server is running) re-registers it with `S7Server`.
4. Click "Start Server" to bring the emulator online (default port 102, all adapters — `Start()`).
5. In the "Live Values" tab, watch values update live and double-click a cell to edit it manually. When a real S7 client (your SCADA system, or a Snap7 client test) connects and reads/writes, this grid updates automatically too, since both share the same `byte[]` buffer.

## Project File (Save/Open)
- **New Project**: clears all current DB definitions.
- **Open...**: loads DB definitions (and, if present in the file, the last live values) from a `.json` file; replaces all currently loaded DBs.
- **Save / Save As...**: writes all DBs, their fields, and current buffer values (Base64) to a `.json` file. "Save" overwrites the previously opened/saved file; on first save it behaves like "Save As" and asks for a filename.
- The file format is plain JSON — you can edit it by hand or check it into version control (git). Example structure:

```json
{
  "formatVersion": "1.0",
  "databases": [
    {
      "number": 1,
      "name": "Station_01",
      "fields": [
        { "name": "CrateCount", "dataType": "Int", "stringLength": 20 },
        { "name": "DemandActive", "dataType": "Bool", "stringLength": 20 }
      ],
      "bufferBase64": "AAAAAAA="
    }
  ]
}
```

- For different scenarios (e.g. `16_stations_normal.json`, `16_stations_fault_test.json`) keep separate files and open whichever one you need.

