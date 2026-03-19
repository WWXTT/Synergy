using System;
using System.Collections.Generic;
using System.Text;
using System.Text.RegularExpressions;

namespace UIConverter.Parsers
{
    /// <summary>
    /// Represents a parsed HTML element
    /// </summary>
    public class HtmlElement
    {
        public string TagName { get; set; }
        public string Id { get; set; }
        public List<string> Classes { get; set; } = new List<string>();
        public Dictionary<string, string> Attributes { get; set; } = new Dictionary<string, string>();
        public string InnerText { get; set; }
        public List<HtmlElement> Children { get; set; } = new List<HtmlElement>();
        public HtmlElement Parent { get; set; }
        public bool IsSelfClosing { get; set; }
        public string InputType { get; set; } = "text";
    }

    /// <summary>
    /// Result of HTML parsing
    /// </summary>
    public class HtmlParseResult
    {
        public HtmlElement Root { get; set; }
        public string ExtractedCss { get; set; }
        public List<string> Errors { get; set; } = new List<string>();
        public List<string> Warnings { get; set; } = new List<string>();
    }

    /// <summary>
    /// Simple HTML parser that extracts structure for conversion to UXML
    /// </summary>
    public class HtmlParser
    {
        private static readonly HashSet<string> SelfClosingTags = new HashSet<string>
        {
            "img", "input", "br", "hr", "meta", "link", "area", "base", "col", "embed", "param", "source", "track", "wbr"
        };

        private static readonly HashSet<string> UnsupportedElements = new HashSet<string>
        {
            "table", "tr", "td", "th", "thead", "tbody", "tfoot", "caption",
            "canvas", "video", "audio", "iframe", "svg", "math",
            "form", "fieldset", "legend", "datalist", "output",
            "script", "style", "noscript", "template"
        };

        private string _html;
        private int _position;
        private HtmlParseResult _result;

        public HtmlParseResult Parse(string html)
        {
            _html = html ?? string.Empty;
            _position = 0;
            _result = new HtmlParseResult();

            try
            {
                // Remove DOCTYPE and comments
                PreprocessHtml();

                // Parse the document
                var root = new HtmlElement { TagName = "root" };
                ParseChildren(root);

                // Find the body or return all children
                if (root.Children.Count > 0)
                {
                    var body = FindElement(root, "body");
                    _result.Root = body ?? root.Children[0];
                }
                else
                {
                    _result.Errors.Add("No HTML content found");
                }
            }
            catch (Exception ex)
            {
                _result.Errors.Add($"Parse error: {ex.Message}");
            }

            return _result;
        }

        private void PreprocessHtml()
        {
            // Extract style tag content before removing
            var styleMatch = Regex.Match(_html, @"<style[^>]*>(.*?)</style>", RegexOptions.Singleline | RegexOptions.IgnoreCase);
            if (styleMatch.Success)
            {
                _result.ExtractedCss = styleMatch.Groups[1].Value.Trim();
            }

            // Remove DOCTYPE
            _html = Regex.Replace(_html, @"<!DOCTYPE[^>]*>", "", RegexOptions.IgnoreCase);

            // Remove comments
            _html = Regex.Replace(_html, @"<!--.*?-->", "", RegexOptions.Singleline);

            // Remove script tags and content
            _html = Regex.Replace(_html, @"<script[^>]*>.*?</script>", "", RegexOptions.Singleline | RegexOptions.IgnoreCase);

            // Remove style tags and content
            _html = Regex.Replace(_html, @"<style[^>]*>.*?</style>", "", RegexOptions.Singleline | RegexOptions.IgnoreCase);

            // Normalize whitespace
            _html = Regex.Replace(_html, @"\s+", " ");
        }

        private void ParseChildren(HtmlElement parent)
        {
            while (_position < _html.Length)
            {
                SkipWhitespace();

                if (_position >= _html.Length)
                    break;

                if (Peek() == '<')
                {
                    if (Peek(1) == '/')
                    {
                        // Closing tag - return to parent
                        break;
                    }

                    var element = ParseElement();
                    if (element != null)
                    {
                        element.Parent = parent;
                        parent.Children.Add(element);
                    }
                }
                else
                {
                    // Text content
                    var text = ParseText();
                    if (!string.IsNullOrWhiteSpace(text))
                    {
                        parent.InnerText = text.Trim();
                    }
                }
            }
        }

