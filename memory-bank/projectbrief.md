# Project Brief

**Quantira Platform** — AI-assisted financial portfolio tracking and analysis.

## What It Is
A .NET 10 + React platform where users manage portfolios, track market data across multiple providers, receive intelligent alerts, and interact with an AI assistant for contextual portfolio analysis.

## Architecture
Clean Architecture, 5 backend layers:
`Domain → Application → Infrastructure / Infrastructure.AI → WebAPI`

Frontend: Vite + React 19 + TypeScript — lives in `client/`.

## Core Capabilities
| Domain | What it does |
|---|---|
| Portfolios | CRUD, position tracking, trade history |
| Assets | Symbol lookup, price tracking |
| Market Data | Multi-provider OHLCV, technical indicators |
| Alerts | Rule-based triggers, notification delivery |
| Chat / AI | Claude-backed portfolio context analysis |

## Success Criteria
- Dependency direction never violated
- MediatR pipeline as the only application entry point
- Build passes with 0 errors, 0 warnings
- Critical flows covered by tests
