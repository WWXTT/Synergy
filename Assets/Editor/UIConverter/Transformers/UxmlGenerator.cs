using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Linq;
using UIConverter.Parsers;
using UnityEngine;

namespace UIConverter.Transformers
{
    /// <summary>
    /// Result of UXML generation
    /// </summary>
    public class UxmlGenerateResult
    {
        public string UxmlContent { get; set; }
        public List<string> Warnings { get; set; } = new List<string>();
        public List<string> Errors { get; set; } = new List<string>();
    }

    /// <summary>
    /// Generates UXML from parsed HTML
    /// </summary>
    public class UxmlGenerator
    {
        private ElementMappingConfig _config;
        private StringBuilder _sb;
        private int _indentLevel;
        private List<string> _warnings;
        private List<string> _errors;

        public UxmlGenerator(ElementMappingConfig config = null)
        {
            _config = config ?? new ElementMappingConfig();
        }

        public UxmlGenerateResult Generate(HtmlElement root, string styleSheetPath = null)
        {
            _sb = new StringBuilder();
            _indentLevel = 0;
            _warnings = new List<string>();
            _errors = new List<string>();

            var result = new UxmlGenerateResult();

            try
            {
                // Write UXML header
                AppendLine("<ui:UXML xmlns:ui=\"UnityEngine.UIElements\">");

                _indentLevel = 1;

                // Add style sheet reference if provided
                if (!string.IsNullOrEmpty(styleSheetPath))
                {
                    AppendLine($"<Style src=\"{styleSheetPath}\" />");
                }

                // Generate elements recursively
                GenerateElement(root);

                // Close UXML
                AppendLine("</ui:UXML>");

                result.UxmlContent = _sb.ToString();
            }
            catch (Exception ex)
            {
                _errors.Add($"UXML generation error: {ex.Message}");
            }

            result.Warnings = _warnings;
            result.Errors = _errors;
            return result;
        }

        private void GenerateElement(HtmlElement element)
        {
            if (element == null) return;

            // Skip root element
            if (element.TagName == "root")
            {
                foreach (var child in element.Children)
                {
                    GenerateElement(child);
                }
                return;
            }

            // Get mapping for this element
            var mapping = GetElementMapping(element);
            if (mapping == null)
            {
                _warnings.Add($"Unknown element '<{element.TagName}>' skipped");
                return;
            }

            var uxmlTag = mapping.UxmlElement;

            // Build element string
            var attributes = BuildAttributes(element, mapping);

            // Check if element has children or text
            bool hasContent = element.Children.Count > 0 || !string.IsNullOrWhiteSpace(element.InnerText);

            if (hasContent)
            {
                // Opening tag
                AppendIndent();
                _sb.Append($"<ui:{uxmlTag}");

                foreach (var attr in attributes)
                {
                    _sb.Append($" {attr.Key}=\"{EscapeXml(attr.Value)}\"");
                }

                _sb.AppendLine(">");

                _indentLevel++;

                // Add text content
                if (!string.IsNullOrWhiteSpace(element.InnerText))
                {
                    if (mapping.TextAttribute != null)
                    {
                        // Text will be added as attribute, handled in BuildAttributes
                    }
                }

                // Generate children
                foreach (var child in element.Children)
                {
                    GenerateElement(child);
                }

                _indentLevel--;

                // Closing tag
                AppendLine($"</ui:{uxmlTag}>");
            }
            else
            {
                // Self-closing tag
                AppendIndent();
                _sb.Append($"<ui:{uxmlTag}");

                foreach (var attr in attributes)
                {
                    _sb.Append($" {attr.Key}=\"{EscapeXml(attr.Value)}\"");
                }

                _sb.AppendLine(" />");
            }
        }

        private ElementMapping GetElementMapping(HtmlElement element)
        {
            var tagName = element.TagName.ToLowerInvariant();

            // Handle input with type
            if (tagName == "input")
            {
                return _config.GetInputMapping(element.InputType);
            }

            return _config.GetMapping(tagName);
        }

