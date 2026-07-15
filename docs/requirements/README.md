# Requirements — CKL.Libs.SiteBuilder

Requirements for the SiteBuilder core library: the markdown→HTML pipeline.
IDs are stable and never reused; a changed requirement is edited in place.
Provenance: [ckl-builder ADR-0019](https://github.com/ckluth/ckl-builder/blob/main/docs/decisions/0019-reimplement-site-builder-as-ckl-toolset.md).

### R-01: Three separable pipeline stages in one library
The library must carry the whole pipeline as three separable internal components
— index/resolve, assemble, render — in a single library, not as separate
libraries.

### R-02: In-memory site model
The assembler must emit an in-memory site model whose nodes each carry a source
path, a navigation position, and any overrides, plus synthetic node-index,
landing, and search nodes — never a materialised staging folder on disk.

### R-03: Lazy, direct source reads at render time
The renderer must read each source document directly and lazily at render time
and stream HTML to the output; non-markdown assets must be copied straight to the
output.

### R-04: Markdig rendering pipeline
The library must render markdown to HTML through the ported Markdig pipeline.

### R-05: Offline Mermaid diagrams
The library must render Mermaid diagrams from a vendored, offline copy — no CDN
and no npm dependency at build or run time.

### R-06: Folder-structure navigation tree
The library must derive a collapsible `<details>` navigation tree from the site
model's folder structure.

### R-07: Markdown link rewriting
The library must rewrite `.md` links to their generated `.html` targets.

### R-08: In-memory index, not persisted
The library must derive its mechanical document index in memory on each run and
must not persist it (no `docs-index.json` on disk).

### R-09: Configurable scan roots
The library must read, from hand-authored YAML, the set of repositories/roots to
scan for markdown.

### R-10: Single authoritative navigation path
The library must assemble every build through one authoritative navigation map
that defines the output structure — allowing documents to be rearranged, skipped,
or retitled — and must not treat source layout as the navigation. There is no
second, map-less assembly path: the map is always the basis of the site.

### R-11: Navigation map generated when absent, then hand-owned
When no navigation-map file exists at the configured (or default) location, the
library must generate one as a scaffold derived from the source structure — into
the current working directory — and then build from it. The generated file is a
normal, committable project artifact; once present it is hand-owned and never
overwritten. Drift detection (R-12) applies to every build.

### R-12: Unplaced-document drift detection
The library must report/flag any source document absent from the navigation map
as "unplaced" and must never silently drop it.

### R-13: Metadata resolution by precedence
The library must resolve each document's metadata through the precedence chain
structure → frontmatter → AI-inferred residue into an in-memory index, with
structure authoritative over frontmatter, recomputed each run, carrying out the
policy set by ckl-builder ADR-0018.

### R-14: Replaceable stylesheet seam
The library must expose the theme/CSS as a replaceable seam accepting a custom
stylesheet.

### R-15: Configurable Mermaid theme seam
The library must expose the Mermaid theme as a configurable seam.

### R-16: Essential output configuration
The library must accept essential output configuration — at minimum the output
location and core output knobs.

### R-17: Deterministic, non-interactive operation
The library must operate deterministically, non-interactively, and fully
config-driven, so a headless caller can drive it end to end.

### R-18: Guaranteed synthesised landing page
The library must always emit a root landing page (`index.html`) that lists the
top-level pages and sections, so every generated site has a front door regardless
of the corpus. Provenance: [ckl-builder ADR-0021](https://github.com/ckluth/ckl-builder/blob/main/docs/decisions/0021-sitebuilder-single-generated-nav-path.md).

### R-19: No landing filename magic; explicit home designation
The library must not promote any document to the site home by filename —
`README.md` and `_index.md` carry no special status and are ordinary pages. A
hand-authored home is selected only by explicit designation of a document as home
in the navigation map, in which case it replaces the synthesised landing.
Provenance: ckl-builder ADR-0021.

### R-20: Complete navigation — no unreachable pages
The generated navigation map must place every discovered document — top-level
pages and landing-less folders included — so that no document renders to HTML
while being unreachable from the navigation. Provenance: ckl-builder ADR-0021.

### R-21: Configurable section-click behaviour
The library must expose a configuration option selecting how a navigation section
behaves: `expand` (default — the sidebar tree is the section overview, no
generated page) or `overview` (a generated per-folder listing page), with an
optional per-section override in the navigation map. Provenance: ckl-builder
ADR-0021.

### R-22: Unconditional search page
The library must always emit the offline search page over the in-memory metadata
index, on every build. Provenance: ckl-builder ADR-0021.

### R-23: Asset-copy ignore rule
The library must apply, when copying non-markdown assets, a built-in default
ignore set (dot-directories such as `.git`/`.vs`, and build/VCS outputs such as
`bin`/`obj`/`node_modules`) plus an optional config-driven exclude list, so
workspace cruft is never copied into the self-contained output. Provenance:
ckl-builder ADR-0021.

### R-24: Guarded output-directory clean
Before generating, the library must reconcile its output directory by a guarded
wholesale clean: it writes a marker into the output it owns; if the output
directory exists, is non-empty, and lacks the marker, the build must fail rather
than delete unrecognised data; if the marker is present, the directory is cleaned
and rebuilt, so no stale file from a previous build survives into the site.
Provenance: ckl-builder ADR-0021.
