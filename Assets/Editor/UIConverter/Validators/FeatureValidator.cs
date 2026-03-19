using System;
using System.Collections.Generic;
using System.Text;
using UIConverter.Parsers;

namespace UIConverter.Validators
{
    /// <summary>
    /// Validation result for HTML/CSS to Unity conversion
    /// </summary>
    public class ValidationResult
    {
        public bool IsValid { get; set; }
        public List<ValidationIssue> Issues { get; set; } = new List<ValidationIssue>();
        public List<string> Suggestions { get; set; } = new List<string>();

        public bool HasErrors => Issues.Exists(i => i.Severity == ValidationSeverity.Error);
        public bool HasWarnings => Issues.Exists(i => i.Severity == ValidationSeverity.Warning);
    }

    public class ValidationIssue
    {
        public ValidationSeverity Severity { get; set; }
        public string Category { get; set; } // "HTML" or "CSS"
        public string Location { get; set; } // Element or selector
        public string Property { get; set; }
        public string Value { get; set; }
        public string Message { get; set; }
        public string Alternative { get; set; }
    }

    public enum ValidationSeverity
    {
        Info,
        Warning,
        Error
    }

    /// <summary>
    /// Validates HTML/CSS for Unity UIToolkit compatibility
    /// </summary>
    public class FeatureValidator
    {
        // Unsupported HTML elements
        private static readonly HashSet<string> UnsupportedElements = new HashSet<string>
        {
            "table", "tr", "td", "th", "thead", "tbody", "tfoot", "caption", "colgroup", "col",
            "canvas", "video", "audio", "iframe", "embed", "object", "param",
            "svg", "math", "template", "slot",
            "form", "fieldset", "legend", "datalist", "output", "meter",
            "details", "summary", "dialog",
            "script", "style", "noscript"
        };

        // Elements with warnings (may need special handling)
        private static readonly HashSet<string> WarningElements = new HashSet<string>
        {
            "form", "input", "select", "textarea" // Need specific attributes
        };

        // Unsupported CSS properties
        private static readonly Dictionary<string, ValidationIssue> UnsupportedCssProperties = new Dictionary<string, ValidationIssue>
        {
            { "display:grid", new ValidationIssue { Severity = ValidationSeverity.Error, Message = "CSS Grid is not supported", Alternative = "Use Flexbox with flex-wrap" } },
            { "display:inline-grid", new ValidationIssue { Severity = ValidationSeverity.Error, Message = "CSS Grid is not supported", Alternative = "Use Flexbox with flex-wrap" } },
            { "position:fixed", new ValidationIssue { Severity = ValidationSeverity.Warning, Message = "position:fixed is not supported", Alternative = "Use position:absolute" } },
            { "position:sticky", new ValidationIssue { Severity = ValidationSeverity.Warning, Message = "position:sticky is not supported", Alternative = "Use position:relative" } },
            { "float", new ValidationIssue { Severity = ValidationSeverity.Error, Message = "float is not supported", Alternative = "Use Flexbox" } },
            { "clear", new ValidationIssue { Severity = ValidationSeverity.Error, Message = "clear is not supported", Alternative = "Use Flexbox" } },
        };

        // CSS properties that need transformation
        private static readonly Dictionary<string, string> PropertyWarnings = new Dictionary<string, string>
        {
            { "gap", "gap is not supported. Use margin on child elements." },
            { "row-gap", "row-gap is not supported. Use margin on child elements." },
            { "column-gap", "column-gap is not supported. Use margin on child elements." },
            { "box-shadow", "box-shadow is not supported. Use layered elements or 9-slice images." },
            { "filter", "filter is not supported. Use -unity-material or shaders." },
            { "transform", "transform has limited support in USS." },
            { "animation", "animation is not supported. Use USS transition or code animation." },
            { "cursor", "cursor is not supported in Runtime UI." },
            { "clip-path", "clip-path is not supported." },
            { "mask", "mask is not supported." },
        };

