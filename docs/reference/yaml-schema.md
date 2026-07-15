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
  relative to the config file. If the path is configured but the file does not
  exist yet, the first build scaffolds it.

Relative source paths must be unique across the configured scan roots so the nav
map can refer to them unambiguously.

## Nav map

```yaml
nav:
  - title: Home
    source: README.md
  - title: Guide
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

When a nav map is present, the map owns output order, titles, and placement.
Documents absent from the map are reported as unplaced drift.
