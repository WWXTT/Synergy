using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Text.RegularExpressions;
using UIConverter.Parsers;
using UIConverter.Transformers;
using UIConverter.Validators;
using UnityEditor;
using UnityEngine;

namespace UIConverter
{
    /// <summary>
    /// Main conversion orchestrator
    /// </summary>
    public static class UIConverterCore
    {
        /// <summary>
        /// Default Unity Runtime UI styles
        /// </summary>
        private const string DEFAULT_USS = @"
/* Default Unity Runtime UI styles */
.unity-button {
    background-color: #4a90d9;
    color: #ffffff;
    border-radius: 4px;
    padding: 8px 16px;
    height: 40px;
}

.unity-label {
    color: #ffffff;
}

.unity-toggle { }

.unity-text-field {
    background-color: #2a2a3e;
    color: #ffffff;
    border-width: 1px;
    border-color: #4a4a6a;
    border-radius: 4px;
    padding: 8px;
}

/* Layout helper classes */
.row { flex-direction: row; }
.column { flex-direction: column; }
.center { justify-content: center; align-items: center; }
.expand { flex-grow: 1; }
";

        public static ConversionResult Convert(string htmlContent)
        {
            var result = new ConversionResult();

            try
            {
                // Step 1: Parse HTML (extracts embedded CSS automatically)
                var htmlParser = new HtmlParser();
                var htmlResult = htmlParser.Parse(htmlContent);
                result.HtmlErrors.AddRange(htmlResult.Errors);
                result.HtmlWarnings.AddRange(htmlResult.Warnings);

                // Step 2: Parse CSS (use extracted CSS from HTML)
                var cssContent = htmlResult.ExtractedCss ?? string.Empty;
                var cssParser = new CssParser();
                var cssResult = cssParser.Parse(cssContent);
                result.CssErrors.AddRange(cssResult.Errors);
                result.CssWarnings.AddRange(cssResult.Warnings);

                // Step 3: Validate
                var validator = new FeatureValidator();
                var htmlValidation = validator.ValidateHtml(htmlResult.Root);
                var cssValidation = validator.ValidateCss(cssResult);

                foreach (var issue in htmlValidation.Issues)
                {
                    result.ValidationIssues.Add($"[HTML] {issue.Message}" +
                        (string.IsNullOrEmpty(issue.Alternative) ? "" : $" Alternative: {issue.Alternative}"));
                }

                foreach (var issue in cssValidation.Issues)
                {
                    result.ValidationIssues.Add($"[CSS] {issue.Message}" +
                        (string.IsNullOrEmpty(issue.Alternative) ? "" : $" Alternative: {issue.Alternative}"));
                }

                // Step 4: Generate USS (default styles + converted)
                var ussGenerator = new UssGenerator();
                var ussResult = ussGenerator.Generate(cssResult);

                // Combine default styles with converted styles
                var convertedUss = ussResult.UssContent ?? string.Empty;
                result.UssContent = DEFAULT_USS + convertedUss;

                foreach (var warning in ussResult.Warnings)
                {
                    result.Warnings.Add($"[CSS→USS] {warning.Message}");
                }

                // Step 5: Generate UXML (structure only, styles in separate USS file)
                var uxmlGenerator = new UxmlGenerator();
                var uxmlResult = uxmlGenerator.Generate(htmlResult.Root);
                result.UxmlContent = uxmlResult.UxmlContent;
                result.Warnings.AddRange(uxmlResult.Warnings);

                result.Success = true;
            }
            catch (Exception ex)
            {
                result.Success = false;
                result.Errors.Add($"Conversion failed: {ex.Message}");
            }

            return result;
        }

