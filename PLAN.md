# Stock Tracker .NET — Rewrite Plan

## Overview

Rewrite of [stock-tracker](https://github.com/nthewara88agent/stock-tracker) (Python/FastAPI + Streamlit) to **.NET 9** with C#, designed to run in containers.

## Current App (Python)

| Component | Tech | Purpose |
|-----------|------|---------|
| Backend API | FastAPI + SQLAlchemy | REST API for portfolio CRUD, price fetching, CGT calculations |
| Frontend | Streamlit | Dashboard with charts, portfolio management UI |
| Database | PostgreSQL | Holdings, price history, users |
| Auth | JWT (OAuth2 password bearer) | User authentication |
| Price Data | yfinance (.AX suffix) | ASX stock prices from Yahoo Finance |
| Containerisation | Docker Compose | Backend + Frontend + PostgreSQL |

### Key Features
- **Portfolio Management** — Add/edit/delete stock holdings (buy date, quantity, price, brokerage)
- **Live Prices** — Fetch from Yahoo Finance, store history
- **CGT Calculator** — Australian capital gains tax with 50% discount for >12 months
- **Dashboard** — Portfolio value, P&L, allocation charts, sparklines
- **Multi-user** — JWT auth with user isolation

---

## .NET 9 Architecture

### Tech Stack

| Layer | Technology | Notes |
|-------|-----------|-------|
| **Backend API** | ASP.NET Core 9 Minimal APIs | Replaces FastAPI |
| **ORM** | Entity Framework Core 9 | Replaces SQLAlchemy |
| **Database** | PostgreSQL (Npgsql) | Same as current |
| **Auth** | ASP.NET Core Identity + JWT | Replaces custom JWT |
| **Frontend** | Blazor WebAssembly (WASM) | Replaces Streamlit |
| **Charts** | Radzen.Blazor or MudBlazor | Replaces Plotly |
| **Price Data** | HttpClient + Yahoo Finance API | Replaces yfinance |
| **Containerisation** | Docker multi-stage builds | .NET 9 slim images |
| **Testing** | xUnit + Moq | Unit + integration tests |

### Why These Choices
- **Minimal APIs** over Controllers — less boilerplate, similar to FastAPI's developer experience
- **Blazor WASM** — full C# frontend, no JavaScript, runs in browser, single language stack
- **EF Core** — mature ORM, migrations, LINQ queries
- **ASP.NET Identity + JWT** — battle-tested auth, same JWT approach as current app

---

## Project Structure

```
stock-tracker-dotnet/
├── src/
│   ├── StockTracker.Api/              # Backend API project
│   │   ├── Program.cs                 # App entry, Minimal API endpoints
│   │   ├── Data/
│   │   │   ├── AppDbContext.cs         # EF Core DbContext
│   │   │   └── Migrations/            # EF Core migrations
│   │   ├── Models/
│   │   │   ├── Holding.cs             # Stock holding entity
│   │   │   ├── PriceHistory.cs        # Price history entity
│   │   │   └── User.cs               # User entity (Identity)
│   │   ├── Dtos/
│   │   │   ├── HoldingDto.cs          # Request/response DTOs
│   │   │   ├── PriceDto.cs
│   │   │   └── CgtReportDto.cs
│   │   ├── Services/
│   │   │   ├── PriceService.cs        # Yahoo Finance price fetching
│   │   │   ├── CgtService.cs          # Australian CGT calculations
│   │   │   └── PortfolioService.cs    # Portfolio aggregation logic
│   │   ├── Auth/
│   │   │   └── JwtTokenService.cs     # JWT generation/validation
│   │   ├── Endpoints/
│   │   │   ├── AuthEndpoints.cs       # /api/auth/* routes
│   │   │   ├── HoldingEndpoints.cs    # /api/holdings/* routes
│   │   │   └── PriceEndpoints.cs      # /api/prices/* routes
│   │   ├── appsettings.json
│   │   ├── Dockerfile
│   │   └── StockTracker.Api.csproj
│   │
│   ├── StockTracker.Web/              # Blazor WASM frontend
│   │   ├── Program.cs
│   │   ├── Pages/
│   │   │   ├── Dashboard.razor        # Main portfolio dashboard
│   │   │   ├── Holdings.razor         # Add/edit holdings
│   │   │   ├── CgtReport.razor        # CGT calculator page
│   │   │   └── Login.razor            # Login/register
│   │   ├── Components/
│   │   │   ├── PortfolioChart.razor    # Allocation pie chart
│   │   │   ├── HoldingTable.razor      # Holdings data grid
│   │   │   └── SparkLine.razor         # Mini price charts
│   │   ├── Services/
│   │   │   └── ApiClient.cs           # Typed HttpClient for API
│   │   ├── wwwroot/
│   │   ├── Dockerfile
│   │   └── StockTracker.Web.csproj
│   │
│   └── StockTracker.Shared/           # Shared DTOs/models
│       ├── Dtos/
│       └── StockTracker.Shared.csproj
│
├── tests/
│   ├── StockTracker.Api.Tests/        # API unit + integration tests
│   └── StockTracker.Web.Tests/        # Blazor component tests
│
├── docker-compose.yml                 # API + Web + PostgreSQL
├── .github/
│   └── workflows/
│       └── ci.yml                     # Build + test + Docker push
├── StockTracker.sln
├── .dockerignore
├── .gitignore
└── README.md
```

---

## Implementation Phases

### Phase 1 — Foundation
- [ ] Solution structure + project scaffolding
- [ ] EF Core DbContext + models (Holding, PriceHistory, User)
- [ ] PostgreSQL connection + initial migration
- [ ] Docker Compose (API + PostgreSQL)
- [ ] Health check endpoint

### Phase 2 — Auth
- [ ] ASP.NET Identity setup with PostgreSQL
- [ ] JWT token generation + validation
- [ ] Register/login Minimal API endpoints
- [ ] Auth middleware for protected routes

### Phase 3 — Core API
- [ ] Holdings CRUD endpoints (Minimal APIs)
- [ ] Price fetching service (Yahoo Finance via HttpClient)
- [ ] Price history storage + retrieval
- [ ] Portfolio summary endpoint (total value, P&L, allocation)

### Phase 4 — CGT Calculator
- [ ] CGT service — replicate Python logic in C#
- [ ] 50% CGT discount for holdings >12 months
- [ ] CGT report endpoint with per-holding breakdown

### Phase 5 — Blazor Frontend
- [ ] Blazor WASM project setup
- [ ] Login/register pages
- [ ] Dashboard — portfolio value, P&L cards, allocation chart
- [ ] Holdings table with add/edit/delete
- [ ] Price sparklines
- [ ] CGT report page

### Phase 6 — Polish & Deploy
- [ ] Multi-stage Dockerfiles (build + runtime)
- [ ] Docker Compose (API + Web + PostgreSQL)
- [ ] GitHub Actions CI (build, test, Docker image)
- [ ] Error handling + loading states
- [ ] README with setup instructions

---

## API Endpoints (matching current app)

```
POST   /api/auth/register          # Register new user
POST   /api/auth/login             # Login, returns JWT

GET    /api/holdings               # List user's holdings
POST   /api/holdings               # Add holding
PUT    /api/holdings/{id}          # Update holding
DELETE /api/holdings/{id}          # Delete holding

POST   /api/prices/fetch           # Fetch latest prices from Yahoo
GET    /api/prices/history/{ticker} # Get price history

GET    /api/portfolio/summary      # Portfolio value, P&L, allocation
GET    /api/cgt/report             # CGT report with tax calculations
```

---

## Key Differences from Python Version

| Aspect | Python | .NET |
|--------|--------|------|
| API style | FastAPI decorators | Minimal API `app.MapGet()` |
| ORM | SQLAlchemy + Alembic | EF Core + Migrations |
| Frontend | Streamlit (server-rendered) | Blazor WASM (client-side) |
| Auth | Manual JWT + passlib | ASP.NET Identity + built-in JWT |
| Price lib | yfinance package | Raw Yahoo Finance API via HttpClient |
| Types | Pydantic models | Records + DTOs |
| DI | Manual | Built-in .NET DI container |
| Config | python-dotenv | appsettings.json + IConfiguration |

---

## Ready to Build

Approve this plan and I'll start with **Phase 1** — scaffolding the solution, models, and Docker setup.
