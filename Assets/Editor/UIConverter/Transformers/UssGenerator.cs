using System;
using System.Collections.Generic;
using System.Text;
using System.Text.RegularExpressions;
using UIConverter.Parsers;
using UnityEngine;

namespace UIConverter.Transformers
{
    /// <summary>
    /// Result of USS generation
    /// </summary>
    public class UssGenerateResult
    {
        public string UssContent { get; set; }
        public List<UssConversionWarning> Warnings { get; set; } = new List<UssConversionWarning>();
        public List<string> Errors { get; set; } = new List<string>();
    }

    /// <summary>
    /// Warning about CSS to USS conversion
    /// </summary>
    public class UssConversionWarning
    {
        public string Property { get; set; }
        public string OriginalValue { get; set; }
        public string ConvertedValue { get; set; }
        public string Message { get; set; }
        public WarningSeverity Severity { get; set; }
        public string Selector { get; set; }
    }

    public enum WarningSeverity
    {
        Info,
        Warning,
        Error
    }

    /// <summary>
    /// Generates USS from parsed CSS
    /// </summary>
    public class UssGenerator
    {
        private StringBuilder _sb;
        private List<UssConversionWarning> _warnings;
        private List<string> _errors;

        // Direct CSS properties that work in USS
        private static readonly HashSet<string> DirectProperties = new HashSet<string>
        {
            "width", "height", "min-width", "min-height", "max-width", "max-height",
            "margin", "margin-top", "margin-right", "margin-bottom", "margin-left",
            "padding", "padding-top", "padding-right", "padding-bottom", "padding-left",
            "border-width", "border-top-width", "border-right-width", "border-bottom-width", "border-left-width",
            "border-color", "border-top-color", "border-right-color", "border-bottom-color", "border-left-color",
            "border-radius", "border-top-left-radius", "border-top-right-radius", "border-bottom-right-radius", "border-bottom-left-radius",
            "background-color", "color", "font-size", "opacity", "visibility", "overflow",
            "flex-direction", "flex-wrap", "justify-content", "align-items", "align-content", "align-self",
            "flex-grow", "flex-shrink", "flex-basis", "order",
            "left", "top", "right", "bottom", "aspect-ratio",
            "white-space", "text-overflow", "letter-spacing", "word-spacing"
        };

        // CSS properties that need transformation
        private static readonly Dictionary<string, string> PropertyTransforms = new Dictionary<string, string>
        {
            { "text-align", "-unity-text-align" },
            { "font-weight", "-unity-font-style" },
            { "font-style", "-unity-font-style" }
        };

        // Value transformations for text-align
        private static readonly Dictionary<string, string> TextAlignValues = new Dictionary<string, string>
        {
            { "left", "middle-left" },
            { "center", "middle-center" },
            { "right", "middle-right" },
            { "justify", "middle-center" }
        };

        // Value transformations for font-weight
        private static readonly Dictionary<string, string> FontWeightValues = new Dictionary<string, string>
        {
            { "normal", "normal" },
            { "bold", "bold" },
            { "bolder", "bold" },
            { "lighter", "normal" }
        };

        // Unsupported properties
        private static readonly HashSet<string> UnsupportedProperties = new HashSet<string>
        {
            "gap", "row-gap", "column-gap",
            "box-shadow", "filter", "clip-path", "mask",
            "mix-blend-mode", "isolation"
        };

        public UssGenerateResult Generate(CssParseResult cssResult)
        {
            _sb = new StringBuilder();
            _warnings = new List<UssConversionWarning>();
            _errors = new List<string>();

            var result = new UssGenerateResult();

            try
            {
                foreach (var rule in cssResult.Rules)
                {
                    GenerateRule(rule);
                }

                result.UssContent = _sb.ToString();
            }
            catch (Exception ex)
            {
                _errors.Add($"USS generation error: {ex.Message}");
            }

            // Add parse warnings
            foreach (var warning in cssResult.Warnings)
            {
                _warnings.Add(new UssConversionWarning
                {
                    Message = warning,
                    Severity = WarningSeverity.Warning
                });
            }

            result.Warnings = _warnings;
            result.Errors = _errors;
            return result;
        }

        private void GenerateRule(CssRule rule)
        {
            if (rule.Declarations.Count == 0)
                return;

            // Convert selector
            var selector = ConvertSelector(rule.Selector);
            if (string.IsNullOrWhiteSpace(selector))
                return;

            _sb.AppendLine($"{selector} {{");

            foreach (var decl in rule.Declarations)
            {
                GenerateDeclaration(decl, rule.Selector);
            }

            _sb.AppendLine("}");
            _sb.AppendLine();
        }

        private string ConvertSelector(string cssSelector)
        {
            var selector = cssSelector.Trim();

            // Remove pseudo-elements (::before, ::after)
            selector = Regex.Replace(selector, @"::(before|after|first-line|first-letter|selection)", "", RegexOptions.IgnoreCase);

            // Convert HTML element selectors to Unity types
            selector = ConvertElementSelector(selector);

            // Handle :hover, :active, :focus (keep them for USS)
            // USS supports :hover, :active, :focus, :focus-visible

            // Warn about complex selectors
            if (selector.Contains(":nth-child") || selector.Contains(":nth-of-type"))
            {
                _warnings.Add(new UssConversionWarning
                {
                    Selector = cssSelector,
                    Message = $"Complex selector '{cssSelector}' may not work correctly in USS",
                    Severity = WarningSeverity.Warning
                });
            }

            return selector;
        }

