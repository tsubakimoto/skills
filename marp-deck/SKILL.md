---
name: marp-deck
description: "Use this skill whenever the user wants to create, outline, rewrite, or organize a presentation in Marp or Marpit Markdown. Trigger on requests to turn notes, docs, READMEs, specs, or outlines into slide Markdown; set up `marp: true` frontmatter; choose slide structure, directives, backgrounds, speaker flow, asset folders, or export commands; or build a new Markdown slide deck from scratch. Use it even when the user says 'presentation', 'deck', or 'slides' without naming Marp explicitly but the desired output is a Marp Markdown deck. If the task is mainly theme CSS or visual restyling, also consult `marp-css`."
---

# Marp Deck Authoring

Use this skill to create or reshape a slide deck in Marp Markdown. Focus on slide structure, Markdown authoring, directives, asset organization, and export workflow.

## Quick routing

| Situation | What to do |
| --- | --- |
| Create a new Marp deck from scratch | Start from `assets/starter-deck.md`, then adapt the flow and content. |
| Turn an existing doc into slides | Use `headingDivider` if the source is already well-structured, otherwise rewrite into slide-sized chunks. |
| User needs theme CSS or a custom visual system | Use this skill for deck structure, and consult `marp-css` for theme work. |
| User wants export commands or local preview | Give Marp CLI or Marp for VS Code guidance. |
| User mentions folders, assets, or project layout | Use the recommended directory layout in this skill. |

## Workflow

1. Inspect the source material before writing slides.
2. Infer the audience, goal, and presentation length from the prompt and files.
3. Decide the deck shape before drafting:
   - title / hook
   - context or problem
   - evidence or analysis
   - proposal / solution
   - next steps / summary
4. Write one clear idea per slide. Prefer strong headings over dense paragraphs.
5. Set up Marp frontmatter and directives early so the deck is runnable immediately.
6. Use relative paths for images and themes so the deck works across preview and export workflows.
7. Keep styling lightweight in the Markdown itself. If the user needs reusable styling or a redesign, hand that part to `marp-css`.

## Authoring rules

### 1. Start with valid Marp frontmatter

Use frontmatter when creating a deck unless the user already has a preferred format.

```markdown
---
marp: true
theme: default
paginate: true
---
```

- `marp: true` enables Marp features in Marp for VS Code.
- `theme` selects the active theme.
- `paginate` enables page numbers when the theme supports them.
- For Marp Core decks, `size` is available as a global directive, such as `size: 4:3`.

### 2. Separate slides with rulers

Use `---` between slides.

```markdown
# Slide 1

Content

---

# Slide 2
```

If the source document is plain Markdown with consistent headings, `headingDivider` can be cleaner than inserting rulers everywhere.

```markdown
---
marp: true
headingDivider: 2
---
```

### 3. Use directives deliberately

Useful directives to reach for first:

- global: `theme`, `style`, `headingDivider`, `lang`
- local: `paginate`, `header`, `footer`, `class`
- slide styling: `backgroundColor`, `backgroundImage`, `backgroundSize`, `color`
- Marp Core additions: `size`, `math`

Use spot directives with a leading underscore when the change should affect only one slide.

```markdown
<!-- _class: lead -->
<!-- _paginate: false -->
```

### 4. Prefer slide-native image syntax

Use Marpit image syntax instead of HTML hacks when it expresses the layout clearly.

```markdown
![bg](./images/hero.jpg)
![bg right](./images/product.png)
![bg left:33%](./images/diagram.png)
![w:220](./images/logo.png)
```

- `![bg]` sets a slide background.
- `left` / `right` create split-background layouts.
- `left:33%` or `right:40%` controls split width.
- `w:` / `h:` set image size with stable units.

### 5. Keep slides presentation-sized

- Prefer 1 message per slide.
- Keep bullets short and parallel.
- Split crowded slides instead of shrinking text.
- Convert long prose into headings, short bullets, tables, diagrams, or comparison layouts.
- Use section divider slides to reset attention in longer decks.

### 6. Use inline style sparingly

- Use `<style scoped>` only for a one-slide exception.
- Use a shared theme CSS file for repeated styling.
- If styling becomes a main task, route that work to `marp-css`.

## Recommended deck structures

### Story-first business deck

1. Title / promise
2. Why this matters now
3. Current state or problem
4. Evidence / data / examples
5. Proposed approach
6. Impact / tradeoffs
7. Next steps

### Technical explanation deck

1. Title / scope
2. System context
3. Current architecture or problem
4. Key design decisions
5. Flow, sequence, or API examples
6. Risks and mitigations
7. Rollout / action items

### Training or workshop deck

1. Title / objective
2. Audience prerequisites
3. Agenda
4. Concepts
5. Demo or worked example
6. Practice or checklist
7. Summary / references

## Recommended directory layout

This is a practical convention for Marp projects, not an official requirement.

```text
slides/
  deck.md
  themes/
    brand.css
  images/
    hero.png
    diagrams/
  data/
  snippets/
  exports/
```

Use it like this:

- `slides/deck.md`: main deck entry point
- `slides/themes/`: reusable Marp theme CSS
- `slides/images/`: screenshots, diagrams, logos, photography
- `slides/data/`: CSV or JSON used to generate charts or tables
- `slides/snippets/`: reused Markdown fragments or raw HTML fragments if the workflow needs them
- `slides/exports/`: generated `.html`, `.pdf`, `.pptx`, or images

If the workspace uses Marp for VS Code and custom themes, register theme files in `.vscode/settings.json` with `markdown.marp.themes`.

## Output requirements

When the user wants deck content, produce:

1. The Marp Markdown deck.
2. Any required frontmatter or directives.
3. Relative asset paths that match the workspace layout.
4. Brief notes about missing assets or placeholders only when needed.

When the user wants a plan before writing slides, provide:

1. the deck outline,
2. the intended slide count,
3. any assumptions about audience or tone.

## Export and preview guidance

Prefer the simplest workflow already available in the workspace.

### Marp for VS Code

- Add `marp: true` in frontmatter.
- Use the preview while editing.
- Export from the Marp command if the extension is installed.

### Marp CLI

```bash
npx @marp-team/marp-cli@latest slides/deck.md
npx @marp-team/marp-cli@latest slides/deck.md --pdf -o slides/exports/deck.pdf
npx @marp-team/marp-cli@latest slides/deck.md --pptx -o slides/exports/deck.pptx
```

- HTML export is the default.
- PDF, PPTX, and image export require a supported browser.
- If local images must be resolved during browser-based export, `--allow-local-files` may be required for trusted content.

## Avoid

- Writing document-length prose onto slides.
- Mixing too many layout ideas on one slide.
- Using absolute-position HTML for ordinary content when Markdown or Marp image syntax is enough.
- Treating the recommended directory layout as mandatory.
- Solving a theme-design problem only with Markdown structure when `marp-css` is the better tool.

## Bundled resources

- Read `references/marp-authoring-reference.md` for exact syntax reminders and directory guidance.
- Start from `assets/starter-deck.md` when the user needs a clean Marp scaffold fast.
