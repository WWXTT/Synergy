# HTML/CSS Constraints for Unity UIToolkit Conversion

This document defines the constraints for generating HTML/CSS that can be successfully converted to Unity UXML/USS.

## HTML Constraints

### Allowed Elements

These elements have direct equivalents in UIToolkit:

```html
<!-- Containers -->
<div>, <span>, <section>, <header>, <footer>, <nav>, <main>, <article>, <aside>

<!-- Text -->
<h1>, <h2>, <h3>, <h4>, <h5>, <h6>, <p>, <label>

<!-- Interactive -->
<button>, <input>, <select>, <textarea>

<!-- Media -->
<img>

<!-- Other -->
<progress>, <ul>, <ol>, <li>, <a>
```

### Forbidden Elements

These elements have NO equivalent and must be avoided:

```html
<!-- Layout: Use Flexbox instead -->
<table>, <tr>, <td>, <th>, <thead>, <tbody>

<!-- No equivalent -->
<canvas>, <video>, <audio>, <iframe>

<!-- Different concept -->
<form>  <!-- Use <div> as container -->

<!-- Not applicable -->
<svg>, <script>, <style> (inline styles only)
```

### Element Mapping

| HTML | UXML Equivalent | Notes |
|------|-----------------|-------|
| `<div>` | `<ui:VisualElement>` | Generic container |
| `<span>` | `<ui:VisualElement>` | Inline container (use flex) |
| `<button>` | `<ui:Button>` | Button |
| `<input type="text">` | `<ui:TextField>` | Text input |
| `<input type="password">` | `<ui:TextField password="true">` | Password input |
| `<input type="checkbox">` | `<ui:Toggle>` | Checkbox |
| `<input type="radio">` | `<ui:RadioButton>` | Radio button |
| `<input type="number">` | `<ui:FloatField>` | Number input |
| `<input type="range">` | `<ui:Slider>` | Slider |
| `<select>` | `<ui:DropdownField>` | Dropdown |
| `<textarea>` | `<ui:TextField multiline="true">` | Multiline text |
| `<label>` | `<ui:Label>` | Text label |
| `<img>` | `<ui:Image>` | Image |
| `<progress>` | `<ui:ProgressBar>` | Progress bar |
| `<h1>-<h6>` | `<ui:Label>` | Headings (style with CSS) |
| `<p>` | `<ui:Label>` | Paragraph |
| `<ul>/<ol>` | `<ui:ListView>` | List |

### Attribute Mapping

| HTML Attribute | UXML Attribute |
|----------------|----------------|
| `id="name"` | `name="name"` |
| `class="cls"` | `class="cls"` |
| `disabled` | `enabled="false"` |
| `value="val"` | `value="val"` |
| `placeholder="text"` | `placeholder="text"` |
| `readonly` | `isReadOnly="true"` |

## CSS Constraints

### Allowed Properties (Direct Support)

```css
/* Dimensions */
width, height
min-width, min-height, max-width, max-height
aspect-ratio

/* Box Model */
margin, margin-top, margin-right, margin-bottom, margin-left
padding, padding-top, padding-right, padding-bottom, padding-left
border-width, border-color, border-radius

/* Background */
background-color

/* Text */
color, font-size
white-space, text-overflow
letter-spacing, word-spacing
text-shadow

/* Flexbox (Full Support) */
display: flex | none
flex-direction: row | row-reverse | column | column-reverse
flex-wrap: nowrap | wrap | wrap-reverse
justify-content: flex-start | flex-end | center | space-between | space-around
align-items: flex-start | flex-end | center | stretch
align-content: flex-start | flex-end | center | stretch
align-self: flex-start | flex-end | center | stretch
flex-grow, flex-shrink, flex-basis, flex
order

/* Positioning */
position: absolute | relative
left, top, right, bottom

/* Other */
opacity, visibility, overflow: hidden | visible
```

### Properties Requiring Conversion

```css
/* text-align → -unity-text-align */
text-align: left;      /* → -unity-text-align: middle-left; */
text-align: center;    /* → -unity-text-align: middle-center; */
text-align: right;     /* → -unity-text-align: middle-right; */

/* font-weight → -unity-font-style */
font-weight: bold;     /* → -unity-font-style: bold; */
font-weight: normal;   /* → -unity-font-style: normal; */

/* display block/inline → flex */
display: block;        /* → display: flex; flex-direction: column; */
display: inline;       /* → display: flex; */

/* gap → margin on children */
gap: 10px;             /* Remove and add margin: 5px to children */
```

### Forbidden Properties

