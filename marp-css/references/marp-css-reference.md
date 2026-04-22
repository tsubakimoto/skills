# Marp CSS Reference

Concise reference notes for writing Marp / Marpit theme CSS.

## Theme basics

- Theme CSS requires `/* @theme name */`.
- Each slide is rendered as a `section`.
- In Marpit theme CSS, `:root` behaves like the slide root, not the document root.
- `rem` units resolve relative to the slide root size, so they are safe for theme-scale typography.

## Slide size

- Default slide size is `1280px x 720px`.
- Define slide size on `section` or `:root`.
- Use only absolute units such as `px`, `pt`, `in`, `cm`, `mm`, `pc`, or `Q`.
- For Marp Core size switching, define `@size` metadata, for example:

```css
/*
 * @theme brand
 * @size 16:9 1280px 720px
 * @size 4:3 960px 720px
 */
```

## Pagination

- Marp exposes page numbers through `data-marpit-pagination` and total pages through `data-marpit-pagination-total`.
- Style pagination with `section::after` or `:root::after`.
- If you override pagination `content`, it still has to contain `attr(data-marpit-pagination)` or Marpit will ignore that declaration.

Example:

```css
section::after {
  content: 'Page ' attr(data-marpit-pagination) ' / ' attr(data-marpit-pagination-total);
}
```

## Header and footer

- `header` and `footer` are available through Marp directives.
- They do not get default theme styling.
- Use explicit positioning when you want them in the slide margin area.

## Extending themes

- Use `@import 'default';`, `@import 'gaia';`, or `@import 'uncover';` to build on existing themes.
- Use `@import-theme` if a preprocessor would otherwise consume `@import`.
- Built-in Marp themes also expose useful CSS variables for color customization.

## Inline styles inside Markdown

- `<style>` applies globally to the deck.
- `<style scoped>` applies only to the current slide.
- This is useful for a one-off exception without polluting the shared theme.

## Tailwind CSS with Marp

- Marp allows `class` attributes on raw HTML, so Tailwind utility classes can be used on HTML fragments such as `<div class="flex gap-4 rounded-xl">...</div>`.
- Tailwind still needs its normal CSS generation pipeline. Utility classes do nothing unless the rendered deck includes the compiled Tailwind stylesheet.
- Tailwind is strongest for local composition. Marp theme CSS is still the better place for slide-level rules like root sizing, typography scale, pagination, and repeated branding.
- If the deck is mostly Markdown, prefer translating Tailwind design tokens into theme CSS instead of turning many slides into HTML-heavy utility markup.
- Tailwind scans source files for class names. If classes are built dynamically, they may be omitted from the output unless safelisted.
- Responsive variants like `sm:` or `lg:` are often less important in fixed-size slide canvases than in app UIs.

## Background image syntax

Use Markdown image syntax for visual layouts that depend on slide backgrounds:

```md
![bg](hero.jpg)
![bg right](photo.jpg)
![bg left:33%](diagram.png)
![bg contain](illustration.png)
```

Useful facts:

- `bg` makes the image a slide background.
- `left` / `right` create split backgrounds and also shrink the content area.
- `contain`, `cover`, `auto`, percentages, and explicit `w:` / `h:` sizing are supported for backgrounds.
- Advanced multiple backgrounds and some filters depend on inline SVG mode.

## Marp Core metadata worth remembering

- `@size ...` defines named presets for the `size:` directive.
- `@auto-scaling ...` enables features such as fitting headers and scaling code / math blocks.

Example:

```css
/*
 * @theme brand
 * @auto-scaling fittingHeader,code,math
 */
```

## Practical design advice

- Start with tokens: background, foreground, accent, muted, border, code background.
- Define a consistent type scale before styling components.
- Keep slide padding and vertical rhythm consistent.
- Prefer background-image syntax, split layouts, and Markdown structure over fragile absolute-position hacks.