        private Dictionary<string, string> BuildAttributes(HtmlElement element, ElementMapping mapping)
        {
            var attributes = new Dictionary<string, string>();

            // Name (from id)
            if (!string.IsNullOrEmpty(element.Id))
            {
                attributes["name"] = element.Id;
            }

            // Classes
            var classes = new List<string>(element.Classes);
            if (!string.IsNullOrEmpty(mapping.DefaultClass))
            {
                classes.Insert(0, mapping.DefaultClass);
            }
            if (classes.Count > 0)
            {
                attributes["class"] = string.Join(" ", classes);
            }

            // Text content (for elements with textAttribute)
            if (!string.IsNullOrWhiteSpace(element.InnerText) && !string.IsNullOrEmpty(mapping.TextAttribute))
            {
                attributes[mapping.TextAttribute] = element.InnerText.Trim();
            }

            // Default attributes from mapping
            if (mapping.DefaultAttributes != null)
            {
                foreach (var attr in mapping.DefaultAttributes)
                {
                    attributes[attr.Key] = attr.Value;
                }
            }

            // Map HTML attributes to UXML
            foreach (var attr in element.Attributes)
            {
                var uxmlAttr = MapAttribute(attr.Key, attr.Value);
                if (uxmlAttr.HasValue)
                {
                    attributes[uxmlAttr.Value.Key] = uxmlAttr.Value.Value;
                }
            }

            return attributes;
        }

        private KeyValuePair<string, string>? MapAttribute(string htmlAttr, string value)
        {
            switch (htmlAttr.ToLowerInvariant())
            {
                case "id":
                    return new KeyValuePair<string, string>("name", value);
                case "class":
                    return new KeyValuePair<string, string>("class", value);
                case "disabled":
                    return new KeyValuePair<string, string>("enabled", "false");
                case "readonly":
                    return new KeyValuePair<string, string>("isReadOnly", "true");
                case "placeholder":
                    return new KeyValuePair<string, string>("placeholder", value);
                case "value":
                    return new KeyValuePair<string, string>("value", value);
                case "maxlength":
                    return new KeyValuePair<string, string>("maxLength", value);
                case "min":
                    return new KeyValuePair<string, string>("lowValue", value);
                case "max":
                    return new KeyValuePair<string, string>("highValue", value);
                case "step":
                    return new KeyValuePair<string, string>("pageSize", value);
                case "src":
                    return new KeyValuePair<string, string>("source", value);
                case "alt":
                    return new KeyValuePair<string, string>("tooltip", value);
                case "type":
                case "style":
                    // These are handled specially
                    return null;
                default:
                    // Pass through unknown attributes
                    return new KeyValuePair<string, string>(htmlAttr, value);
            }
        }

        private string EscapeXml(string value)
        {
            if (string.IsNullOrEmpty(value)) return value;

            return value
                .Replace("&", "&amp;")
                .Replace("<", "&lt;")
                .Replace(">", "&gt;")
                .Replace("\"", "&quot;")
                .Replace("'", "&apos;");
        }

        private void AppendIndent()
        {
            _sb.Append(new string(' ', _indentLevel * 4));
        }

        private void AppendLine(string line)
        {
            AppendIndent();
            _sb.AppendLine(line);
        }
    }

    #region Config Classes

    /// <summary>
    /// Configuration for element mapping (loaded from JSON)
    /// </summary>
    public class ElementMappingConfig
    {
        private Dictionary<string, ElementMapping> _mappings;
        private Dictionary<string, ElementMapping> _inputMappings;
        private ElementMapping _defaultInputMapping;

