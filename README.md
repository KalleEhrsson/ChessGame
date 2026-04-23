# First-Person Chess

A first-person 3D chess experience where the board is a physical space, not just a top-down UI.
You move through the arena, interact with pieces in-world, and switch to a tactical camera when precision matters.

## Why This Project Is Different

This is not trying to reteach chess rules. The focus is on **how chess feels** when translated into a first-person 3D game:

- Board-scale presence (you traverse the board as a player).
- Physical interaction layer for piece handling.
- Hybrid control loop: exploration view + tactical move view.
- Local AI pipeline through Stockfish (no online services).

## Core Features

### Immersive Movement + Interaction
- First-person movement across a full-scale board environment.
- Direct interaction with pieces in-world.

### Tactical Camera Workflow
- Fast switch from immersive view to tactical selection mode.
- Built for readable move planning without losing world context.

### Move Feedback Loop
- Hover feedback on tiles.
- Legal-move highlighting.
- Piece movement updates synced to board state.

### Rules Validation (Current Scope)
- Turn-based move flow.
- King-safety-aware legality checks.
- Illegal move rejection before state commit.

## Current State

### Implemented

- [x] First-person controller and interaction foundation.
- [x] Tile-based board model with coordinate-driven logic.
- [x] Full initial piece setup.
- [x] Tactical camera + selection flow.
- [x] Move generation with hover/highlight feedback.
- [x] Turn system + king-safety legality validation.

### In Progress

- [ ] End-to-end AI turn loop hardening.
- [ ] Match-state UX messaging.

## Roadmap

### Next Milestones

- [x] Checkmate / stalemate detection.
- [ ] Advanced rules: castling, en passant, promotion.
- [ ] Complete Stockfish turn integration.
- [ ] Stronger UI feedback (turn state, legality reasons, game state).
- [ ] Piece crushing/capture presentation mechanic.
- [ ] Timer modes (blitz / rapid / classical).
- [ ] Polish pass (animations, sound, feel).

## AI — Stockfish

This project uses **Stockfish** as the local chess engine.

Flow:

1. Export current board state to **FEN**.
2. Send position to Stockfish via **UCI**.
3. Read back the engine's best move and apply it in Unity.

Highlights:

- Runs **locally** on the machine.
- Requires **no API key** and no cloud dependency.

Official repository:

- https://github.com/official-stockfish/Stockfish

### Stockfish License

Stockfish is licensed under the **GNU General Public License v3.0 (GPLv3)**.

If you distribute this project with Stockfish included:

- Include a copy of the GPLv3 license.
- Make corresponding Stockfish source code available (bundle it or provide a clear source link).

For exact obligations, refer to the official Stockfish repository license text.

## Architecture

System design priorities:

- **Tile-first logic:** tiles own occupancy truth and move destinations.
- **Separation of concerns:**
  - move generation
  - move validation
  - presentation/visual feedback
- **Auto-wiring preference:** minimal Inspector plumbing where possible.

## Tech Stack

- Unity (C#)
- Stockfish (local executable over UCI)

## Project Status

Active development. The playable core is working; rule completeness, AI loop robustness, and presentation polish are the next targets.