        /// <summary>
        /// Adds a Style reference to UXML content
        /// </summary>
        public static string AddStyleReference(string uxmlContent, string ussAssetPath)
        {
            if (string.IsNullOrEmpty(uxmlContent) || string.IsNullOrEmpty(ussAssetPath))
                return uxmlContent;

            // Check if style reference already exists
            if (uxmlContent.Contains($"src=\"{ussAssetPath}\""))
                return uxmlContent;

            // Find the position after <ui:UXML ...> opening tag
            var match = Regex.Match(uxmlContent, @"<ui:UXML[^>]*>");
            if (match.Success)
            {
                var insertPosition = match.Index + match.Value.Length;
                var styleReference = $"\n    <Style src=\"{ussAssetPath}\" />";
                return uxmlContent.Insert(insertPosition, styleReference);
            }

            return uxmlContent;
        }
    }

    public class ConversionResult
    {
        public bool Success { get; set; }
        public string UxmlContent { get; set; }
        public string UssContent { get; set; }
        public List<string> Errors { get; set; } = new List<string>();
        public List<string> Warnings { get; set; } = new List<string>();
        public List<string> HtmlErrors { get; set; } = new List<string>();
        public List<string> HtmlWarnings { get; set; } = new List<string>();
        public List<string> CssErrors { get; set; } = new List<string>();
        public List<string> CssWarnings { get; set; } = new List<string>();
        public List<string> ValidationIssues { get; set; } = new List<string>();
    }

    /// <summary>
    /// Unity Editor Window for the UI Converter (using IMGUI to avoid TextField style tag issues)
    /// </summary>
    public class UIConverterWindow : EditorWindow
    {
        private const string EditorPrefsUxmlPathKey = "UIConverter_UxmlPath";
        private const string EditorPrefsUssPathKey = "UIConverter_UssPath";

        [MenuItem("Tools/UI Converter")]
        public static void ShowWindow()
        {
            var window = GetWindow<UIConverterWindow>("UI Converter");
            window.minSize = new Vector2(900, 600);
        }

        // State
        private string _htmlInput = "";
        private string _uxmlOutput = "";
        private string _ussOutput = "";
        private Vector2 _htmlScrollPos;
        private Vector2 _uxmlScrollPos;
        private Vector2 _ussScrollPos;
        private Vector2 _warningsScrollPos;
        private ConversionResult _lastResult;
        private string _statusText = "Ready";
        private string _uxmlOutputPath;
        private string _ussOutputPath;

        private string UxmlOutputPath
        {
            get
            {
                if (string.IsNullOrEmpty(_uxmlOutputPath))
                {
                    _uxmlOutputPath = EditorPrefs.GetString(EditorPrefsUxmlPathKey, Application.dataPath);
                }
                // Convert relative path to absolute if needed
                _uxmlOutputPath = EnsureAbsolutePath(_uxmlOutputPath);
                return _uxmlOutputPath;
            }
            set
            {
                _uxmlOutputPath = value;
                EditorPrefs.SetString(EditorPrefsUxmlPathKey, value);
            }
        }

        private string UssOutputPath
        {
            get
            {
                if (string.IsNullOrEmpty(_ussOutputPath))
                {
                    _ussOutputPath = EditorPrefs.GetString(EditorPrefsUssPathKey, Application.dataPath);
                }
                // Convert relative path to absolute if needed
                _ussOutputPath = EnsureAbsolutePath(_ussOutputPath);
                return _ussOutputPath;
            }
            set
            {
                _ussOutputPath = value;
                EditorPrefs.SetString(EditorPrefsUssPathKey, value);
            }
        }

        private string EnsureAbsolutePath(string path)
        {
            if (string.IsNullOrEmpty(path)) return Application.dataPath;

            // Already absolute path (Windows: C:\..., Unix: /...)
            if (path.Contains(":") || path.StartsWith("/"))
                return path;

            // Relative path starting with Assets/
            if (path.StartsWith("Assets/", StringComparison.OrdinalIgnoreCase))
            {
                string projectPath = Path.GetDirectoryName(Application.dataPath);
                return Path.Combine(projectPath, path);
            }

            // Other relative paths
            return Path.GetFullPath(Path.Combine(Application.dataPath, "..", path));
        }

        // Layout constants
        private const float ButtonHeight = 30f;
        private const float ConvertButtonHeight = 40f;

