# API Tester

A Windows desktop tool for crafting HTTP requests and keeping every exchange. Think of it as a
lightweight, team-aware Postman: each request and its full response (headers, body, timing,
certificates) is archived in a local SQLite database, and sessions can sync between machines —
and teammates — through Azure Blob Storage or an Azure DevOps git repository.

Built with .NET 10 / WinForms. Ships as a single self-contained native executable (Native AOT) —
no .NET runtime needed on the machine that runs it.

## Features

**Sending requests**

- Any HTTP method, exact HTTP version selection, request headers and body editors with
  syntax highlighting (JSON/XML pretty-printing) via FastColoredTextBox.
- Header and URL autocomplete; environment-specific hosts are read from an optional `urls.txt`
  next to the executable (one URL per line, git-ignored).
- Client certificate authentication — pick a certificate from the Current User store per request.
- A **Repeat** box to fire the same request N times in a row.
- `Ctrl+R` resends the selected session's request.

**Inspecting responses**

- Status bar with HTTP version, duration and size; clickable hyperlinks inside headers.
- Full **timing breakdown** per request: DNS resolution, TCP connect, TLS handshake, request
  headers/content, response headers/content — collected from the `System.Net` EventSource.
- **Server certificate inspection**: subject, issuer, validity range and chain result, stored
  with every session.

**Session history**

- Every exchange is stored locally in SQLite; response bodies are Brotli-compressed.
- Grid with live text filter, groups, inline note editing, multi-row delete, and
  "Copy to" another profile.
- **Profiles**: separate databases and window layouts per project/environment, with session
  import/export between database files.

**Team sync** (optional, per profile)

- Row-level sync — only changed sessions travel, not the whole database. Runs silently on
  change, every 60 s, and on close.
- Targets: **Azure Blob Storage** (SAS token with `rcwdl`, or an account key where a proxy
  breaks SAS) or an **Azure DevOps git repository** (PAT with Code read & write).
- Optional **AES-256-GCM encryption** of everything published, keyed by a shared passphrase —
  request headers carry credentials, so the container should not hold them in plain text.

**Files tab**

- A file manager over the profile's remote store (blob container or DevOps repo): browse,
  upload/download, folders, rename/move, and two-way drag & drop with Explorer — including
  drag-out downloads that start at the drop, not at the drag.

**Text utilities**

- Base64 encode/decode and URL escape/unescape, pre-filled from the clipboard.


