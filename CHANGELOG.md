# Changelog

All notable changes to `CKL.Libs.SiteBuilder` are documented in this file.

The format follows [Keep a Changelog](https://keepachangelog.com/en/1.1.0/),
and this project adheres to [Semantic Versioning](https://semver.org/).

## [1.1.0] - 2026-07-14

Ported the C# renderer (Markdig pipeline, offline vendored Mermaid, folder→
`<details>` navigation, `.md`→`.html` link rewriting) and introduced the
in-memory `SiteModel`/`SiteAssembler` pipeline core, replacing the placeholder
public surface with the real `SiteBuilder`/`SiteBuilderOptions` facade. The
assembler emits an in-memory site model — no materialised staging folder —
and the renderer reads each source directly and lazily at render time,
copying non-markdown assets straight to the output. Implements requirements
R-02 … R-07; see `docs/requirements/README.md` and `docs/architecture.md`.

## [1.0.0] - 2026-07-14

Initial bootstrap: an empty, convention-correct skeleton (solution, projects,
local-build gate, packaging) with a minimal placeholder public surface. No
pipeline logic yet — see `docs/requirements/README.md` and
`docs/architecture.md` for the intended shape.