        // Custom style to avoid <style> tag processing issues
        private GUIStyle _textAreaStyle;
        private GUIStyle _readOnlyTextAreaStyle;

        private GUIStyle TextAreaStyle
        {
            get
            {
                if (_textAreaStyle == null)
                {
                    _textAreaStyle = new GUIStyle(EditorStyles.textArea)
                    {
                        richText = false,
                        wordWrap = true
                    };
                }
                return _textAreaStyle;
            }
        }

        private GUIStyle ReadOnlyTextAreaStyle
        {
            get
            {
                if (_readOnlyTextAreaStyle == null)
                {
                    _readOnlyTextAreaStyle = new GUIStyle(EditorStyles.textArea)
                    {
                        richText = false,
                        wordWrap = true
                    };
                    _readOnlyTextAreaStyle.normal.textColor = Color.gray;
                }
                return _readOnlyTextAreaStyle;
            }
        }

        private void OnGUI()
        {
            // Title
            EditorGUILayout.Space(10);
            EditorGUI.LabelField(GUILayoutUtility.GetRect(0, 25), "HTML/CSS to UXML/USS Converter",
                EditorStyles.boldLabel);
            EditorGUILayout.Space(5);

            // Main content area
            EditorGUILayout.BeginHorizontal();

            // Left column (Input)
            DrawInputColumn();

            GUILayout.Space(10);

            // Right column (Output)
            DrawOutputColumn();

            EditorGUILayout.EndHorizontal();

            // Warnings
            EditorGUILayout.Space(10);
            DrawWarnings();

            // Status bar
            EditorGUILayout.Space(5);
            EditorGUILayout.LabelField(_statusText, EditorStyles.helpBox);
        }

        private void DrawInputColumn()
        {
            EditorGUILayout.BeginVertical(GUILayout.Width(position.width * 0.48f));

            // HTML Input Label
            EditorGUILayout.LabelField("HTML Input (CSS in <style> tags will be auto-extracted):", EditorStyles.boldLabel);

            // Button row - Load HTML
            if (GUILayout.Button("Load HTML File", GUILayout.Height(ButtonHeight)))
            {
                LoadFile();
            }
            if (GUILayout.Button("Reset Paths", GUILayout.Height(ButtonHeight)))
            {
                ClearSavedPaths();
            }

            // Output paths settings
            EditorGUILayout.Space(5);
            EditorGUILayout.LabelField("Output Settings:", EditorStyles.boldLabel);

            // UXML path
            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.LabelField("UXML:", GUILayout.Width(50));
            if (GUILayout.Button("...", GUILayout.Width(30)))
            {
                SetUxmlPath();
            }
            EditorGUILayout.LabelField(GetDisplayPath(UxmlOutputPath), EditorStyles.miniLabel);
            EditorGUILayout.EndHorizontal();

            // USS path
            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.LabelField("USS:", GUILayout.Width(50));
            if (GUILayout.Button("...", GUILayout.Width(30)))
            {
                SetUssPath();
            }
            EditorGUILayout.LabelField(GetDisplayPath(UssOutputPath), EditorStyles.miniLabel);
            EditorGUILayout.EndHorizontal();

            EditorGUILayout.Space(5);

            // HTML Text Area
            _htmlScrollPos = EditorGUILayout.BeginScrollView(_htmlScrollPos, GUILayout.ExpandHeight(true));
            _htmlInput = EditorGUILayout.TextArea(_htmlInput, TextAreaStyle, GUILayout.ExpandHeight(true));
            EditorGUILayout.EndScrollView();

            EditorGUILayout.Space(5);

            // Convert button
            GUI.backgroundColor = new Color(0.2f, 0.4f, 0.8f);
            if (GUILayout.Button("Convert", GUILayout.Height(ConvertButtonHeight)))
            {
                Convert();
            }
            GUI.backgroundColor = Color.white;

            EditorGUILayout.EndVertical();
        }