        public ElementMappingConfig()
        {
            // Default mappings
            _mappings = new Dictionary<string, ElementMapping>
            {
                { "div", new ElementMapping { UxmlElement = "VisualElement" } },
                { "span", new ElementMapping { UxmlElement = "VisualElement" } },
                { "section", new ElementMapping { UxmlElement = "VisualElement" } },
                { "header", new ElementMapping { UxmlElement = "VisualElement", DefaultClass = "header" } },
                { "footer", new ElementMapping { UxmlElement = "VisualElement", DefaultClass = "footer" } },
                { "nav", new ElementMapping { UxmlElement = "VisualElement", DefaultClass = "nav" } },
                { "main", new ElementMapping { UxmlElement = "VisualElement", DefaultClass = "main" } },
                { "article", new ElementMapping { UxmlElement = "VisualElement" } },
                { "aside", new ElementMapping { UxmlElement = "VisualElement" } },

                { "button", new ElementMapping { UxmlElement = "Button", TextAttribute = "text" } },
                { "a", new ElementMapping { UxmlElement = "Button", TextAttribute = "text", DefaultClass = "link" } },

                { "label", new ElementMapping { UxmlElement = "Label", TextAttribute = "text" } },
                { "p", new ElementMapping { UxmlElement = "Label", TextAttribute = "text", DefaultClass = "paragraph" } },
                { "h1", new ElementMapping { UxmlElement = "Label", TextAttribute = "text", DefaultClass = "h1" } },
                { "h2", new ElementMapping { UxmlElement = "Label", TextAttribute = "text", DefaultClass = "h2" } },
                { "h3", new ElementMapping { UxmlElement = "Label", TextAttribute = "text", DefaultClass = "h3" } },
                { "h4", new ElementMapping { UxmlElement = "Label", TextAttribute = "text", DefaultClass = "h4" } },
                { "h5", new ElementMapping { UxmlElement = "Label", TextAttribute = "text", DefaultClass = "h5" } },
                { "h6", new ElementMapping { UxmlElement = "Label", TextAttribute = "text", DefaultClass = "h6" } },

                { "img", new ElementMapping { UxmlElement = "Image" } },
                { "progress", new ElementMapping { UxmlElement = "ProgressBar" } },
                { "select", new ElementMapping { UxmlElement = "DropdownField" } },
                { "textarea", new ElementMapping { UxmlElement = "TextField", DefaultAttributes = new Dictionary<string, string> { { "multiline", "true" } } } },

                { "ul", new ElementMapping { UxmlElement = "ListView" } },
                { "ol", new ElementMapping { UxmlElement = "ListView" } },
                { "li", new ElementMapping { UxmlElement = "VisualElement", DefaultClass = "list-item" } },
            };

            _inputMappings = new Dictionary<string, ElementMapping>
            {
                { "text", new ElementMapping { UxmlElement = "TextField" } },
                { "password", new ElementMapping { UxmlElement = "TextField", DefaultAttributes = new Dictionary<string, string> { { "password", "true" } } } },
                { "number", new ElementMapping { UxmlElement = "FloatField" } },
                { "checkbox", new ElementMapping { UxmlElement = "Toggle" } },
                { "radio", new ElementMapping { UxmlElement = "RadioButton" } },
                { "range", new ElementMapping { UxmlElement = "Slider" } },
                { "email", new ElementMapping { UxmlElement = "TextField" } },
                { "tel", new ElementMapping { UxmlElement = "TextField" } },
                { "url", new ElementMapping { UxmlElement = "TextField" } },
                { "search", new ElementMapping { UxmlElement = "TextField" } },
            };

            _defaultInputMapping = new ElementMapping { UxmlElement = "TextField" };
        }

        public ElementMapping GetMapping(string tagName)
        {
            return _mappings.TryGetValue(tagName.ToLowerInvariant(), out var mapping)
                ? mapping
                : new ElementMapping { UxmlElement = "VisualElement" };
        }

        public ElementMapping GetInputMapping(string inputType)
        {
            return _inputMappings.TryGetValue(inputType?.ToLowerInvariant() ?? "text", out var mapping)
                ? mapping
                : _defaultInputMapping;
        }

        public void LoadFromJson(string json)
        {
            // TODO: Parse JSON configuration
        }
    }

    public class ElementMapping
    {
        public string UxmlElement { get; set; }
        public string TextAttribute { get; set; }
        public string DefaultClass { get; set; }
        public Dictionary<string, string> DefaultAttributes { get; set; }
    }

    #endregion
}