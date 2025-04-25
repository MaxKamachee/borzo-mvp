# Borzo Client (React UI)

## Overview
Modern, defense-tech inspired chat UI for SolidWorks agentic workflow. Built with React, TypeScript, Tailwind CSS, shadcn/ui, and Framer Motion.

## Features
- Sleek chat bar and send button (shadcn/ui)
- Responsive, flat dark theme (Anduril-like)
- Parameter/status panel
- Sends commands to backend and displays responses

## Development
- `npm install`
- `npm start` (runs on localhost:3000)
- Edit `src/App.tsx` and `src/App.css` for UI changes

## API
- POST chat commands to `/agent` endpoint (see root README for contract)

## Handoff
- No SolidWorks dependencies—fully testable standalone
- Ready for integration with backend and C# add-in

See [../README.md](../README.md) for full system architecture and handoff notes.