        private void DrawOutputColumn()
        {
            EditorGUILayout.BeginVertical(GUILayout.Width(position.width * 0.48f));

            // UXML Output
            EditorGUILayout.LabelField("UXML Output:", EditorStyles.boldLabel);

            EditorGUILayout.BeginHorizontal();
            if (GUILayout.Button("Copy UXML", GUILayout.Height(ButtonHeight)))
            {
                CopyToClipboard(_uxmlOutput);
            }
            EditorGUILayout.EndHorizontal();

            _uxmlScrollPos = EditorGUILayout.BeginScrollView(_uxmlScrollPos, GUILayout.Height(200));
            EditorGUI.BeginDisabledGroup(true);
            EditorGUILayout.TextArea(_uxmlOutput, ReadOnlyTextAreaStyle, GUILayout.ExpandHeight(true));
            EditorGUI.EndDisabledGroup();
            EditorGUILayout.EndScrollView();

            EditorGUILayout.Space(5);

            // USS Output
            EditorGUILayout.LabelField("USS Output:", EditorStyles.boldLabel);

            EditorGUILayout.BeginHorizontal();
            if (GUILayout.Button("Copy USS", GUILayout.Height(ButtonHeight)))
            {
                CopyToClipboard(_ussOutput);
            }
            EditorGUILayout.EndHorizontal();

            _ussScrollPos = EditorGUILayout.BeginScrollView(_ussScrollPos, GUILayout.Height(200));
            EditorGUI.BeginDisabledGroup(true);
            EditorGUILayout.TextArea(_ussOutput, ReadOnlyTextAreaStyle, GUILayout.ExpandHeight(true));
            EditorGUI.EndDisabledGroup();
            EditorGUILayout.EndScrollView();

            EditorGUILayout.Space(5);

            // Save and Link Styles buttons
            EditorGUILayout.BeginHorizontal();
            if (GUILayout.Button("Save to Project", GUILayout.Height(ButtonHeight)))
            {
                SaveFiles();
            }
            GUILayout.Space(5);
            GUI.backgroundColor = new Color(0.3f, 0.6f, 0.3f);
            if (GUILayout.Button("Link Styles", GUILayout.Height(ButtonHeight)))
            {
                LinkStyles();
            }
            GUI.backgroundColor = Color.white;
            EditorGUILayout.EndHorizontal();

            EditorGUILayout.EndVertical();
        }

        private void DrawWarnings()
        {
            if (_lastResult == null) return;

            var allIssues = new List<string>();
            allIssues.AddRange(_lastResult.HtmlErrors);
            allIssues.AddRange(_lastResult.HtmlWarnings);
            allIssues.AddRange(_lastResult.CssErrors);
            allIssues.AddRange(_lastResult.CssWarnings);
            allIssues.AddRange(_lastResult.ValidationIssues);
            allIssues.AddRange(_lastResult.Warnings);
            allIssues.AddRange(_lastResult.Errors);

            if (allIssues.Count == 0) return;

            EditorGUILayout.LabelField($"Issues ({allIssues.Count}):", EditorStyles.boldLabel);

            _warningsScrollPos = EditorGUILayout.BeginScrollView(_warningsScrollPos, GUILayout.Height(100));
            EditorGUILayout.BeginVertical(EditorStyles.helpBox);
            foreach (var issue in allIssues)
            {
                EditorGUILayout.LabelField($"• {issue}", EditorStyles.wordWrappedLabel);
            }
            EditorGUILayout.EndVertical();
            EditorGUILayout.EndScrollView();
        }

        private void Convert()
        {
            if (string.IsNullOrWhiteSpace(_htmlInput))
            {
                _statusText = "Please enter HTML content";
                return;
            }

            _statusText = "Converting...";

            try
            {
                _lastResult = UIConverterCore.Convert(_htmlInput);

                if (_lastResult.Success)
                {
                    _uxmlOutput = _lastResult.UxmlContent ?? "";
                    _ussOutput = _lastResult.UssContent ?? "";
                    _statusText = "Conversion complete!";
                }
                else
                {
                    _statusText = "Conversion failed with errors";
                }
            }
            catch (Exception ex)
            {
                _statusText = $"Error: {ex.Message}";
                Debug.LogError($"UI Converter error: {ex}");
            }

            Repaint();
        }

