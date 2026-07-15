# YAML schema

`CKL.Libs.SiteBuilder` reads two pinned YAML shapes: the site config and the
authoritative nav map.

## Site config

```yaml
title: My Docs Site
output: ./_site
scanRoots:
  - ../repo-a/docs
  - ../repo-b/docs
theme:
  stylesheet: ./custom.css
  mermaid: dark
nav: ./nav.yml
section: expand
assets:
  exclude:
    - vendor
    - "*.generated"
```

- `title` — optional; defaults to the first scan root's folder name.
- `output` — optional; defaults to `./_site` relative to the config file.
- `scanRoots` — required; one or more source roots, each resolved relative to the
  config file when written as a relative path.
- `theme` — optional.
  - `stylesheet` — optional; when set, its file contents are emitted as
    `site.css` instead of the built-in stylesheet.
  - `mermaid` — optional; defaults to `dark`.
- `nav` — optional; path to the authoritative nav-map YAML file, resolved
  relative to the config file. When omitted, the nav map path defaults to
  `nav.yml` in the current working directory. Either way, a missing map is
  always generated as a **complete** scaffold (every discovered document
  placed) on first run, then hand-owned thereafter.
- `section` — optional; `expand` (default) or `overview`. Controls whether a
  folder-derived nav section is a pure expandable nav group (no generated
  page) or gets a generated overview/listing page. Overridable per nav-map
  entry via that entry's own `section:` field.
- `assets` — optional.
  - `exclude` — optional list of directory-name or glob patterns (matched
    per path segment, case-insensitive) added to the built-in asset-copy
    ignore set (`.git`, `.vs`, `.vscode`, `.idea`, `bin`, `obj`,
    `node_modules`, and any dotfile/dot-directory segment).

Relative source paths must be unique across the configured scan roots so the nav
map can refer to them unambiguously.

## Nav map

```yaml
nav:
  - title: Home
    source: README.md
    home: true
  - title: Guide
    section: overview
    children:
      - title: Introduction
        source: guide/intro.md
      - title: Advanced
        source: guide/advanced.md
        skip: true
```

- `nav` — required root sequence of entries.
- `title` — required output label.
- `source` — optional source document path, relative to a configured scan root.
- `children` — optional ordered child entries. A section entry omits `source` and
  uses `children`; a leaf entry uses `source`.
- `skip` — optional; when `true`, the entry is treated as placed for drift
  detection but omitted from rendered output.
- `home` — optional; when `true` on a leaf (`source`) entry, that document
  becomes the root `index.html` landing page instead of the usual
  title-derived output path. At most one entry may set `home: true`; a
  second one fails the build. When no entry is designated `home`, a landing
  page is synthesised automatically, listing the top-level pages and
  sections.
- `section` — optional, on a section (`children`) entry only; `expand` or
  `overview`. Overrides the config-level `section:` default for that entry's
  subtree.

When a nav map is present, the map owns output order, titles, and placement.
Documents absent from the map are reported as unplaced drift.
