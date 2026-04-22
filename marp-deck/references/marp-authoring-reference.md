# Marp authoring reference

Use this reference when you need exact syntax, a starter workflow, or a recommended folder layout for a Marp deck.

## Minimal deck

```markdown
---
marp: true
theme: default
paginate: true
---

# Deck title

Subtitle or promise

---

## Problem

- Point one
- Point two

---

## Proposal

- Step one
- Step two
```

## Core syntax reminders

### Slide breaks

Marpit splits slides with a horizontal ruler:

```markdown
---
```

If the content is already structured by headings, `headingDivider` can turn a document into slides without manually inserting every ruler.

```markdown
---
marp: true
headingDivider: 2
---
```

### Useful directives

```markdown
---
marp: true
theme: default
paginate: true
size: 4:3
header: "Team update"
footer: "Internal"
---
```

- `marp: true`: enables Marp features in Marp for VS Code
- `theme`: active theme
- `paginate`: page numbers
- `size`: Marp Core slide size preset such as `4:3`
- `header` / `footer`: repeated slide furniture

Single-slide overrides can use spot directives:

```markdown
<!-- _class: lead -->
<!-- _paginate: false -->
```

### Backgrounds and images

```markdown
![bg](./images/cover.jpg)
![bg right](./images/ui-shot.png)
![bg left:33%](./images/diagram.png)
![w:240](./images/logo.png)
```

- `bg` turns an image into the slide background.
- `right` / `left` create split-background slides.
- `left:33%` controls the split size.
- `w:` / `h:` resize inline images with stable units.

### Scoped style for one-off fixes

```markdown
<style scoped>
h1 {
  letter-spacing: 0.04em;
}
</style>
```

Use a shared theme CSS file when the same rule should apply across many slides.

## Recommended directory layout

```text
slides/
  deck.md
  themes/
    brand.css
  images/
    diagrams/
    screenshots/
  data/
  snippets/
  exports/
```

## Layout notes

- Keep `deck.md` as the main entry point unless the project already uses multiple deck files.
- Use relative paths from `deck.md`, for example `./images/hero.png`.
- Put generated outputs in `exports/` so source files stay clean.
- Keep theme CSS in `themes/` so Marp for VS Code can register it cleanly.

Example `.vscode/settings.json`:

```json
{
  "markdown.marp.themes": [
    "./slides/themes/brand.css"
  ]
}
```

## Export commands

```bash
# HTML
npx @marp-team/marp-cli@latest slides/deck.md -o slides/exports/deck.html

# PDF
npx @marp-team/marp-cli@latest slides/deck.md --pdf -o slides/exports/deck.pdf

# PPTX
npx @marp-team/marp-cli@latest slides/deck.md --pptx -o slides/exports/deck.pptx
```

Notes:

- HTML is the default output format.
- PDF, PPTX, and image export require a supported browser.
- Browser-based export may need `--allow-local-files` for trusted local assets.

## When to involve `marp-css`

Use the `marp-css` skill when the main task is one of these:

- custom theme creation
- deck-wide typography or palette changes
- pagination styling
- header / footer positioning
- background polish
- reusable CSS architecture
