## Project: NGDocs — Local Documentation & Database UI

### Context

GraphLib currently includes a `docs/` directory containing static HTML documentation.

We are **not modifying or replacing** that directory.

Instead, we are creating a **new sibling folder**:
fa
```
NGDocs/
```

NGDocs is a **local-only Angular SPA** that serves as:

* a living manual
* a visual companion to the GraphLib CLI
* a database exploration and editing tool

This is **not** a production web app.

---

## High-level goals

1. Preserve existing docs (non-breaking)
2. Provide a modern SPA UI for:

   * reading documentation
   * viewing work items
   * inspecting/editing the SQLite database
3. Keep everything **local-only**
4. Avoid .NET for the web layer
5. Favor clarity, explicitness, and debuggability over automation

Tone: practical, developer-facing, slightly funky.

---

## Folder structure (target)

```
docs/          (unchanged)
NGDocs/
  ├─ app/      (Angular SPA)
  ├─ server/   (Node / Express API)
  ├─ data/     (json-server files)
  ├─ package.json
  └─ README.md
```

---

## SPA model (Angular)

NGDocs is a **single-page application** using:

* Angular
* Bootstrap
* (later) Chart.js

Routes/pages that must always exist (even without a database):

* `/`               → Index
* `/getting-started`
* `/troubleshooting`
* `/work-items-board`

These pages can:

* embed static HTML
* load content dynamically
* or reference existing docs as needed

We are **not rewriting the docs yet** — this is display-first.

---

## Work Items Board (Phase 1)

The **Work Items Board** is **data-driven** using `json-server`.

### Data source

* `json-server` runs locally
* Backed by a JSON file under `NGDocs/data/`

Example work item shape:

```json
{
  "id": "GLC-001",
  "project": "GraphLib.Console",
  "type": "User Story Template",
  "priority": 3,
  "title": "CLI: Add --fileTypes allowlist override",
  "summary": "Allow operators to pass a temporary allowlist at runtime.",
  "tags": ["cli", "args", "settings"],
  "files": ["src/GraphLib.Console/Cli/Args.cs"],
  "asA": "As an Operator",
  "iNeed": "to override allowed file extensions",
  "soThat": "I can test new extensions safely.",
  "acceptance": [],
  "notes": []
}
```

### Requirements (Phase 1)

* Read-only display
* List view
* Basic sorting / filtering
* Detail view for a single item

No editing required yet.

---

## Database Browser (Mandatory backend)

### Critical constraint (read carefully)

A browser-based Angular SPA **cannot** read a local SQLite file at paths like:

```
src\GraphLib.Console\bin\Debug\net8.0\Data\GraphLib.db
```

Therefore:

> **A local Node/Express API is mandatory for database access.**

This is non-negotiable due to browser security constraints.

---

## Database selection model

The UI **does not auto-detect** databases.

Instead, the user explicitly selects one of two known paths:

* **Debug**

  ```
  src\GraphLib.Console\bin\Debug\net8.0\Data\GraphLib.db
  ```

* **Release**

  ```
  src\GraphLib.Console\bin\Release\net8.0\Data\GraphLib.db
  ```

### Requirements

* Selection via radio buttons or dropdown
* Always display the full selected path
* If the file does not exist:

  * show a clear message
  * disable DB navigation features

No guessing. No magic.

---

## Express API responsibilities

The Express server:

* runs locally
* opens the selected SQLite DB file
* exposes safe HTTP endpoints for the Angular app

### Required endpoints (initial design)

* `GET /api/db/status`
* `GET /api/db/tables`
* `GET /api/db/table/:name`
* `POST /api/db/table/:name/update`

### Philosophy

* CRUD via UI, not SQL
* Primary-key based updates
* SQL knowledge is **not required** for normal use

---

## SQL Query Runner (advanced, optional)

* Available behind an “advanced” UI section
* SELECT queries by default
* Write queries only if explicitly enabled
* Results shown in a table

This is for debugging and exploration only.

---

## Visualizations

Use Chart.js for simple, explanatory charts such as:

* File extension counts
* Conversion success vs failure
* Top source → target conversions

These are **exploratory**, not compliance dashboards.

---

## Development workflow

`NGDocs/package.json` must include:

* Angular dev server
* json-server
* Express API

All started via:

```bash
npm run dev
```

Use `concurrently` as needed.

---

## Non-goals (important)

* No authentication
* No hosting
* No deployment pipeline
* No .NET web apps
* No filesystem scanning heuristics
* No production hardening

If it feels like a product, you’ve gone too far.

---

## Deliverables (initial)

1. Folder structure scaffolded
2. Angular routes/pages exist
3. json-server running with sample data
4. Express API stubbed with DB selection logic
5. README.md explaining how the system works

Implementation comes later — **explain first, then build**.