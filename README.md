# Stock Tracker .NET

ASX stock portfolio tracker built with .NET 9, Blazor WASM, and PostgreSQL.

## Features

- **Portfolio Management** — Add/edit/delete stock holdings
- **Live Prices** — Yahoo Finance ASX prices (`.AX` suffix)
- **Dashboard** — Portfolio value, P&L, allocation pie chart
- **CGT Calculator** — Australian capital gains tax with 50% discount (>12 months)
- **Multi-user** — JWT authentication with user isolation

## Tech Stack

| Layer | Technology |
|-------|-----------|
| Backend | ASP.NET Core 9 Minimal APIs |
| Frontend | Blazor WebAssembly + MudBlazor |
| Database | PostgreSQL + EF Core |
| Auth | ASP.NET Identity + JWT |
| Container | Docker Compose |

## Quick Start

```bash
docker compose up --build -d
```

- **Frontend:** http://localhost:5001
- **API:** http://localhost:5000
- **Health:** http://localhost:5000/api/health

## API Endpoints

```
POST /api/auth/register    — Register
POST /api/auth/login       — Login (returns JWT)
GET  /api/holdings         — List holdings
POST /api/holdings         — Add holding
PUT  /api/holdings/{id}    — Update holding
DELETE /api/holdings/{id}  — Delete holding
POST /api/prices/fetch     — Fetch latest prices
GET  /api/prices/history/{ticker}
GET  /api/portfolio/summary
GET  /api/cgt/report
```

## Architecture

```
StockTracker.Shared  — DTOs shared between API and Web
StockTracker.Api     — Backend API + EF Core + Identity
StockTracker.Web     — Blazor WASM frontend + MudBlazor
```