        private void LoadFile()
        {
            var path = EditorUtility.OpenFilePanel("Load HTML File", "", "html");
            if (!string.IsNullOrEmpty(path))
            {
                try
                {
                    _htmlInput = File.ReadAllText(path);
                    _statusText = $"Loaded {Path.GetFileName(path)}";
                }
                catch (Exception ex)
                {
                    _statusText = $"Error loading file: {ex.Message}";
                }
            }
        }

        private void SetUxmlPath()
        {
            var path = EditorUtility.OpenFolderPanel("Select UXML Output Folder", UxmlOutputPath, "");
            if (!string.IsNullOrEmpty(path))
            {
                UxmlOutputPath = path;
                _statusText = $"UXML path set";
            }
        }

        private void SetUssPath()
        {
            var path = EditorUtility.OpenFolderPanel("Select USS Output Folder", UssOutputPath, "");
            if (!string.IsNullOrEmpty(path))
            {
                UssOutputPath = path;
                _statusText = $"USS path set";
            }
        }

        private void ClearSavedPaths()
        {
            _uxmlOutputPath = null;
            _ussOutputPath = null;
            EditorPrefs.DeleteKey(EditorPrefsUxmlPathKey);
            EditorPrefs.DeleteKey(EditorPrefsUssPathKey);
            _statusText = "Paths cleared. Please reselect output folders.";
        }

        private string GetDisplayPath(string fullPath)
        {
            if (string.IsNullOrEmpty(fullPath)) return "";

            // Show relative path if within project
            if (fullPath.StartsWith(Application.dataPath))
            {
                return "Assets" + fullPath.Substring(Application.dataPath.Length);
            }
            // Otherwise show truncated path
            if (fullPath.Length > 40)
            {
                return "..." + fullPath.Substring(fullPath.Length - 37);
            }
            return fullPath;
        }

        private string GetRelativeAssetPath(string absolutePath)
        {
            if (string.IsNullOrEmpty(absolutePath)) return "";

            // Normalize path separators
            absolutePath = absolutePath.Replace("\\", "/");
            string dataPath = Application.dataPath.Replace("\\", "/");

            // If path is within the project's Assets folder
            if (absolutePath.StartsWith(dataPath, StringComparison.OrdinalIgnoreCase))
            {
                string relative = absolutePath.Substring(dataPath.Length);
                if (relative.StartsWith("/"))
                    relative = relative.Substring(1);
                return "Assets/" + relative;
            }

            // Check if path contains /Assets/ anywhere (from absolute path)
            int assetsIndex = absolutePath.IndexOf("/Assets/", StringComparison.OrdinalIgnoreCase);
            if (assetsIndex >= 0)
            {
                return absolutePath.Substring(assetsIndex + 1); // +1 to skip the leading /
            }

            // Already a relative path starting with Assets/
            if (absolutePath.StartsWith("Assets/", StringComparison.OrdinalIgnoreCase))
            {
                return absolutePath;
            }

            // Fallback - return just the filename
            return Path.GetFileName(absolutePath);
        }

