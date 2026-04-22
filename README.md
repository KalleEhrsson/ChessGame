# First-Person Chess (Unity)

A first-person 3D chess game built in Unity where the player explores a full-scale board, interacts with physical chess pieces, uses a tactical camera to plan moves, and plays against a local AI opponent.

## Project Overview

This project combines immersive first-person gameplay with standard chess rules.

- Walk around the board as if it were a physical arena.
- Interact directly with pieces in the world.
- Switch to a tactical view for clear move selection.
- Play against a local chess engine (Stockfish) without external APIs.

The goal is to create a portfolio-ready game experience that blends strategy, spatial presence, and clean system architecture.

## Core Features

### First-Person Movement
- Player-controlled movement around a full-scale chessboard environment.
- Camera and controls designed for in-world exploration of the board state.

### Tile-Based Board System (A1–H8)
- Board represented as an 8x8 tile grid with chess coordinates.
- Tile indexing and coordinate mapping used for gameplay logic.

### Piece System (Full Chess Setup)
- Standard initial piece placement for both sides.
- Piece data and ownership tracked independently from visual presentation.

### Interaction + Tactical Camera
- Direct world interaction with chess pieces.
- Tactical camera mode for precision move planning and selection.

### Move System
- Hover feedback for target tiles.
- Move highlighting for legal destinations.
- Piece movement execution with board state updates.

### Real Chess Rules (Current Scope)
- Turn-based move flow.
- Move legality checks with king safety enforcement.
- Illegal moves rejected before board state is committed.

## Current State

The following systems are currently implemented:

- First-person player movement and world interaction foundations.
- Tile-based board representation with coordinate-driven logic.
- Full initial chess piece setup and placement flow.
- Tactical camera and selection workflow.
- Move generation and tile highlighting/hover feedback.
- Turn system and legality validation, including king safety constraints.

## Roadmap

Planned milestones and next features:

- Checkmate and stalemate detection.
- Advanced chess rules:
  - Castling
  - En passant
  - Promotion
- Full Stockfish gameplay loop integration for AI turns.
- Clear UI/gameplay feedback (turn indicator, legal/illegal move feedback, game state messages).
- Piece crushing/capture presentation mechanic.
- Timer system (e.g., blitz/rapid/classical presets).
- Overall polish: animation pass, sound design, and visual feedback improvements.

## AI — Stockfish

This project uses **Stockfish** as its chess engine for AI decision-making.

How the integration works (high level):

1. Current board state is serialized to **FEN**.
2. The FEN is sent to Stockfish through the **UCI** protocol.
3. Stockfish returns the **best move**, which is then applied in-game.

Key points:

- Runs fully **locally** on the player's machine.
- Requires **no external API** and no online service dependency.

Official Stockfish repository:

- https://github.com/official-stockfish/Stockfish

### Stockfish License

Stockfish is licensed under the **GNU General Public License v3.0 (GPLv3)**.

If Stockfish is distributed with this project:

- A copy of the GPLv3 license must be included with the distribution.
- The corresponding Stockfish source code must be made available (for example, by including it or providing a clear source link).

For exact terms, refer to the official Stockfish repository and license text.

## Architecture

The project architecture follows a tile-centric, system-separated approach:

- **Tiles are the source of truth** for board occupancy and move resolution.
- **System separation** keeps responsibilities clear:
  - Move generation
  - Legality/validation
  - Visual feedback and interaction
- **Auto-wiring preference** is used where practical to reduce manual Inspector setup and keep scene configuration lightweight.

## Tech Stack

- **Engine:** Unity
- **Language:** C#
- **Input:** Unity Input System
- **AI Engine:** Stockfish (local executable, UCI)

## Project Status

Active development. Core gameplay foundations are in place, with rule completeness, AI flow hardening, and presentation polish currently prioritized.