        // Unsupported pseudo-selectors
        private static readonly HashSet<string> UnsupportedPseudoElements = new HashSet<string>
        {
            "::before", "::after", "::first-line", "::first-letter", "::selection"
        };

        private static readonly HashSet<string> LimitedPseudoClasses = new HashSet<string>
        {
            ":nth-child", ":nth-of-type", ":first-child", ":last-child", ":only-child", ":nth-last-child", ":nth-last-of-type"
        };

        public ValidationResult ValidateHtml(HtmlElement root)
        {
            var result = new ValidationResult { IsValid = true };

            ValidateHtmlElement(root, result);

            return result;
        }

        private void ValidateHtmlElement(HtmlElement element, ValidationResult result)
        {
            if (element == null) return;

            var tagName = element.TagName.ToLowerInvariant();

            // Check for unsupported elements
            if (UnsupportedElements.Contains(tagName))
            {
                var issue = new ValidationIssue
                {
                    Severity = ValidationSeverity.Error,
                    Category = "HTML",
                    Location = $"<{tagName}>",
                    Message = $"'{tagName}' element has no equivalent in UIToolkit",
                    Alternative = GetAlternative(tagName)
                };
                result.Issues.Add(issue);
                result.IsValid = false;
            }

            // Check for elements needing special handling
            if (WarningElements.Contains(tagName))
            {
                if (tagName == "form")
                {
                    result.Issues.Add(new ValidationIssue
                    {
                        Severity = ValidationSeverity.Info,
                        Category = "HTML",
                        Location = "<form>",
                        Message = "form element will be converted to VisualElement container",
                        Alternative = "Use div for container, handle form logic in code"
                    });
                }
            }

            // Validate attributes
            foreach (var attr in element.Attributes)
            {
                ValidateAttribute(tagName, attr.Key, attr.Value, result);
            }

            // Check for inline styles
            if (element.Attributes.ContainsKey("style"))
            {
                result.Issues.Add(new ValidationIssue
                {
                    Severity = ValidationSeverity.Info,
                    Category = "HTML",
                    Location = $"<{tagName}>",
                    Property = "style",
                    Message = "Inline styles will be extracted to USS file",
                });
            }

            // Recurse into children
            foreach (var child in element.Children)
            {
                ValidateHtmlElement(child, result);
            }
        }

        private void ValidateAttribute(string tagName, string attrName, string attrValue, ValidationResult result)
        {
            // Check for event handlers
            if (attrName.StartsWith("on", StringComparison.OrdinalIgnoreCase))
            {
                result.Issues.Add(new ValidationIssue
                {
                    Severity = ValidationSeverity.Warning,
                    Category = "HTML",
                    Location = $"<{tagName}>",
                    Property = attrName,
                    Message = $"Event handler '{attrName}' will be ignored. Handle events in C# code.",
                    Alternative = "Register callbacks in C# using element.RegisterCallback()"
                });
            }

            // Check for data attributes
            if (attrName.StartsWith("data-", StringComparison.OrdinalIgnoreCase))
            {
                result.Issues.Add(new ValidationIssue
                {
                    Severity = ValidationSeverity.Info,
                    Category = "HTML",
                    Location = $"<{tagName}>",
                    Property = attrName,
                    Message = $"data attribute '{attrName}' will be preserved but may need custom handling"
                });
            }
        }

        public ValidationResult ValidateCss(CssParseResult cssResult)
        {
            var result = new ValidationResult { IsValid = true };

            foreach (var rule in cssResult.Rules)
            {
                ValidateCssRule(rule, result);
            }

            return result;
        }

