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
   and the authoritative navigation map, which is now the single, always-used
   assembly path (there is no map-less/legacy path). A root `index.html`
   landing is always present: either a nav-map entry explicitly designated
   `home: true`, or — when none is designated — a synthesised landing page
   listing the top-level pages and sections. There is no filename magic:
   `README.md`/`_index.md` are ordinary documents unless explicitly marked
   `home`. Each section (folder-derived nav group) either expands as a pure
   nav group with no generated page (`section: expand`, the default) or gets
   a generated overview/listing page (`section: overview`), configurable
   globally or per nav-map entry. A search page is always emitted,
   unconditionally. No staging folder is materialised on disk.
3. **Render** — walks the site model, reads each source document directly and
   lazily at render time, and streams HTML to the output. Markdown is rendered
   through a Markdig pipeline; a collapsible `<details>` navigation tree is
   derived from folder structure; `.md` links are rewritten to their `.html`
   targets; Mermaid diagrams are rendered from a vendored, offline copy.
   Non-markdown assets are copied straight to the output, skipping a built-in
   ignore set (`.git`, `.vs`, `.vscode`, `.idea`, `bin`, `obj`,
   `node_modules`, and any dotfile/dot-directory segment) plus any
   configured excludes.

## Configuration

Configuration is hand-authored YAML with two roles: (i) the repositories/roots to
scan, and (ii) an **authoritative navigation/site map** that defines the *output*
structure — documents may be rearranged, skipped, or retitled — deliberately not
a mirror of source layout. The navigation map is always resolved: when the
config omits a `nav:` key, the map path defaults to `nav.yml` in the current
working directory and is generated there as a **complete** scaffold (every
discovered document placed, with no README/`_index`-as-landing special
casing) on first run, then hand-owned thereafter. A source document absent
from the map is reported as "unplaced" (drift detection) and never silently
dropped. Location and navigation are thereby kept as separate concerns.
A nav-map section may also use a wildcard `source:` pattern, which expands
**at assembly time** against the discovered relative source set, with optional
literal `exclude:` paths treated as a section-scoped drift-acknowledgment: they
are omitted from that wildcard's own rendering and never reported as drift, but
are not exclusively claimed, so another section may still place the same file
via its own explicit `source:` entry. A single-file (non-wildcard) nav entry may
also leave `title:` empty, in which case the title is inherited from the
discovered document (its first H1, or a formatted filename) at assembly time.
The assembler also carries a general non-fatal warnings channel (separate from
drift) for ignored intros, unmatched wildcard excludes, and similar
author-feedback cases.

A `section:` config key (`expand` default, or `overview`) sets the default
section-click behaviour; a nav-map entry may override it per-subtree. An
`intro:` config key adds optional Markdown above the synthesised landing-page
listing, and a section/wildcard entry may add its own `intro:` above a
generated `overview` page. An `assets: { exclude: [...] }` config key adds
directory-name/glob patterns to the built-in asset-copy ignore set. The output
directory is guarded: a first build creates or reuses an empty directory; a
rebuild over a directory carrying this tool's `.sitebuilder` marker is cleaned
and reconciled; a non-empty directory without the marker is refused, deleting
nothing.

## Seams

The theme/CSS is a replaceable seam accepting a custom stylesheet, and the
Mermaid theme is a configurable seam. Output configuration covers at least the
output location and core output knobs. The library operates deterministically,
non-interactively, and fully config-driven, so a headless caller (the
CKL.Apps.SiteBuilder CLI) can drive it end to end.

## Status

The renderer, in-memory assembler, and metadata-resolution pass are real, and
the library is now fully config-driven around a single, always-generated
navigation path. The Markdig pipeline, offline Mermaid rendering,
`.md`→`.html` link rewriting, non-markdown asset copying with an ignore rule,
the metadata-resolution precedence pass, YAML config parsing, authoritative
nav-map assembly, always-on nav-map scaffolding/generation, drift reporting
for unplaced documents, the replaceable stylesheet seam, the configurable
Mermaid theme seam, a guaranteed synthesised (or home-designated) landing
page, configurable section-click behaviour, an unconditionally-emitted search
page, and a guarded output-directory clean are all implemented and exposed
through the public `SiteBuilder` / `SiteBuilderOptions` facade.

## Provenance

- Toolset design and scope: ckl-builder ADR-0019.
- Metadata-resolution precedence policy: ckl-builder ADR-0018.
- Single generated-nav path, guaranteed landing, section behaviour,
  unconditional search, asset ignore rule, guarded clean: ckl-builder
  ADR-0021.

(Pointers only — the decisions themselves are not restated here.)