        private string ConvertElementSelector(string selector)
        {
            // Map HTML elements to Unity element types
            var elementMap = new Dictionary<string, string>
            {
                { "div", "visual-element" },
                { "span", "visual-element" },
                { "button", "unity-button" },
                { "input", "unity-text-field" },
                { "label", "unity-label" },
                { "img", "unity-image" },
                { "select", "unity-dropdown-field" },
                { "textarea", "unity-text-field" },
                { "progress", "unity-progress-bar" },
                { "ul", "unity-list-view" },
                { "ol", "unity-list-view" },
                { "h1", "unity-label" },
                { "h2", "unity-label" },
                { "h3", "unity-label" },
                { "h4", "unity-label" },
                { "h5", "unity-label" },
                { "h6", "unity-label" },
                { "p", "unity-label" }
            };

            foreach (var map in elementMap)
            {
                // Replace element selector at word boundary
                selector = Regex.Replace(selector, $@"\b{map.Key}\b", map.Value, RegexOptions.IgnoreCase);
            }

            return selector;
        }

        private void GenerateDeclaration(CssDeclaration decl, string selector)
        {
            var property = decl.Property.ToLowerInvariant().Trim();
            var value = decl.Value.Trim();

            // Handle shorthand properties
            if (property == "flex")
            {
                ExpandFlexShorthand(value, selector);
                return;
            }

            if (property == "margin" || property == "padding" || property == "border-width" || property == "border-color" || property == "border-radius")
            {
                // Keep shorthand, Unity supports it
                _sb.AppendLine($"    {property}: {value};");
                return;
            }

            // Check if unsupported
            if (UnsupportedProperties.Contains(property))
            {
                _warnings.Add(new UssConversionWarning
                {
                    Selector = selector,
                    Property = property,
                    OriginalValue = value,
                    Message = GetUnsupportedMessage(property),
                    Severity = property == "gap" ? WarningSeverity.Error : WarningSeverity.Warning
                });
                return;
            }

            // Handle display
            if (property == "display")
            {
                var displayValue = value.ToLowerInvariant();
                if (displayValue == "grid" || displayValue == "inline-grid")
                {
                    _warnings.Add(new UssConversionWarning
                    {
                        Selector = selector,
                        Property = property,
                        OriginalValue = value,
                        Message = "CSS Grid is not supported. Use Flexbox instead.",
                        Severity = WarningSeverity.Error
                    });
                    return;
                }
                if (displayValue == "block" || displayValue == "inline" || displayValue == "inline-block")
                {
                    _warnings.Add(new UssConversionWarning
                    {
                        Selector = selector,
                        Property = property,
                        OriginalValue = value,
                        ConvertedValue = "flex",
                        Message = $"display: {displayValue} converted to flex",
                        Severity = WarningSeverity.Info
                    });
                    _sb.AppendLine("    display: flex;");
                    if (displayValue == "block")
                    {
                        _sb.AppendLine("    flex-direction: column;");
                    }
                    return;
                }
                _sb.AppendLine($"    display: {value};");
                return;
            }

            // Handle position
            if (property == "position")
            {
                var posValue = value.ToLowerInvariant();
                if (posValue == "fixed" || posValue == "sticky")
                {
                    _warnings.Add(new UssConversionWarning
                    {
                        Selector = selector,
                        Property = property,
                        OriginalValue = value,
                        ConvertedValue = posValue == "fixed" ? "absolute" : "relative",
                        Message = $"position: {posValue} is not supported, converted to {(posValue == "fixed" ? "absolute" : "relative")}",
                        Severity = WarningSeverity.Warning
                    });
                    _sb.AppendLine($"    position: {(posValue == "fixed" ? "absolute" : "relative")};");
                    return;
                }
                _sb.AppendLine($"    position: {value};");
                return;
            }

            // Handle background-image
            if (property == "background-image")
            {
                var convertedValue = ConvertBackgroundImage(value);
                _sb.AppendLine($"    background-image: {convertedValue};");
                return;
            }

            // Handle background with gradient
            if (property == "background")
            {
                if (value.Contains("gradient"))
                {
                    _warnings.Add(new UssConversionWarning
                    {
                        Selector = selector,
                        Property = property,
                        OriginalValue = value,
                        Message = "CSS gradients are not supported. Use solid color or image.",
                        Severity = WarningSeverity.Warning
                    });
                    return;
                }
                // Try to extract solid color
                var colorMatch = Regex.Match(value, @"#[0-9a-fA-F]+|rgb\([^)]+\)|rgba\([^)]+\)");
                if (colorMatch.Success)
                {
                    _sb.AppendLine($"    background-color: {colorMatch.Value};");
                }
                return;
            }

            // Handle text-align
            if (property == "text-align")
            {
                var ussValue = TextAlignValues.TryGetValue(value.ToLowerInvariant(), out var mapped)
                    ? mapped
                    : "middle-left";
                _warnings.Add(new UssConversionWarning
                {
                    Selector = selector,
                    Property = property,
                    OriginalValue = value,
                    ConvertedValue = ussValue,
                    Message = $"text-align converted to -unity-text-align",
                    Severity = WarningSeverity.Info
                });
                _sb.AppendLine($"    -unity-text-align: {ussValue};");
                return;
            }

            // Handle font-weight
            if (property == "font-weight")
            {
                var ussValue = FontWeightValues.TryGetValue(value.ToLowerInvariant(), out var mapped)
                    ? mapped
                    : (int.TryParse(value, out int weight) ? (weight >= 600 ? "bold" : "normal") : "normal");
                _sb.AppendLine($"    -unity-font-style: {ussValue};");
                return;
            }

            // Handle font-style
            if (property == "font-style")
            {
                // Note: This should combine with font-weight in real implementation
                var ussValue = value.ToLowerInvariant() == "italic" ? "italic" : "normal";
                _sb.AppendLine($"    -unity-font-style: {ussValue};");
                return;
            }

            // Handle cursor
            if (property == "cursor")
            {
                _warnings.Add(new UssConversionWarning
                {
                    Selector = selector,
                    Property = property,
                    OriginalValue = value,
                    Message = "cursor property is not supported in Runtime UI. Use texture cursor.",
                    Severity = WarningSeverity.Warning
                });
                return;
            }

            // Handle transform
            if (property == "transform")
            {
                _warnings.Add(new UssConversionWarning
                {
                    Selector = selector,
                    Property = property,
                    OriginalValue = value,
                    Message = "transform has limited support in USS",
                    Severity = WarningSeverity.Warning
                });
                _sb.AppendLine($"    /* transform: {value}; */");
                return;
            }

            // Handle transition
            if (property == "transition")
            {
                _warnings.Add(new UssConversionWarning
                {
                    Selector = selector,
                    Property = property,
                    OriginalValue = value,
                    Message = "transition is supported in USS but may behave differently",
                    Severity = WarningSeverity.Info
                });
                _sb.AppendLine($"    {property}: {value};");
                return;
            }

            // Handle animation
            if (property == "animation")
            {
                _warnings.Add(new UssConversionWarning
                {
                    Selector = selector,
                    Property = property,
                    OriginalValue = value,
                    Message = "animation is not supported. Use USS transition or code-based animation.",
                    Severity = WarningSeverity.Warning
                });
                return;
            }

            // Direct pass-through
            if (DirectProperties.Contains(property))
            {
                _sb.AppendLine($"    {property}: {value};");
                return;
            }

            // Unknown property - pass through with warning
            _warnings.Add(new UssConversionWarning
            {
                Selector = selector,
                Property = property,
                OriginalValue = value,
                Message = $"Unknown or unsupported CSS property: {property}",
                Severity = WarningSeverity.Warning
            });
        }

