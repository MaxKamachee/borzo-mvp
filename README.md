# Borzo MVP: Agentic SolidWorks Assistant

## Overview
Borzo is an agentic workflow assistant for SolidWorks, enabling conversational, reversible, and logged design actions through a modern chat UI. This MVP is structured for easy handoff—only SolidWorks-specific add-in code remains for final integration.

---

## Monorepo Structure
- `/client` — React/TypeScript chat UI
- `/backend` — Python FastAPI microservice (intent parsing, mock SW logic)
- `/solidworks-addin` — C# .NET Add-in (to be completed for SW integration)
- `/assets` — STEP files, airfoil data, etc.

---

## MVP Plan
1. Repo & Project Setup: Monorepo scaffolded (Done).
2. SolidWorks Add-In Foundation: C# COM add-in scaffold, Task Pane, CommunicationBridge implemented with HTTP+COM skeleton and OS guards for non-Windows (Done).
3. React Task Pane UI: Chat UI, `handleSend` cases wired for airfoil via `window.external`, propulsion, CG, DRC (Done).
4. FastAPI Microservice: Endpoints `/airfoil` (using `airfoils`+`numpy`), `/propulsion`, `/cg`, `/drc`, `/agent` implemented (Done).
5. Airfoil Sketch Generator: External service approach—frontend calls COM → C# fetches coords from FastAPI → `SketchCoords` draws spline (Done).
6. Propulsion Selector & STEP Insertion: Stub and API methods defined; actual STEP scan+insert to implement on Windows (In Progress).
7. Live CG Tracker: Endpoint in place; C# and React stubs ready (Pending).
8. Static DRC: Endpoint in place; UI stubbed (Pending).
9. Logging & Metrics: Planned; code comments in place (Pending).
10. Testing, Polish, & Marketing: Ready for integration tests and handoff (Next).

---

## Backend API Contract
- **Endpoint:** `/agent`
- **Request:** `{ "message": "Generate NACA 2412 airfoil" }`
- **Response:** `{ "status": "success", "result": "Airfoil generated (mock)", ... }`
- **Mock Logic:** All SW actions simulated for now—swap in real C# add-in calls post-handoff.

---

## Handoff Instructions
1. Enable COM integration in `/solidworks-addin`:
   - Remove or adjust `RuntimeInformation.IsOSPlatform(OSPlatform.Windows)` guards in `CommunicationBridge.cs` and `AirfoilSketcher.cs` to allow COM calls on Windows.
   - Implement `GenerateAirfoil(naca, chord)` in C# by forwarding to `AirfoilSketcher.GenerateAirfoil` and sketching via SolidWorks API.
   - In `PropulsionSelector`, implement STEP file lookup in `/assets/steps` and use `PartDoc.Extension.InsertPart` to insert the selected propulsion model.
   - Implement `GetCG()` COM method to compute CG via SolidWorks Mass Property API and return JSON.
   - Implement `CheckDRC(partId)` COM method to run design-rule checks and return violation data.
2. Build & register the add-in:
   - Use Visual Studio or `dotnet build` targeting .NET Framework 4.x (matching SolidWorks).
   - Register COM component: e.g. `regasm /codebase bin\Release\net48\BorzoAddin.dll` and `regsvr32 BorzoAddin.dll`.
3. In backend, replace mock SW API logic with real calls to the registered COM add-in via the bridge.
4. Validate round-trip: React → Backend → C# Add-in → SolidWorks → Backend → React.

---

## Developer Notes (Mac/Linux)
- The C# add-in uses `RuntimeInformation` guards to skip COM calls on non-Windows.
- You can build and run backend & client fully on macOS.
- On Windows with SolidWorks, remove guards to enable real actions.
### Windows/COM Integration Notes
- Ensure SolidWorks (2019+) and Visual Studio (.NET Framework 4.x) are installed.
- After building the add-in, register the assembly:
  ```powershell
  cd solidworks-addin/BorzoAddin/bin/Release/net48
  regasm /codebase BorzoAddin.dll
  regsvr32 BorzoAddin.dll
  ```
- Verify the COM GUIDs in `BorzoAddin.csproj` match those expected by SolidWorks.
- Remove or adapt non-Windows guards in `CommunicationBridge.cs` so `external` calls reach SolidWorks APIs.

---

## Contact & Further Work
- Expand logging, error handling, and testing as needed.
- For questions, see code comments or contact the original developer.