        private void ValidateCssRule(CssRule rule, ValidationResult result)
        {
            var selector = rule.Selector;

            // Check for unsupported pseudo-elements
            foreach (var pseudo in UnsupportedPseudoElements)
            {
                if (selector.Contains(pseudo))
                {
                    result.Issues.Add(new ValidationIssue
                    {
                        Severity = ValidationSeverity.Error,
                        Category = "CSS",
                        Location = selector,
                        Property = pseudo,
                        Message = $"Pseudo-element '{pseudo}' is not supported in USS",
                        Alternative = "Add actual child elements to the structure"
                    });
                    result.IsValid = false;
                }
            }

            // Check for limited pseudo-classes
            foreach (var pseudo in LimitedPseudoClasses)
            {
                if (selector.Contains(pseudo))
                {
                    result.Issues.Add(new ValidationIssue
                    {
                        Severity = ValidationSeverity.Warning,
                        Category = "CSS",
                        Location = selector,
                        Property = pseudo,
                        Message = $"Pseudo-class '{pseudo}' has limited support in USS",
                        Alternative = "Use simple class selectors instead"
                    });
                }
            }

            // Check declarations
            foreach (var decl in rule.Declarations)
            {
                ValidateDeclaration(decl, selector, result);
            }
        }

        private void ValidateDeclaration(CssDeclaration decl, string selector, ValidationResult result)
        {
            var property = decl.Property.ToLowerInvariant();
            var value = decl.Value.ToLowerInvariant();

            // Check display property
            if (property == "display")
            {
                if (value.Contains("grid"))
                {
                    result.Issues.Add(new ValidationIssue
                    {
                        Severity = ValidationSeverity.Error,
                        Category = "CSS",
                        Location = selector,
                        Property = "display",
                        Value = value,
                        Message = "CSS Grid is not supported in UIToolkit",
                        Alternative = "Use Flexbox with flex-wrap: wrap"
                    });
                    result.IsValid = false;
                }
                else if (value == "block" || value == "inline" || value == "inline-block")
                {
                    result.Issues.Add(new ValidationIssue
                    {
                        Severity = ValidationSeverity.Info,
                        Category = "CSS",
                        Location = selector,
                        Property = "display",
                        Value = value,
                        Message = $"display: {value} will be converted to flex",
                        Alternative = "Use display: flex explicitly"
                    });
                }
            }

            // Check position property
            if (property == "position")
            {
                if (value == "fixed")
                {
                    result.Issues.Add(new ValidationIssue
                    {
                        Severity = ValidationSeverity.Warning,
                        Category = "CSS",
                        Location = selector,
                        Property = "position",
                        Value = value,
                        Message = "position: fixed is not supported",
                        Alternative = "Use position: absolute"
                    });
                }
                else if (value == "sticky")
                {
                    result.Issues.Add(new ValidationIssue
                    {
                        Severity = ValidationSeverity.Warning,
                        Category = "CSS",
                        Location = selector,
                        Property = "position",
                        Value = value,
                        Message = "position: sticky is not supported",
                        Alternative = "Use position: relative"
                    });
                }
            }

            // Check float
            if (property == "float" && value != "none")
            {
                result.Issues.Add(new ValidationIssue
                {
                    Severity = ValidationSeverity.Error,
                    Category = "CSS",
                    Location = selector,
                    Property = "float",
                    Value = value,
                    Message = "float is not supported in UIToolkit",
                    Alternative = "Use Flexbox layout instead"
                });
                result.IsValid = false;
            }

            // Check property warnings
            if (PropertyWarnings.TryGetValue(property, out var warning))
            {
                result.Issues.Add(new ValidationIssue
                {
                    Severity = ValidationSeverity.Warning,
                    Category = "CSS",
                    Location = selector,
                    Property = property,
                    Value = value,
                    Message = warning
                });
            }

            // Check for gradients
            if ((property == "background" || property == "background-image") && value.Contains("gradient"))
            {
                result.Issues.Add(new ValidationIssue
                {
                    Severity = ValidationSeverity.Warning,
                    Category = "CSS",
                    Location = selector,
                    Property = property,
                    Value = value,
                    Message = "CSS gradients are not supported in USS",
                    Alternative = "Use solid colors or pre-generated images"
                });
            }

            // Check for calc()
            if (value.Contains("calc("))
            {
                result.Issues.Add(new ValidationIssue
                {
                    Severity = ValidationSeverity.Warning,
                    Category = "CSS",
                    Location = selector,
                    Property = property,
                    Value = value,
                    Message = "calc() is not supported in USS",
                    Alternative = "Calculate values manually and use fixed units"
                });
            }

            // Check for viewport units
            if (value.Contains("vw") || value.Contains("vh"))
            {
                result.Issues.Add(new ValidationIssue
                {
                    Severity = ValidationSeverity.Warning,
                    Category = "CSS",
                    Location = selector,
                    Property = property,
                    Value = value,
                    Message = "Viewport units (vw/vh) are not supported in USS",
                    Alternative = "Use percentage or pixel units"
                });
            }

            // Check for rem/em units
            if (value.Contains("rem") || value.Contains("em"))
            {
                result.Issues.Add(new ValidationIssue
                {
                    Severity = ValidationSeverity.Info,
                    Category = "CSS",
                    Location = selector,
                    Property = property,
                    Value = value,
                    Message = "rem/em units will be converted to pixels",
                    Alternative = "Consider using pixel or percentage units"
                });
            }
        }

