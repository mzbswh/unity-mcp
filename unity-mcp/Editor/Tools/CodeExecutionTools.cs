#if UNITY_MCP_ROSLYN
using System;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using UnityMcp.Shared.Attributes;
using UnityMcp.Shared.Models;
using UnityMcp.Shared.Utils;

namespace UnityMcp.Editor.Tools
{
    [McpToolGroup("CodeExecution")]
    public static class CodeExecutionTools
    {
        [McpTool("code_execute",
            "Compile and execute arbitrary C# code snippet using Roslyn. " +
            "The code should define a static method 'object Run()' which will be invoked. " +
            "Has access to UnityEngine and UnityEditor namespaces. " +
            "Dangerous APIs (File IO, Network, Process) are blocked.",
            Group = "advanced", AutoRegister = false)]
        public static ToolResult Execute(
            [Desc("C# code snippet. Must contain a static method: object Run()")] string code,
            [Desc("Execution timeout in seconds (default: 10, max: 60)")] int timeoutSeconds = 10)
        {
            if (string.IsNullOrWhiteSpace(code))
                return ToolResult.Error("Code cannot be empty");

            if (timeoutSeconds > 60) timeoutSeconds = 60;
            if (timeoutSeconds < 1) timeoutSeconds = 1;

            // 1. Security check
            var securityResult = SecurityChecker.Validate(code);
            if (!securityResult.IsValid)
                return ToolResult.Error($"Security violation: {securityResult.Reason}");

            // 2. Compile
            var compilation = CreateCompilation(code);
            using var ms = new MemoryStream();
            var emitResult = compilation.Emit(ms);

            if (!emitResult.Success)
            {
                var errors = emitResult.Diagnostics
                    .Where(d => d.Severity == DiagnosticSeverity.Error)
                    .Select(d => new
                    {
                        id = d.Id,
                        message = d.GetMessage(),
                        line = d.Location.GetLineSpan().StartLinePosition.Line + 1,
                        column = d.Location.GetLineSpan().StartLinePosition.Character + 1
                    });

                return ToolResult.Json(new
                {
                    success = false,
                    phase = "compilation",
                    errors,
                    warnings = emitResult.Diagnostics
                        .Where(d => d.Severity == DiagnosticSeverity.Warning)
                        .Select(d => d.GetMessage())
                });
            }

            // 3. Load and execute
            ms.Seek(0, SeekOrigin.Begin);
            var assembly = Assembly.Load(ms.ToArray());
            var entryType = assembly.GetTypes()
                .FirstOrDefault(t => t.GetMethod("Run",
                    BindingFlags.Public | BindingFlags.Static) != null);

            if (entryType == null)
                return ToolResult.Error(
                    "Code must contain a public static method: object Run()");

            var method = entryType.GetMethod("Run", BindingFlags.Public | BindingFlags.Static);

            // 4. Capture Console output
            var consoleOutput = new StringWriter();
            var originalOut = Console.Out;
            Console.SetOut(consoleOutput);

            var sw = System.Diagnostics.Stopwatch.StartNew();
            try
            {
                var cts = new CancellationTokenSource(TimeSpan.FromSeconds(timeoutSeconds));
                var task = Task.Run(() => method.Invoke(null, null), cts.Token);

                if (!task.Wait(TimeSpan.FromSeconds(timeoutSeconds)))
                    return ToolResult.Error($"Execution timed out after {timeoutSeconds}s");

                sw.Stop();
                var result = task.Result;

                return ToolResult.Json(new
                {
                    success = true,
                    returnValue = result,
                    returnType = result?.GetType().Name ?? "null",
                    consoleOutput = consoleOutput.ToString(),
                    executionTimeMs = (int)sw.ElapsedMilliseconds
                });
            }
            catch (AggregateException ex) when (ex.InnerException is TargetInvocationException tie)
            {
                sw.Stop();
                return ToolResult.Json(new
                {
                    success = false,
                    phase = "execution",
                    error = tie.InnerException?.Message ?? tie.Message,
                    stackTrace = tie.InnerException?.StackTrace,
                    consoleOutput = consoleOutput.ToString()
                });
            }
            catch (TargetInvocationException ex)
            {
                sw.Stop();
                return ToolResult.Json(new
                {
                    success = false,
                    phase = "execution",
                    error = ex.InnerException?.Message ?? ex.Message,
                    stackTrace = ex.InnerException?.StackTrace,
                    consoleOutput = consoleOutput.ToString()
                });
            }
            catch (OperationCanceledException)
            {
                return ToolResult.Error($"Execution timed out after {timeoutSeconds}s");
            }
            finally
            {
                Console.SetOut(originalOut);
            }
        }

        [McpTool("code_validate",
            "Compile C# code without executing - returns diagnostics only. " +
            "Useful for checking if generated code compiles before writing to file.",
            ReadOnly = true, Idempotent = true, Group = "advanced")]
        public static ToolResult Validate(
            [Desc("C# code snippet to validate")] string code)
        {
            if (string.IsNullOrWhiteSpace(code))
                return ToolResult.Error("Code cannot be empty");

            var compilation = CreateCompilation(code);
            var diagnostics = compilation.GetDiagnostics();

            return ToolResult.Json(new
            {
                isValid = !diagnostics.Any(d => d.Severity == DiagnosticSeverity.Error),
                errors = diagnostics
                    .Where(d => d.Severity == DiagnosticSeverity.Error)
                    .Select(d => new
                    {
                        id = d.Id,
                        message = d.GetMessage(),
                        line = d.Location.GetLineSpan().StartLinePosition.Line + 1
                    }),
                warnings = diagnostics
                    .Where(d => d.Severity == DiagnosticSeverity.Warning)
                    .Select(d => new
                    {
                        id = d.Id,
                        message = d.GetMessage(),
                        line = d.Location.GetLineSpan().StartLinePosition.Line + 1
                    })
            });
        }

        private static CSharpCompilation CreateCompilation(string code)
        {
            var syntaxTree = CSharpSyntaxTree.ParseText(code);

            var references = AppDomain.CurrentDomain.GetAssemblies()
                .Where(a => !a.IsDynamic && !string.IsNullOrEmpty(a.Location))
                .Where(a =>
                {
                    var name = a.GetName().Name;
                    return name.StartsWith("UnityEngine") || name.StartsWith("UnityEditor")
                        || name == "mscorlib" || name == "System" || name == "System.Core"
                        || name == "netstandard" || name.StartsWith("System.");
                })
                .Select(a => MetadataReference.CreateFromFile(a.Location))
                .Cast<MetadataReference>()
                .ToList();

            return CSharpCompilation.Create(
                $"McpDynamic_{Guid.NewGuid():N}",
                new[] { syntaxTree },
                references,
                new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary));
        }
    }
}
#endif
