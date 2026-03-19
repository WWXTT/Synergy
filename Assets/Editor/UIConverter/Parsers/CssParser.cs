using System;
using System.Collections.Generic;
using System.Text;
using System.Text.RegularExpressions;

namespace UIConverter.Parsers
{
    /// <summary>
    /// Represents a CSS rule (selector + declarations)
    /// </summary>
    public class CssRule
    {
        public string Selector { get; set; }
        public List<CssDeclaration> Declarations { get; set; } = new List<CssDeclaration>();
        public int LineNumber { get; set; }
    }

    /// <summary>
    /// Represents a CSS declaration (property: value)
    /// </summary>
    public class CssDeclaration
    {
        public string Property { get; set; }
        public string Value { get; set; }
        public bool IsImportant { get; set; }
        public int LineNumber { get; set; }
    }

    /// <summary>
    /// Result of CSS parsing
    /// </summary>
    public class CssParseResult
    {
        public List<CssRule> Rules { get; set; } = new List<CssRule>();
        public List<string> Errors { get; set; } = new List<string>();
        public List<string> Warnings { get; set; } = new List<string>();
    }

    /// <summary>
    /// CSS parser that extracts rules for conversion to USS
    /// </summary>
    public class CssParser
    {
        private string _css;
        private int _position;
        private int _lineNumber;
        private CssParseResult _result;

        public CssParseResult Parse(string css)
        {
            _css = css ?? string.Empty;
            _position = 0;
            _lineNumber = 1;
            _result = new CssParseResult();

            try
            {
                // Remove comments
                PreprocessCss();

                // Parse rules
                while (_position < _css.Length)
                {
                    SkipWhitespace();
                    if (_position >= _css.Length)
                        break;

                    ParseRule();
                }
            }
            catch (Exception ex)
            {
                _result.Errors.Add($"Parse error at line {_lineNumber}: {ex.Message}");
            }

            return _result;
        }

        private void PreprocessCss()
        {
            // Remove CSS comments
            _css = Regex.Replace(_css, @"/\*.*?\*/", "", RegexOptions.Singleline);
        }

        private void ParseRule()
        {
            var rule = new CssRule
            {
                LineNumber = _lineNumber
            };

            // Parse selector
            rule.Selector = ParseSelector();
            if (string.IsNullOrWhiteSpace(rule.Selector))
            {
                SkipToNextRule();
                return;
            }

            // Validate selector
            ValidateSelector(rule.Selector);

            SkipWhitespace();

            // Expect '{'
            if (Peek() != '{')
            {
                _result.Errors.Add($"Expected '{{' at line {_lineNumber}");
                SkipToNextRule();
                return;
            }
            _position++; // Skip '{'

            // Parse declarations
            ParseDeclarations(rule);

            // Skip '}'
            if (Peek() == '}')
                _position++;

            _result.Rules.Add(rule);
        }

        private string ParseSelector()
        {
            var sb = new StringBuilder();

            while (_position < _css.Length)
            {
                var c = Peek();
                if (c == '{')
                    break;

                if (c == '\n')
                    _lineNumber++;

                sb.Append(c);
                _position++;
            }

            return sb.ToString().Trim();
        }

        private void ParseDeclarations(CssRule rule)
        {
            while (_position < _css.Length)
            {
                SkipWhitespace();

                if (Peek() == '}')
                    break;

                var declaration = ParseDeclaration();
                if (declaration != null)
                {
                    rule.Declarations.Add(declaration);
                }
            }
        }

        private CssDeclaration ParseDeclaration()
        {
            // Parse property name
            var property = ParsePropertyName();
            if (string.IsNullOrWhiteSpace(property))
            {
                SkipToNextDeclaration();
                return null;
            }

            SkipWhitespace();

            // Expect ':'
            if (Peek() != ':')
            {
                _result.Warnings.Add($"Expected ':' after property '{property}' at line {_lineNumber}");
                SkipToNextDeclaration();
                return null;
            }
            _position++; // Skip ':'

            SkipWhitespace();

            // Parse value
            var (value, isImportant) = ParseValue();

            return new CssDeclaration
            {
                Property = property.Trim(),
                Value = value.Trim(),
                IsImportant = isImportant,
                LineNumber = _lineNumber
            };
        }

        private string ParsePropertyName()
        {
            var sb = new StringBuilder();

            while (_position < _css.Length)
            {
                var c = Peek();
                if (c == ':' || c == ';' || c == '}' || c == '{')
                    break;

                if (char.IsWhiteSpace(c))
                    break;

                sb.Append(c);
                _position++;
            }

            return sb.ToString().Trim();
        }

        private (string value, bool isImportant) ParseValue()
        {
            var sb = new StringBuilder();
            var isImportant = false;

            while (_position < _css.Length)
            {
                var c = Peek();

                if (c == ';')
                {
                    _position++; // Skip ';'
                    break;
                }

                if (c == '}')
                    break;

                if (c == '!' && _css.Substring(_position).StartsWith("!important", StringComparison.OrdinalIgnoreCase))
                {
                    isImportant = true;
                    _position += "!important".Length;
                    continue;
                }

                sb.Append(c);
                _position++;
            }

            return (sb.ToString().Trim(), isImportant);
        }

        private void SkipWhitespace()
        {
            while (_position < _css.Length)
            {
                var c = _css[_position];
                if (!char.IsWhiteSpace(c))
                    break;

                if (c == '\n')
                    _lineNumber++;

                _position++;
            }
        }

        private void SkipToNextDeclaration()
        {
            while (_position < _css.Length)
            {
                var c = Peek();
                if (c == ';' || c == '}')
                {
                    if (c == ';')
                        _position++;
                    break;
                }
                _position++;
            }
        }

        private void SkipToNextRule()
        {
            var braceCount = 0;

            while (_position < _css.Length)
            {
                var c = Peek();

                if (c == '{')
                    braceCount++;
                else if (c == '}')
                {
                    braceCount--;
                    if (braceCount <= 0)
                    {
                        _position++;
                        break;
                    }
                }

                _position++;
            }
        }

        private char Peek(int offset = 0)
        {
            var pos = _position + offset;
            return pos < _css.Length ? _css[pos] : '\0';
        }

        private void ValidateSelector(string selector)
        {
            // Check for unsupported pseudo-elements
            if (selector.Contains("::before") || selector.Contains("::after"))
            {
                _result.Warnings.Add($"Selector '{selector}' uses unsupported pseudo-element (::before/::after)");
            }

            // Check for complex selectors
            if (selector.Contains(":nth-child") || selector.Contains(":nth-of-type"))
            {
                _result.Warnings.Add($"Selector '{selector}' uses :nth-child/:nth-of-type which has limited support in USS");
            }

            // Check for deep nested selectors
            var depth = selector.Split(new[] { ' ', '>', '+' }, StringSplitOptions.RemoveEmptyEntries).Length;
            if (depth > 3)
            {
                _result.Warnings.Add($"Selector '{selector}' is deeply nested, consider simplifying");
            }
        }
    }
}