# Architecture — CKL.Libs.SiteBuilder

SiteBuilder is a C# library that assembles scattered markdown across one or many
repositories into a single, navigable, self-contained static HTML site. This
document describes what the library is; it carries no decision-history — the
reasoning lives in the ckl-builder trail (see *Provenance*).

## Pipeline stages

The library carries the whole pipeline as **three separable internal components**
within a single library (not separate libraries):

1. **Index / resolve** — discovers markdown under the configured scan roots and
   resolves each document's metadata through a precedence chain (structure →
   frontmatter → AI-inferred residue) into an in-memory index. Structure is
   authoritative over frontmatter; the index is recomputed each run and never
   persisted to disk.
2. **Assemble** — produces an **in-memory site model** from the resolved index
   and the authoritative navigation map. Each node carries a source path, its
   navigation position, and any overrides; synthetic node-index, landing, and
   search nodes are added. No staging folder is materialised on disk.
3. **Render** — walks the site model, reads each source document directly and
   lazily at render time, and streams HTML to the output. Markdown is rendered
   through a Markdig pipeline; a collapsible `<details>` navigation tree is
   derived from folder structure; `.md` links are rewritten to their `.html`
   targets; Mermaid diagrams are rendered from a vendored, offline copy.
   Non-markdown assets are copied straight to the output.

## Configuration

Configuration is hand-authored YAML with two roles: (i) the repositories/roots to
scan, and (ii) an **authoritative navigation/site map** that defines the *output*
structure — documents may be rearranged, skipped, or retitled — deliberately not
a mirror of source layout. The navigation map is generated on first run as a
scaffold derived from the source structure, then hand-owned. A source document
absent from the map is reported as "unplaced" (drift detection) and never
silently dropped. Location and navigation are thereby kept as separate concerns.

## Seams

The theme/CSS is a replaceable seam accepting a custom stylesheet, and the
Mermaid theme is a configurable seam. Output configuration covers at least the
output location and core output knobs. The library operates deterministically,
non-interactively, and fully config-driven, so a headless caller (the
CKL.Apps.SiteBuilder CLI) can drive it end to end.

## Status

The renderer, in-memory assembler, and metadata-resolution pass are real, and
the library is now fully config-driven. The Markdig pipeline, offline Mermaid
rendering, `.md`→`.html` link rewriting, non-markdown asset copying, the
metadata-resolution precedence pass, YAML config parsing, authoritative nav-map
assembly, first-run nav-map scaffolding, drift reporting for unplaced
documents, the replaceable stylesheet seam, the configurable Mermaid theme
seam, and synthetic section-index and search pages are all implemented and
exposed through the public `SiteBuilder` / `SiteBuilderOptions` facade.

## Provenance

- Toolset design and scope: ckl-builder ADR-0019.
- Metadata-resolution precedence policy: ckl-builder ADR-0018.

(Pointers only — the decisions themselves are not restated here.)