        private HtmlElement ParseElement()
        {
            if (Peek() != '<')
                return null;

            _position++; // Skip '<'

            // Parse tag name
            var tagName = ParseTagName();
            if (string.IsNullOrEmpty(tagName))
            {
                SkipToEndOfTag();
                return null;
            }

            var element = new HtmlElement
            {
                TagName = tagName.ToLowerInvariant(),
                IsSelfClosing = SelfClosingTags.Contains(tagName.ToLowerInvariant())
            };

            // Check for unsupported elements
            if (UnsupportedElements.Contains(element.TagName))
            {
                _result.Warnings.Add($"Unsupported element '<{element.TagName}>' will be skipped or converted to VisualElement");
            }

            // Parse attributes
            ParseAttributes(element);

            // Check for self-closing syntax
            if (Peek() == '/' && Peek(1) == '>')
            {
                _position += 2;
                return element;
            }

            // Skip '>'
            if (Peek() == '>')
                _position++;

            // Handle input type
            if (element.TagName == "input" && element.Attributes.TryGetValue("type", out var inputType))
            {
                element.InputType = inputType.ToLowerInvariant();
            }

            // Parse children for non-self-closing elements
            if (!element.IsSelfClosing && !VoidElement(element.TagName))
            {
                ParseChildren(element);
                SkipClosingTag(element.TagName);
            }

            return element;
        }

        private string ParseTagName()
        {
            SkipWhitespace();
            var start = _position;

            while (_position < _html.Length)
            {
                var c = _html[_position];
                if (char.IsWhiteSpace(c) || c == '>' || c == '/' || c == '=')
                    break;
                _position++;
            }

            return _html.Substring(start, _position - start);
        }

        private void ParseAttributes(HtmlElement element)
        {
            while (_position < _html.Length)
            {
                SkipWhitespace();

                if (Peek() == '>' || Peek() == '/')
                    break;

                var attrName = ParseAttributeName();
                if (string.IsNullOrEmpty(attrName))
                    break;

                string attrValue = null;
                if (Peek() == '=')
                {
                    _position++;
                    attrValue = ParseAttributeValue();
                }

                switch (attrName.ToLowerInvariant())
                {
                    case "id":
                        element.Id = attrValue;
                        break;
                    case "class":
                        if (!string.IsNullOrEmpty(attrValue))
                        {
                            element.Classes.AddRange(attrValue.Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries));
                        }
                        break;
                    default:
                        element.Attributes[attrName] = attrValue ?? string.Empty;
                        break;
                }
            }
        }

        private string ParseAttributeName()
        {
            SkipWhitespace();
            var start = _position;

            while (_position < _html.Length)
            {
                var c = _html[_position];
                if (char.IsWhiteSpace(c) || c == '>' || c == '/' || c == '=')
                    break;
                _position++;
            }

            return _html.Substring(start, _position - start);
        }

        private string ParseAttributeValue()
        {
            SkipWhitespace();

            if (Peek() == '"' || Peek() == '\'')
            {
                var quote = _html[_position];
                _position++;
                var start = _position;

                while (_position < _html.Length && _html[_position] != quote)
                    _position++;

                var value = _html.Substring(start, _position - start);
                if (_position < _html.Length)
                    _position++; // Skip closing quote

                return value;
            }
            else
            {
                // Unquoted value
                var start = _position;
                while (_position < _html.Length)
                {
                    var c = _html[_position];
                    if (char.IsWhiteSpace(c) || c == '>' || c == '/')
                        break;
                    _position++;
                }
                return _html.Substring(start, _position - start);
            }
        }

        private string ParseText()
        {
            var start = _position;
            while (_position < _html.Length && Peek() != '<')
                _position++;

            var text = _html.Substring(start, _position - start);
            return System.Net.WebUtility.HtmlDecode(text);
        }

        private void SkipWhitespace()
        {
            while (_position < _html.Length && char.IsWhiteSpace(_html[_position]))
                _position++;
        }

        private void SkipToEndOfTag()
        {
            while (_position < _html.Length && Peek() != '>')
                _position++;
            if (_position < _html.Length)
                _position++;
        }

        private void SkipClosingTag(string tagName)
        {
            var closeTag = $"</{tagName}";
            var idx = _html.IndexOf(closeTag, _position, StringComparison.OrdinalIgnoreCase);
            if (idx >= 0)
            {
                _position = idx + closeTag.Length;
                while (_position < _html.Length && Peek() != '>')
                    _position++;
                if (_position < _html.Length)
                    _position++;
            }
        }

        private char Peek(int offset = 0)
        {
            var pos = _position + offset;
            return pos < _html.Length ? _html[pos] : '\0';
        }

        private bool VoidElement(string tagName)
        {
            var voidElements = new HashSet<string> { "br", "hr", "img", "input", "meta", "link" };
            return SelfClosingTags.Contains(tagName) || voidElements.Contains(tagName);
        }

        private HtmlElement FindElement(HtmlElement parent, string tagName)
        {
            foreach (var child in parent.Children)
            {
                if (child.TagName.Equals(tagName, StringComparison.OrdinalIgnoreCase))
                    return child;

                var found = FindElement(child, tagName);
                if (found != null)
                    return found;
            }
            return null;
        }
    }
}