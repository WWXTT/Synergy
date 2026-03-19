# Unity UIToolkit Guide for AI

This document provides comprehensive guidance for AI systems generating HTML/CSS that will be converted to Unity UIToolkit (UXML/USS) format.

## Overview

Unity UIToolkit is a UI framework inspired by web technologies. It uses:
- **UXML** - Similar to HTML, defines UI structure
- **USS** - Similar to CSS, defines styling

## Supported Layout System: Flexbox

Unity UIToolkit uses the **Yoga** layout engine, which implements a subset of Flexbox.

### Flexbox Container Properties

```css
/* Direction */
flex-direction: row | row-reverse | column | column-reverse;

/* Wrapping */
flex-wrap: nowrap | wrap | wrap-reverse;

/* Alignment */
align-content: flex-start | flex-end | center | stretch;
align-items: auto | flex-start | flex-end | center | stretch;
justify-content: flex-start | flex-end | center | space-between | space-around;
```

### Flexbox Item Properties

```css
flex-grow: <number>;
flex-shrink: <number>;
flex-basis: <length> | auto;
flex: <grow> <shrink> <basis>; /* shorthand */
align-self: auto | flex-start | flex-end | center | stretch;
order: <integer>;
```

**Important**: CSS Grid is NOT supported. Use Flexbox with `flex-wrap: wrap` to simulate multi-row layouts.

## Available UXML Elements (Runtime)

### Containers
| Element | UXML Tag | Description |
|---------|----------|-------------|
| VisualElement | `<ui:VisualElement>` | Generic container |
| Box | `<ui:Box>` | Container with border |
| GroupBox | `<ui:GroupBox>` | Grouped container with label |
| Foldout | `<ui:Foldout>` | Collapsible container |
| ScrollView | `<ui:ScrollView>` | Scrollable container |

### Text
| Element | UXML Tag | Description |
|---------|----------|-------------|
| Label | `<ui:Label>` | Display text |
| TextField | `<ui:TextField>` | Text input |
| TextElement | `<ui:TextElement>` | Base text element |

### Buttons
| Element | UXML Tag | Description |
|---------|----------|-------------|
| Button | `<ui:Button>` | Standard button |
| RepeatButton | `<ui:RepeatButton>` | Repeated click button |

### Selection
| Element | UXML Tag | Description |
|---------|----------|-------------|
| Toggle | `<ui:Toggle>` | Checkbox |
| RadioButton | `<ui:RadioButton>` | Radio button |
| RadioButtonGroup | `<ui:RadioButtonGroup>` | Radio button group |
| DropdownField | `<ui:DropdownField>` | Dropdown select |

### Numeric
| Element | UXML Tag | Description |
|---------|----------|-------------|
| FloatField | `<ui:FloatField>` | Float input |
| IntegerField | `<ui:IntegerField>` | Integer input |
| Slider | `<ui:Slider>` | Float slider |
| SliderInt | `<ui:SliderInt>` | Integer slider |

### Other
| Element | UXML Tag | Description |
|---------|----------|-------------|
| Image | `<ui:Image>` | Image display |
| ProgressBar | `<ui:ProgressBar>` | Progress indicator |
| ListView | `<ui:ListView>` | List view |
| TabView | `<ui:TabView>` | Tab container |

## Supported USS Properties

### Box Model
```css
width, height
min-width, min-height
max-width, max-height
margin, margin-top, margin-right, margin-bottom, margin-left
padding, padding-top, padding-right, padding-bottom, padding-left
border-width, border-top-width, border-right-width, border-bottom-width, border-left-width
border-color, border-top-color, border-right-color, border-bottom-color, border-left-color
border-radius, border-top-left-radius, border-top-right-radius, border-bottom-right-radius, border-bottom-left-radius
```

### Positioning
```css
position: absolute | relative;
left, top, right, bottom;
```

### Background
```css
background-color: <color>;
background-image: url('path/to/asset');
-unity-background-scale-mode: stretch-to-fill | scale-and-crop | scale-to-fit;
```

### Text
```css
color: <color>;
font-size: <length>;
-unity-font-style: normal | italic | bold | bold-and-italic;
-unity-text-align: upper-left | middle-left | lower-left | upper-center | middle-center | lower-center | upper-right | middle-right | lower-right;
white-space: normal | nowrap;
text-overflow: clip | ellipsis;
text-shadow: <offset-x> <offset-y> <blur-radius> <color>;
letter-spacing: <length>;
word-spacing: <length>;
```

### Other
```css
opacity: <number>;
visibility: visible | hidden;
display: flex | none;
overflow: hidden | visible;
aspect-ratio: <ratio>;
```

## Unity-Specific Properties

These properties have `-unity-` prefix:

```css
-unity-font: <resource>;
-unity-font-definition: <resource>;
-unity-font-style: normal | italic | bold | bold-and-italic;
-unity-text-align: upper-left | middle-left | ... | lower-right;
-unity-text-outline-width: <length>;
-unity-text-outline-color: <color>;
-unity-background-scale-mode: stretch-to-fill | scale-and-crop | scale-to-fit;
-unity-slice-left: <integer>;
-unity-slice-top: <integer>;
-unity-slice-right: <integer>;
-unity-slice-bottom: <integer>;
-unity-material: <resource>;
```

## Color Formats

```css
/* Supported */
background-color: #FF5500;        /* Hex RGB */
background-color: #FF5500AA;       /* Hex RGBA */
background-color: rgb(255, 85, 0);
background-color: rgba(255, 85, 0, 0.5);

/* Unity named colors */
background-color: unity-theme-color;
```

## Value Units

```css
/* Supported */
width: 100px;      /* Pixels */
width: 50%;        /* Percentage */

/* Not supported - convert to px or % */
width: 10em;       /* Convert to px */
width: 10rem;      /* Convert to px */
width: 50vw;       /* Use % instead */
width: 50vh;       /* Use % instead */
```

## UXML Structure

```xml
<ui:UXML xmlns:ui="UnityEngine.UIElements">
    <ui:VisualElement class="container">
        <ui:Label text="Hello" class="title" />
        <ui:Button text="Click" class="btn-primary" />
    </ui:VisualElement>
</ui:UXML>
```

## Key Differences from Web

1. **No CSS Grid** - Use Flexbox with `flex-wrap: wrap`
2. **No gap property** - Use margin on children instead
3. **No position: fixed/sticky** - Use absolute or relative
4. **No pseudo-elements** - Cannot use ::before, ::after
5. **Limited selectors** - Prefer simple class selectors
6. **No box-shadow** - Use layered elements or 9-slice images
7. **No gradients** - Use solid colors or images
8. **No cursor styles** - Not supported in Runtime

## Best Practices

1. Use `display: flex` for all containers
2. Use simple class selectors (`.className`)
3. Avoid complex descendant selectors
4. Convert `gap` to margin on children
5. Use absolute positioning sparingly
6. Prefer `px` or `%` units
7. Test in Unity after conversion