        private void SaveFiles()
        {
            if (string.IsNullOrWhiteSpace(_uxmlOutput) && string.IsNullOrWhiteSpace(_ussOutput))
            {
                _statusText = "No output to save";
                return;
            }

            // Ask for file name once
            string fileName = "ui";
            var tempPath = EditorUtility.SaveFilePanel("Save Files (Enter Name Only)", UxmlOutputPath, fileName, "uxml");
            if (string.IsNullOrEmpty(tempPath))
            {
                _statusText = "Save cancelled";
                return;
            }

            // Extract filename from path
            fileName = Path.GetFileNameWithoutExtension(tempPath);

            int savedCount = 0;

            // Save UXML
            if (!string.IsNullOrWhiteSpace(_uxmlOutput))
            {
                var uxmlPath = Path.Combine(UxmlOutputPath, fileName + ".uxml");
                try
                {
                    File.WriteAllText(uxmlPath, _uxmlOutput);
                    savedCount++;
                }
                catch (Exception ex)
                {
                    _statusText = $"Error saving UXML: {ex.Message}";
                    return;
                }
            }

            // Save USS
            if (!string.IsNullOrWhiteSpace(_ussOutput))
            {
                var ussPath = Path.Combine(UssOutputPath, fileName + ".uss");
                try
                {
                    File.WriteAllText(ussPath, _ussOutput);
                    savedCount++;
                }
                catch (Exception ex)
                {
                    _statusText = $"Error saving USS: {ex.Message}";
                    return;
                }
            }

            AssetDatabase.Refresh();
            _statusText = $"Saved {savedCount} file(s) as '{fileName}'";
        }

        private void LinkStyles()
        {
            if (!Directory.Exists(UxmlOutputPath) || !Directory.Exists(UssOutputPath))
            {
                _statusText = "Output folders not found. Please set paths first.";
                return;
            }

            // Get all UXML files
            var uxmlFiles = Directory.GetFiles(UxmlOutputPath, "*.uxml");
            if (uxmlFiles.Length == 0)
            {
                _statusText = "No UXML files found in output folder";
                return;
            }

            // Refresh asset database to ensure GUIDs are up to date
            AssetDatabase.Refresh();

            int linkedCount = 0;

            foreach (var uxmlPath in uxmlFiles)
            {
                string fileName = Path.GetFileNameWithoutExtension(uxmlPath);
                string ussPath = Path.Combine(UssOutputPath, fileName + ".uss");

                // Check if matching USS file exists
                if (!File.Exists(ussPath))
                    continue;

                try
                {
                    // Read UXML content
                    string uxmlContent = File.ReadAllText(uxmlPath);

                    // Get the appropriate path reference for the style
                    string ussRefPath = GetStyleReferencePath(ussPath, uxmlPath);

                    if (string.IsNullOrEmpty(ussRefPath))
                    {
                        Debug.LogWarning($"Could not get path for USS file: {ussPath}");
                        continue;
                    }

                    // Add style reference
                    string newContent = UIConverterCore.AddStyleReference(uxmlContent, ussRefPath);

                    // Save if changed
                    if (newContent != uxmlContent)
                    {
                        File.WriteAllText(uxmlPath, newContent);
                        linkedCount++;
                    }
                }
                catch (Exception ex)
                {
                    Debug.LogWarning($"Failed to link {fileName}: {ex.Message}");
                }
            }

            AssetDatabase.Refresh();

            if (linkedCount > 0)
            {
                _statusText = $"Linked {linkedCount} UXML file(s) to matching USS";
            }
            else
            {
                _statusText = "No new links created (may already be linked or no matching files)";
            }
        }

        private string GetStyleReferencePath(string ussAbsolutePath, string uxmlAbsolutePath)
        {
            if (string.IsNullOrEmpty(ussAbsolutePath)) return "";

            // Get the directory paths
            string ussDir = Path.GetDirectoryName(ussAbsolutePath);
            string uxmlDir = Path.GetDirectoryName(uxmlAbsolutePath);

            // Get the USS filename
            string ussFileName = Path.GetFileName(ussAbsolutePath);

            // If both files are in the same directory, use relative path
            if (string.Equals(ussDir, uxmlDir, StringComparison.OrdinalIgnoreCase))
            {
                return ussFileName;
            }

            // Otherwise, use project-relative path from Assets folder
            string assetPath = GetRelativeAssetPath(ussAbsolutePath);
            if (string.IsNullOrEmpty(assetPath))
            {
                // Fallback to just filename if we can't get the asset path
                return ussFileName;
            }

            // Return project-relative path (starts with /Assets/...)
            return "/" + assetPath;
        }

        private void CopyToClipboard(string content)
        {
            EditorGUIUtility.systemCopyBuffer = content;
            _statusText = "Copied to clipboard!";
        }
    }
}
