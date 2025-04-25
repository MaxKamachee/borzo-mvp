# Borzo SolidWorks Add-in

## Overview
This folder is for the C# .NET COM add-in that will connect SolidWorks to the Borzo agentic workflow.

## To Do (for handoff):
- Scaffold C# add-in project
- Register Task Pane and load React UI
- Implement API endpoints to:
  - Generate airfoil sketches
  - Insert propulsion assets
  - Track mass/CG
  - Run DRC checks
- Communicate with backend (REST, WebSocket, or COM)

## Integration
- Replace mock SW API calls in backend with real requests to this add-in
- See root README for API contract and system flow

## Notes
- Add-in code is not required for MVP testing—implement when ready for SolidWorks integration.
