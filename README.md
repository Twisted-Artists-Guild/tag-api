# ⚙️ tag-api

[Wiki](https://github.com/TwistedArtistsGuild/tag-api/wiki) • [Project Board](https://github.com/users/TwistedArtistsGuild/projects/1)

The backend engine for [TwistedArtistsGuild.com](https://twistedartistsguild.com) — powering artist listings, payments, governance, contracts, competitions, and more. Built using ASP.NET Core with Entity Framework, `tag-api` delivers resilient business logic and ethical tooling for a modern digital guild.

---

## 🔥 Introduction

Twisted Artists Guild is a digital cooperative focused on artist ownership, equity, and scalable infrastructure. The `tag-api` backend manages secure APIs for commerce, events, client interactions, and cooperative governance — designed to evolve with the needs of independent creators and artistic enterprises.

---

## 🚀 Getting Started

1. **Clone the repo**
   ```bash
   git clone https://github.com/TwistedArtistsGuild/tag-api.git
   cd tag-api
   ```
2. **Restore packages**
   ```bash
   dotnet restore
   ```
3. **Run the development server**
   ```bash
   dotnet run
   ```

### Requirements
- .NET SDK 9
- PostgreSQL
- Azure SDKs (identity, email, blob)
- Stripe & Mailgun for transactions and messaging

### Folder Structure
- `/Models` — Entity Framework data models
- `/Controllers` — RESTful endpoints
- `/Services` — Business logic, validation
- `/Workflows` — Governance, payroll, events, accounting
- `/Tests` — xUnit test suite

### API Guides
- [Venue import API](docs/venue-import-api.md) — staff bulk import for seeding venues (for the crawler)

---

## 🧪 Build and Test

To compile and run tests:
```bash
dotnet build
dotnet test
```

**Testing Includes:**
- Unit tests (xUnit + Moq)
- Integration coverage for payments, listings, contracts
- Target 70%+ coverage for core business logic

---

## 🤝 Contributing
We welcome contributors with a passion for ethical tooling and artist empowerment.

- Fork the repo
- Create your branch: `git checkout -b feat/my-feature-name`
- Use Conventional Commits
- Submit a PR linked to an issue and milestone

Before submitting:
- Follow .editorconfig style rules
- Use XML documentation in public methods
- Keep logic in services — avoid bloated controllers
- Use EF and avoid raw SQL unless scoped intentionally

---

## 🧭 Philosophy & License
We build systems to strengthen artist control, visibility, and fair compensation. Licensed under GPL v3.0 to guarantee creator protection and transparency.

Open source • Low-profit • Human-first

---

## 📣 Related Projects
- tag-web — Next.js frontend
- tag-api/wiki — backend docs and architecture
