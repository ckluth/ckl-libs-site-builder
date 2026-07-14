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

### R-10: Authoritative navigation/site map
The library must read, from hand-authored YAML, an authoritative navigation map
that defines the output structure — allowing documents to be rearranged, skipped,
or retitled — and must not treat source layout as the navigation.

### R-11: Scaffold-generated navigation map
The library must generate the navigation map on first run as a scaffold derived
from the source structure, after which the map is hand-owned.

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