        private string GetAlternative(string tagName)
        {
            var alternatives = new Dictionary<string, string>
            {
                { "table", "Use Flexbox layout with flex-wrap to create grid-like structures" },
                { "tr", "Use VisualElement containers with flex-direction: row" },
                { "td", "Use VisualElement containers for cells" },
                { "canvas", "Use a custom mesh or texture for custom drawing" },
                { "video", "Use VideoPlayer component and texture" },
                { "audio", "Use AudioSource component" },
                { "iframe", "No equivalent - use UI integration patterns" },
                { "svg", "Convert SVG to images or use Vector Graphics package" },
                { "form", "Use VisualElement as container, handle submission in code" },
                { "script", "All logic must be in C# scripts" },
            };

            return alternatives.TryGetValue(tagName, out var alt) ? alt : "Use VisualElement as generic container";
        }

        /// <summary>
        /// Generate a summary report of validation issues
        /// </summary>
        public string GenerateReport(ValidationResult result)
        {
            var sb = new StringBuilder();

            sb.AppendLine("=== UI Converter Validation Report ===");
            sb.AppendLine();

            if (result.Issues.Count == 0)
            {
                sb.AppendLine("No issues found. The HTML/CSS is compatible with Unity UIToolkit.");
                return sb.ToString();
            }

            // Group by severity
            var errors = result.Issues.FindAll(i => i.Severity == ValidationSeverity.Error);
            var warnings = result.Issues.FindAll(i => i.Severity == ValidationSeverity.Warning);
            var infos = result.Issues.FindAll(i => i.Severity == ValidationSeverity.Info);

            if (errors.Count > 0)
            {
                sb.AppendLine($"ERRORS ({errors.Count}):");
                foreach (var error in errors)
                {
                    sb.AppendLine($"  [{error.Category}] {error.Location}: {error.Message}");
                    if (!string.IsNullOrEmpty(error.Alternative))
                        sb.AppendLine($"    Alternative: {error.Alternative}");
                }
                sb.AppendLine();
            }

            if (warnings.Count > 0)
            {
                sb.AppendLine($"WARNINGS ({warnings.Count}):");
                foreach (var warning in warnings)
                {
                    sb.AppendLine($"  [{warning.Category}] {warning.Location}: {warning.Message}");
                    if (!string.IsNullOrEmpty(warning.Alternative))
                        sb.AppendLine($"    Alternative: {warning.Alternative}");
                }
                sb.AppendLine();
            }

            if (infos.Count > 0)
            {
                sb.AppendLine($"INFO ({infos.Count}):");
                foreach (var info in infos)
                {
                    sb.AppendLine($"  [{info.Category}] {info.Location}: {info.Message}");
                }
                sb.AppendLine();
            }

            sb.AppendLine($"Total issues: {result.Issues.Count} ({errors.Count} errors, {warnings.Count} warnings, {infos.Count} info)");

            return sb.ToString();
        }
    }
}