        private void ExpandFlexShorthand(string value, string selector)
        {
            // flex: <flex-grow> <flex-shrink>? <flex-basis>?
            var parts = value.Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);

            if (parts.Length == 0) return;

            var flexGrow = "1";
            var flexShrink = "1";
            var flexBasis = "auto";

            if (parts.Length >= 1)
            {
                if (parts[0] == "none")
                {
                    flexGrow = "0";
                    flexShrink = "0";
                    flexBasis = "auto";
                }
                else if (parts[0] == "auto")
                {
                    flexGrow = "1";
                    flexShrink = "1";
                    flexBasis = "auto";
                }
                else
                {
                    flexGrow = parts[0];
                }
            }
            if (parts.Length >= 2)
            {
                flexShrink = parts[1];
            }
            if (parts.Length >= 3)
            {
                flexBasis = parts[2];
            }

            _sb.AppendLine($"    flex-grow: {flexGrow};");
            _sb.AppendLine($"    flex-shrink: {flexShrink};");
            _sb.AppendLine($"    flex-basis: {flexBasis};");
        }

        private string ConvertBackgroundImage(string value)
        {
            // url('path') -> resource('path')
            var urlMatch = Regex.Match(value, @"url\(['""]?([^'""\)]+)['""]?\)");
            if (urlMatch.Success)
            {
                var path = urlMatch.Groups[1].Value;
                return $"resource('{path}')";
            }

            return value;
        }

        private string GetUnsupportedMessage(string property)
        {
            switch (property)
            {
                case "gap":
                case "row-gap":
                case "column-gap":
                    return $"{property} is not supported. Use margin on child elements instead.";
                case "box-shadow":
                    return "box-shadow is not supported. Use layered elements or 9-slice images.";
                case "filter":
                    return "filter is not supported. Use -unity-material or custom shaders.";
                case "clip-path":
                case "mask":
                    return $"{property} is not supported.";
                default:
                    return $"{property} is not supported in USS.";
            }
        }
    }
}