```css
/* CSS Grid - NOT SUPPORTED */
display: grid;                    /* ERROR: Use Flexbox */
grid-template-columns: ...;       /* ERROR: Use Flexbox */
grid-gap: ...;                    /* ERROR: Use margin */

/* Gap - NOT SUPPORTED */
gap: 10px;                        /* ERROR: Use margin on children */
row-gap: 10px;                    /* ERROR: Use margin */
column-gap: 10px;                 /* ERROR: Use margin */

/* Position - Limited */
position: fixed;                  /* WARNING: Use absolute */
position: sticky;                 /* WARNING: Use relative */

/* Float - NOT SUPPORTED */
float: left;                      /* ERROR: Use Flexbox */
clear: both;                      /* ERROR: Use Flexbox */

/* Effects - NOT SUPPORTED */
box-shadow: ...;                  /* WARNING: Use layered elements */
filter: blur(...);                /* WARNING: Use material/shader */
transform: rotate(...);           /* WARNING: Limited support */

/* Gradients - NOT SUPPORTED */
background: linear-gradient(...); /* WARNING: Use image or solid color */
background: radial-gradient(...); /* WARNING: Use image or solid color */

/* Pseudo-elements - NOT SUPPORTED */
::before { }                      /* ERROR: Add actual element */
::after { }                       /* ERROR: Add actual element */

/* Complex selectors - Limited */
div > p:first-child { }           /* WARNING: Use class selector */
div + span { }                    /* WARNING: Use class selector */
```

### Selector Constraints

```css
/* ALLOWED: Simple selectors */
.my-class { }
#my-id { }
div { }
Button { }

/* ALLOWED: Simple descendant */
.container .item { }

/* WARNING: Complex selectors */
:nth-child(n)        /* May not work correctly */
:nth-of-type(n)      /* May not work correctly */
:first-child          /* May not work correctly */
:last-child           /* May not work correctly */

/* NOT SUPPORTED */
::before              /* ERROR */
::after               /* ERROR */
::first-line          /* ERROR */
::selection           /* ERROR */

/* LIMITED SUPPORT */
:hover                /* Use USS pseudo-class */
:active               /* Use USS pseudo-class */
:focus                /* Supported */
```

## Replacement Patterns

### Grid Layout → Flexbox

```css
/* DON'T */
.grid {
  display: grid;
  grid-template-columns: repeat(3, 1fr);
  gap: 10px;
}

/* DO */
.flex-container {
  display: flex;
  flex-wrap: wrap;
}
.flex-item {
  width: calc(33.33% - 10px);
  margin: 5px;
}
```

### Gap → Margin

```css
/* DON'T */
.container {
  display: flex;
  gap: 20px;
}

/* DO */
.container {
  display: flex;
}
.item {
  margin-right: 10px;
  margin-bottom: 10px;
}
.item:last-child {
  margin-right: 0;
}
```

### Fixed Position → Absolute

```css
/* DON'T */
.fixed-header {
  position: fixed;
  top: 0;
  left: 0;
  width: 100%;
}

/* DO */
.absolute-header {
  position: absolute;
  top: 0;
  left: 0;
  width: 100%;
}
```

### Box Shadow → Layered Elements

```css
/* DON'T */
.card {
  box-shadow: 0 2px 10px rgba(0,0,0,0.2);
}

/* DO - Option 1: Use border */
.card {
  border-width: 1px;
  border-color: rgba(0,0,0,0.2);
}

/* DO - Option 2: Use layered visual element */
.card-container {
  position: relative;
}
.shadow {
  position: absolute;
  background-color: rgba(0,0,0,0.2);
  /* Offset from card */
  top: 2px;
  left: 2px;
}
```

## Code Generation Template

When generating HTML/CSS for Unity conversion, use this template:

```html
<!DOCTYPE html>
<html>
<head>
<style>
/* Container */
.container {
  display: flex;
  flex-direction: column;
  width: 100%;
  height: 100%;
}

/* Row layout */
.row {
  display: flex;
  flex-direction: row;
}

/* Column layout */
.column {
  display: flex;
  flex-direction: column;
}

/* Gap replacement */
.item {
  margin: 5px;
}

/* Center content */
.center {
  display: flex;
  justify-content: center;
  align-items: center;
}

/* Text alignment - will convert to -unity-text-align */
.text-center {
  text-align: center;
}

/* Bold text - will convert to -unity-font-style */
.bold {
  font-weight: bold;
}
</style>
</head>
<body>
<div class="container">
  <!-- Your content here -->
</div>
</body>
</html>
```