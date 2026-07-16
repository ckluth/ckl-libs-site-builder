# Changelog

All notable changes to `CKL.Libs.SiteBuilder` are documented in this file.

The format follows [Keep a Changelog](https://keepachangelog.com/en/1.1.0/),
and this project adheres to [Semantic Versioning](https://semver.org/).

## [1.4.1] - 2026-07-16

Fixes broken search-result links. The search page lives at `search/index.html`,
but result URLs in its embedded JSON were emitted relative to the site root
(e.g. `ideas/foo.html`), so clicking a result resolved to
`search/ideas/foo.html` and 404'd. URLs are now computed relative to the search
page's own directory (e.g. `../ideas/foo.html`), matching the sidebar nav links.

## [1.4.0] - 2026-07-15

Collapses SiteBuilder onto a single, always-generated navigation path: the
map-less/legacy assembly branch is removed, and a missing `nav:` config now
defaults to `nav.yml` in the current working directory, generated as a
**complete** scaffold (every discovered document placed — no README/`_index`
special casing). Guarantees a browsable root landing page: an entry may be
explicitly designated `home: true`, or a landing is synthesised automatically
listing the top-level pages and sections; there is no filename magic. Adds a
configurable section-click behaviour (`section: expand` default, or
`overview`, overridable per nav-map entry). The search page is now emitted
unconditionally on every build. Applies a built-in asset-copy ignore rule
(`.git`, `.vs`, `.vscode`, `.idea`, `bin`, `obj`, `node_modules`, and any
dotfile/dot-directory segment) plus a configurable `assets.exclude` list.
Guards the output directory: creates or reuses an empty directory, cleans a
directory carrying this tool's `.sitebuilder` marker from a prior run, and
refuses (deleting nothing) to touch an unmarked non-empty directory.
Implements requirements R-10 (edited), R-11 (edited), R-18, R-19, R-20, R-21,
R-22, R-23, and R-24; see `docs/requirements/README.md`, `docs/architecture.md`,
and `docs/reference/yaml-schema.md`.

## [1.3.0] - 2026-07-15

Added hand-authored YAML configuration for scan roots and output location, plus
theme seams for a replaceable stylesheet and a configurable Mermaid theme.
Added the authoritative navigation map, its first-run scaffold generator, and
drift reporting for unplaced documents. The assembler now supports map-driven
reorder/retitle/skip, synthesises section index pages and an offline search page
over the in-memory metadata index, and exposes the drift report through the
config-driven `SiteBuilder.Build(...)` entry point. Implements requirements
R-09, R-10, R-11, R-12, R-14, R-15, R-16, and R-17, plus the remaining
search-node half of R-08; see `docs/requirements/README.md`,
`docs/architecture.md`, and `docs/reference/yaml-schema.md`.

## [1.2.0] - 2026-07-15

Added the metadata-resolution pass (`CKL.Libs.SiteBuilder.Metadata`): structural
extraction from location and decision-trail header fields (`type`, `title`,
`date`, `state`, `tags`), a frontmatter overlay for the residue (lower
precedence than structure — a contradicting frontmatter value is surfaced as a
defect, never an override), and an injectable `IMetadataInference` AI seam with
a deterministic `NoOpMetadataInference` default so resolution stays fully
offline unless a caller supplies a real implementation. Every document is
resolved into an in-memory `MetadataIndex`, built fresh on each run and never
persisted to disk, and wired into `SiteAssembler` so each `SiteNode.Overrides`
now carries its resolved metadata — render output is unchanged. Implements
requirements R-08 (in-memory half) and R-13; see
`docs/requirements/README.md` and `docs/architecture.md`.

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
