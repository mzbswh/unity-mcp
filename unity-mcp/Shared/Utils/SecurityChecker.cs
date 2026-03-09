using System.Text.RegularExpressions;

namespace UnityMcp.Shared.Utils
{
    public static class SecurityChecker
    {
        private static readonly string[] ForbiddenPatterns = new[]
        {
            "System.IO.File",
            "System.IO.Directory",
            "System.IO.StreamWriter",
            "System.IO.StreamReader",
            "System.IO.FileStream",
            "System.IO.FileInfo",
            "System.IO.DirectoryInfo",
            "System.IO.BinaryWriter",
            "System.IO.BinaryReader",
            "System.Net",
            "System.Diagnostics.Process",
            "System.Environment.Exit",
            "System.Reflection.Emit",
            "System.Runtime.InteropServices.DllImport",
            "System.Runtime.InteropServices.Marshal",
            "System.CodeDom",
            "AppDomain",
            "UnityEditor.AssetDatabase.DeleteAsset",
            "UnityEditor.FileUtil.DeleteFileOrDirectory",
            "UnityEditor.AssetDatabase.ImportPackage",
            "UnityEditor.EditorApplication.ExecuteMenuItem",
            "Application.Quit",
        };

        private static readonly string[] ForbiddenUsings = new[]
        {
            "System.Net",
            "System.Net.Http",
            "System.Net.Sockets",
            "System.Diagnostics",
        };

        // Reflection/dynamic access patterns that can bypass string-based checks
        private static readonly string[] ForbiddenReflectionPatterns = new[]
        {
            "Type.GetType",
            "Assembly.Load",
            "Assembly.LoadFrom",
            "Assembly.LoadFile",
            "Activator.CreateInstance",
            "GetMethod",
            "GetField",
            "GetProperty",
            "MethodInfo.Invoke",
            "FieldInfo.SetValue",
            "FieldInfo.GetValue",
            "DynamicInvoke",
            "Expression.Lambda",
            "Expression.Compile",
            "Delegate.CreateDelegate",
            "CSharpCodeProvider",
            "CompileAssemblyFrom",
        };

        // Regex patterns for more sophisticated bypass attempts
        private static readonly Regex TypeofReflectionRegex = new Regex(
            @"typeof\s*\(\s*(System\.IO|System\.Net|System\.Diagnostics\.Process|System\.Reflection\.Emit)",
            RegexOptions.Compiled);

        private static readonly Regex StringConcatBypassRegex = new Regex(
            @"(""[^""]*""\s*\+\s*){1,}""[^""]*""\s*.*\b(GetType|GetMethod|LoadFrom|Invoke)\b",
            RegexOptions.Compiled);

        // Detect using alias that may reference forbidden namespaces
        private static readonly Regex UsingAliasRegex = new Regex(
            @"using\s+\w+\s*=\s*(System\.IO|System\.Net|System\.Diagnostics|System\.Reflection\.Emit|System\.Runtime\.InteropServices)",
            RegexOptions.Compiled);

        public static (bool IsValid, string Reason) Validate(string code)
        {
            if (string.IsNullOrWhiteSpace(code))
                return (false, "Code cannot be empty");

            // Strip single-line and multi-line comments to prevent bypass via
            // embedding forbidden patterns in comment-interrupted tokens
            // e.g. System.IO./**/File or System.IO.//\nFile
            var stripped = Regex.Replace(code, @"//[^\n]*", " ");
            stripped = Regex.Replace(stripped, @"/\*[\s\S]*?\*/", " ");

            // Check forbidden API patterns (against stripped code)
            foreach (var pattern in ForbiddenPatterns)
            {
                if (stripped.Contains(pattern))
                    return (false, $"Forbidden API: {pattern}");
            }

            // Check forbidden using directives
            foreach (var ns in ForbiddenUsings)
            {
                if (Regex.IsMatch(stripped, $@"using\s+{Regex.Escape(ns)}"))
                    return (false, $"Forbidden namespace: {ns}");
            }

            // Check reflection/dynamic access patterns
            foreach (var pattern in ForbiddenReflectionPatterns)
            {
                if (stripped.Contains(pattern))
                    return (false, $"Forbidden reflection API: {pattern}");
            }

            // Check typeof() used to access forbidden namespaces
            if (TypeofReflectionRegex.IsMatch(stripped))
                return (false, "Forbidden: typeof() access to restricted namespace");

            // Check string concatenation used with reflection APIs
            if (StringConcatBypassRegex.IsMatch(stripped))
                return (false, "Forbidden: potential string concatenation bypass with reflection");

            // Check using alias bypass
            if (UsingAliasRegex.IsMatch(stripped))
                return (false, "Forbidden: using alias for restricted namespace");

            return (true, null);
        }
    }
}
