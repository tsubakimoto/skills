---
name: marp-css
description: "Use this skill whenever the user is working on Marp or Marpit slide design with CSS: creating a custom theme, restyling a Markdown deck, tuning style blocks, changing colors, typography, spacing, pagination, header/footer, backgrounds, or making a Marp deck look more polished. Trigger on mentions of Marp, Marpit, `@theme`, theme CSS, slide theming, deck styling, Gaia or Uncover customization, split backgrounds, or requests like 'make these Marp slides prettier' even if the user does not explicitly ask for CSS."
---

# Marp CSS Design

Use this skill to design or revise the visual system of a Marp / Marpit deck without treating it like a generic web page.

## Quick routing

| Situation | What to do |
| --- | --- |
| Small one-off slide tweak | Edit the deck's `<style>` block. Use `<style scoped>` when the change should affect only one slide. |
| Repeated deck-wide styling | Create or update a reusable theme CSS file with `/* @theme ... */`. |
| Built-in theme is close but not enough | Start from `@import 'default'`, `@import 'gaia'`, or `@import 'uncover'`, then layer custom tokens and component styles. |
| User wants to use Tailwind CSS | First determine whether Tailwind is part of the build pipeline. If yes, use utility classes in raw HTML only where that keeps slides clearer; keep deck-wide structure, typography, pagination, and reuse in theme CSS. |
| User wants "nicer visuals" but not a full redesign | Keep content structure, improve palette, typography, spacing, emphasis, and image treatment first. |
| User wants image-led layouts | Use Marpit background image syntax such as `![bg]`, `![bg right]`, or `![bg left:33%]` in Markdown, not CSS hacks alone. |

## Workflow

1. Inspect the current Markdown deck and any existing theme CSS before writing new rules.
2. If Tailwind is mentioned, inspect how CSS is produced: compiled asset, CDN usage, or no Tailwind pipeline at all.
3. Decide whether the job is better served by theme CSS, Tailwind utilities in HTML snippets, or a hybrid approach.
4. Decide the smallest correct scope:
   - inline `<style>` for local tuning
   - `<style scoped>` for a single slide
   - theme CSS file for reusable branding
5. Preserve the author's intent. Improve the design system before rewriting slide content.
6. Prefer a coherent palette, typographic scale, spacing rhythm, and image treatment over lots of one-off selectors.
7. When changing theme code, also update the Markdown directives or theme references if needed.

## Marp-specific rules

- Theme CSS **must** declare `/* @theme name */`.
- Treat `section` or `:root` as the slide viewport. In Marpit theme CSS, `:root` maps to each slide, not the page `<html>`.
- Set slide `width` and `height` only with absolute units such as `px`, `pt`, `in`, or `cm`.
- Prefer `rem` for internal scale once the root font size is set.
- If the deck uses Marp Core and needs switchable sizes, define `@size` presets in the theme.
- Style pagination through `section::after` or `:root::after`. If you customize `content`, it must still include `attr(data-marpit-pagination)`.
- `header` and `footer` from Marp directives have no default theme styling. Position them explicitly if used.
- Use `@import` or `@import-theme` when extending another theme instead of copying large chunks of CSS.
- For slide-specific visual exceptions, prefer `<style scoped>` over bloating the shared theme.
- Raw HTML `class` attributes are allowed in Marp, so Tailwind utility classes can work on HTML fragments, but only if the rendered deck actually includes the compiled Tailwind CSS.

## Tailwind considerations

### When Tailwind is a good fit

- The deck already lives inside a web build pipeline that compiles Tailwind.
- The user is styling raw HTML fragments inside slides and wants quick, local layout control.
- The main need is utility-driven composition for cards, badges, compact flex layouts, or responsive-ish HTML fragments in browser-rendered output.

### When plain theme CSS is the better default

- The user wants reusable deck-wide styling across many slides.
- The task involves slide size, root typography, pagination, header/footer, or Marp-specific constructs.
- The deck is exported through a workflow where Tailwind assets are not obviously present.
- The user is writing mostly Markdown, not HTML-heavy slide bodies.

### Hybrid guidance

- Use Tailwind as a design-system source, not just a pile of utility classes.
- Extract palette, spacing, radius, and type choices from Tailwind into CSS custom properties in the Marp theme when those choices should apply deck-wide.
- Use utility classes sparingly for local slide components that are easier to express in HTML than in Markdown.
- If class names are generated dynamically, make sure the build process safelists them or they may be removed during Tailwind's class scanning.
- Do not assume responsive variants matter in exported slides the same way they do in apps; slide canvases are usually fixed-size.
- If Tailwind is unavailable, translate the intended utilities into ordinary theme CSS instead of blocking on the framework.

## Design heuristics

### 1. Build a deck-level visual system first

- Pick one dominant background mode: mostly light, mostly dark, or clearly alternating by section.
- Limit the palette to one dominant tone, one support tone, and one accent.
- Define reusable tokens with CSS custom properties for background, foreground, accent, muted text, border, and code background.

### 2. Make hierarchy obvious

- Use a clear title scale, subheading scale, and body size.
- Keep body text readable; avoid shrinking text to fit weak layout choices.
- Make emphasis intentional with weight, color, or spacing, not all three everywhere.

### 3. Design for slides, not documents

- Favor short blocks, card layouts, callouts, comparisons, and image-text pairings.
- Avoid dense page-like prose styling.
- Use generous padding and consistent gaps so each slide reads at a glance.

### 4. Let Markdown and CSS share the job

- Use Markdown structure for headings, lists, quotes, tables, and background images.
- Use CSS for tone, rhythm, alignment, and component styling.
- Do not force every layout with absolute positioning if Markdown structure can do it more robustly.

## Output requirements

When the user wants code, produce:

1. The updated theme CSS or `<style>` block.
2. Any required Markdown directives or usage snippet, such as `theme:`, `size:`, `paginate:`, `header:`, background image syntax, or raw HTML with Tailwind classes.
3. If Tailwind is involved, state whether the solution assumes an existing Tailwind build, CDN usage, or a translation into plain CSS.
4. A brief rationale tied to the visual goal, unless the user asked for code only.

## Quality bar

- Keep selectors simple and explainable.
- Default to theme tokens and reusable patterns instead of brittle per-slide overrides.
- Preserve compatibility with Marp features like pagination, scoped styles, and split backgrounds.
- Preserve Tailwind compatibility by avoiding advice that assumes utility classes work without a compiled stylesheet.
- If the user asks for a redesign, give them a coherent system, not isolated cosmetics.

## Avoid

- Generic web-app assumptions about DOM structure outside slide `section` content.
- Viewport-relative slide sizes such as `vw` or `vh`.
- Pagination styles that remove `attr(data-marpit-pagination)`.
- Overusing absolute positioning for ordinary text flow.
- Rewriting the whole deck when a token or layout adjustment would solve the problem.
- Assuming Tailwind utility classes will work in Markdown-only content without HTML wrappers and loaded CSS.

## Bundled resources

- Read [`references/marp-css-reference.md`](references/marp-css-reference.md) when you need exact Marp / Marpit behavior or a reminder of official constraints.
- Start from [`assets/starter-theme.css`](assets/starter-theme.css) when the user wants a reusable custom theme or needs a clean base to iterate on.
- Read [`assets/tailwind-token-bridge.css`](assets/tailwind-token-bridge.css) when the user wants a Marp theme that borrows Tailwind-style tokens without depending on utility classes everywhere.
