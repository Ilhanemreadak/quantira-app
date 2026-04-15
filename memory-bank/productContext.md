# Product Context

## Problem
Individual investors manage portfolios across scattered tools with no unified view, no automated alerts, and no way to quickly ask "why is my portfolio down today?" The analysis burden stays entirely manual.

## Solution
Quantira brings portfolio data, multi-source market prices, rule-based alerts, and an AI chat interface into a single platform. The AI layer has access to the user's actual portfolio context, making its answers actionable rather than generic.

## User Experience Goals
- Dashboard: instant portfolio overview with live P&L
- Alerts: fire at the right moment, deliver via email/notification
- Chat: ask natural language questions, get portfolio-aware answers
- Speed: Redis-cached queries keep the UI snappy even on large portfolios

## Product Constraints
- AI provider (currently Claude) must be swappable → isolated in `Infrastructure.AI`
- Market data providers (Binance, Yahoo, GoldApi) must be hot-swappable → factory pattern
- Multi-tenancy: all data scoped to `OwnerId` (ASP.NET Identity user ID)
