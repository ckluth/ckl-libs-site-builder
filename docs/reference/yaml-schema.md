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
intro: Welcome **home**.
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
- `intro` — optional free-text markdown rendered above the synthesised landing
  page listing. Ignored with a warning when the nav map designates a `home: true`
  page instead.
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
  - title: Ideas
    source: ideas/*.md
    titleFrom: headline
    exclude:
      - ideas/9999-shelved.md
    section: overview
    intro: Fresh ideas under discussion.
```

- `nav` — required root sequence of entries.
- `title` — required output label.
- `source` — optional source document path, relative to a configured scan root.
  When it contains `*` or `?`, it becomes a wildcard section that expands at
  assembly time against discovered relative source paths. Matching is
  case-insensitive with `/` and `\` normalised to `/`: `*` matches any run of
  non-separator characters, `**` matches any run including separators, and `?`
  matches exactly one non-separator character. Expanded children are ordered by
  relative source path ascending (`StringComparer.OrdinalIgnoreCase`).
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
- `titleFrom` — optional, on a wildcard `source` entry only. The only accepted
  value is `headline` (also the default): each expanded child takes its title
  from the matched document's first H1, falling back to a formatted filename when
  no H1 is present.
- `exclude` — optional, on a wildcard `source` entry only. A list of literal
  relative source paths omitted from the expansion and treated as placed (like
  `skip: true`), so they do not render and do not produce drift.
- `intro` — optional on a section (`children`) entry or wildcard section. When
  that section renders an `overview` page, the intro is rendered as Markdown
  above the generated listing. On an `expand` section it is ignored with a
  warning. `intro` is invalid on a leaf (`source`, non-wildcard) entry.

When a nav map is present, the map owns output order, titles, and placement.
Documents absent from the map are reported as unplaced drift. Wildcard entries
may not combine with `children`, `home: true`, or `skip: true`.
