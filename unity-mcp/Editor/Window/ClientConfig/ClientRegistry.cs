using System;
using System.Collections.Generic;
using System.IO;

namespace UnityMcp.Editor.Window.ClientConfig
{
    public static class ClientRegistry
    {
        private static readonly Dictionary<ConfigStrategy, IConfigWriter> s_writers = new()
        {
            { ConfigStrategy.JsonFile, new JsonFileConfigWriter() },
            { ConfigStrategy.CliCommand, new ClaudeCliConfigWriter() },
        };

        public static IConfigWriter GetWriter(ConfigStrategy strategy) => s_writers[strategy];

        public static readonly ClientProfile[] All = BuildProfiles();

        private static ClientProfile[] BuildProfiles()
        {
            string home = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
            return new[]
            {
                new ClientProfile
                {
                    Id = "claude-code", DisplayName = "Claude Code",
                    Strategy = ConfigStrategy.CliCommand,
                    Paths = new PlatformPaths
                    {
                        Windows = Path.Combine(home, ".claude.json"),
                        Mac = Path.Combine(home, ".claude.json"),
                        Linux = Path.Combine(home, ".claude.json"),
                    },
                    InstallSteps = new[] { "Install Claude CLI", "Click Configure to register via project config" },
                },
                new ClientProfile
                {
                    Id = "cursor", DisplayName = "Cursor",
                    Strategy = ConfigStrategy.JsonFile, IsProjectLevel = true,
                    Paths = new PlatformPaths
                    {
                        Windows = Path.Combine(".cursor", "mcp.json"),
                        Mac = Path.Combine(".cursor", "mcp.json"),
                        Linux = Path.Combine(".cursor", "mcp.json"),
                    },
                    InstallSteps = new[] { "Click Configure to write .cursor/mcp.json" },
                },
                new ClientProfile
                {
                    Id = "vscode", DisplayName = "VS Code / Copilot",
                    Strategy = ConfigStrategy.JsonFile, IsProjectLevel = true,
                    Paths = new PlatformPaths
                    {
                        Windows = Path.Combine(".vscode", "mcp.json"),
                        Mac = Path.Combine(".vscode", "mcp.json"),
                        Linux = Path.Combine(".vscode", "mcp.json"),
                    },
                    InstallSteps = new[] { "Click Configure to write .vscode/mcp.json" },
                },
                new ClientProfile
                {
                    Id = "windsurf", DisplayName = "Windsurf",
                    Strategy = ConfigStrategy.JsonFile,
                    Paths = new PlatformPaths
                    {
                        Windows = Path.Combine(home, ".codeium", "windsurf", "mcp_config.json"),
                        Mac = Path.Combine(home, ".codeium", "windsurf", "mcp_config.json"),
                        Linux = Path.Combine(home, ".codeium", "windsurf", "mcp_config.json"),
                    },
                    InstallSteps = new[] { "Click Configure to write mcp_config.json" },
                },
                new ClientProfile
                {
                    Id = "cline", DisplayName = "Cline",
                    Strategy = ConfigStrategy.JsonFile,
                    Paths = new PlatformPaths
                    {
                        Windows = Path.Combine(home, "AppData", "Roaming", "Code", "User", "globalStorage", "saoudrizwan.claude-dev", "settings", "cline_mcp_settings.json"),
                        Mac = Path.Combine(home, "Library", "Application Support", "Code", "User", "globalStorage", "saoudrizwan.claude-dev", "settings", "cline_mcp_settings.json"),
                        Linux = Path.Combine(home, ".config", "Code", "User", "globalStorage", "saoudrizwan.claude-dev", "settings", "cline_mcp_settings.json"),
                    },
                    InstallSteps = new[] { "Click Configure to write Cline MCP settings" },
                },
                new ClientProfile
                {
                    Id = "roo-code", DisplayName = "Roo Code",
                    Strategy = ConfigStrategy.JsonFile,
                    Paths = new PlatformPaths
                    {
                        Windows = Path.Combine(home, "AppData", "Roaming", "Code", "User", "globalStorage", "rooveterinaryinc.roo-cline", "settings", "mcp_settings.json"),
                        Mac = Path.Combine(home, "Library", "Application Support", "Code", "User", "globalStorage", "rooveterinaryinc.roo-cline", "settings", "mcp_settings.json"),
                        Linux = Path.Combine(home, ".config", "Code", "User", "globalStorage", "rooveterinaryinc.roo-cline", "settings", "mcp_settings.json"),
                    },
                    InstallSteps = new[] { "Click Configure to write Roo Code MCP settings" },
                },
                new ClientProfile
                {
                    Id = "cherry-studio", DisplayName = "Cherry Studio",
                    Strategy = ConfigStrategy.JsonFile,
                    Paths = new PlatformPaths
                    {
                        Windows = Path.Combine(home, "AppData", "Roaming", "CherryStudio", "mcp.json"),
                        Mac = Path.Combine(home, "Library", "Application Support", "CherryStudio", "mcp.json"),
                        Linux = Path.Combine(home, ".config", "CherryStudio", "mcp.json"),
                    },
                    InstallSteps = new[] { "Click Configure to write Cherry Studio config" },
                },
                new ClientProfile
                {
                    Id = "witboost", DisplayName = "Witboost",
                    Strategy = ConfigStrategy.JsonFile,
                    Paths = new PlatformPaths
                    {
                        Windows = Path.Combine(home, ".witboost", "mcp.json"),
                        Mac = Path.Combine(home, ".witboost", "mcp.json"),
                        Linux = Path.Combine(home, ".witboost", "mcp.json"),
                    },
                    InstallSteps = new[] { "Click Configure to write Witboost config" },
                },
                new ClientProfile
                {
                    Id = "augment", DisplayName = "Augment Code",
                    Strategy = ConfigStrategy.JsonFile, IsProjectLevel = true,
                    Paths = new PlatformPaths
                    {
                        Windows = Path.Combine(".augment", "mcp.json"),
                        Mac = Path.Combine(".augment", "mcp.json"),
                        Linux = Path.Combine(".augment", "mcp.json"),
                    },
                    InstallSteps = new[] { "Click Configure to write .augment/mcp.json" },
                },
                new ClientProfile
                {
                    Id = "trae", DisplayName = "Trae",
                    Strategy = ConfigStrategy.JsonFile,
                    Paths = new PlatformPaths
                    {
                        Windows = Path.Combine(home, ".trae", "mcp.json"),
                        Mac = Path.Combine(home, ".trae", "mcp.json"),
                        Linux = Path.Combine(home, ".trae", "mcp.json"),
                    },
                    InstallSteps = new[] { "Click Configure to write Trae config" },
                },
                new ClientProfile
                {
                    Id = "zed", DisplayName = "Zed",
                    Strategy = ConfigStrategy.JsonFile,
                    Paths = new PlatformPaths
                    {
                        Windows = Path.Combine(home, ".config", "zed", "settings.json"),
                        Mac = Path.Combine(home, ".config", "zed", "settings.json"),
                        Linux = Path.Combine(home, ".config", "zed", "settings.json"),
                    },
                    InstallSteps = new[] { "Add to context_servers in Zed settings.json" },
                },
                new ClientProfile
                {
                    Id = "claude-desktop", DisplayName = "Claude Desktop",
                    Strategy = ConfigStrategy.JsonFile,
                    Paths = new PlatformPaths
                    {
                        Windows = Path.Combine(home, "AppData", "Roaming", "Claude", "claude_desktop_config.json"),
                        Mac = Path.Combine(home, "Library", "Application Support", "Claude", "claude_desktop_config.json"),
                        Linux = Path.Combine(home, ".config", "Claude", "claude_desktop_config.json"),
                    },
                    InstallSteps = new[] { "Click Configure to write claude_desktop_config.json" },
                },
                new ClientProfile
                {
                    Id = "vscode-insiders", DisplayName = "VS Code Insiders",
                    Strategy = ConfigStrategy.JsonFile, IsProjectLevel = true,
                    Paths = new PlatformPaths
                    {
                        Windows = Path.Combine(".vscode-insiders", "mcp.json"),
                        Mac = Path.Combine(".vscode-insiders", "mcp.json"),
                        Linux = Path.Combine(".vscode-insiders", "mcp.json"),
                    },
                    InstallSteps = new[] { "Click Configure to write .vscode-insiders/mcp.json" },
                },
                new ClientProfile
                {
                    Id = "rider", DisplayName = "JetBrains Rider",
                    Strategy = ConfigStrategy.JsonFile, IsProjectLevel = true,
                    Paths = new PlatformPaths
                    {
                        Windows = Path.Combine(".idea", "mcp.json"),
                        Mac = Path.Combine(".idea", "mcp.json"),
                        Linux = Path.Combine(".idea", "mcp.json"),
                    },
                    InstallSteps = new[] { "Click Configure to write .idea/mcp.json" },
                },
                new ClientProfile
                {
                    Id = "kiro", DisplayName = "Kiro",
                    Strategy = ConfigStrategy.JsonFile, IsProjectLevel = true,
                    Paths = new PlatformPaths
                    {
                        Windows = Path.Combine(".kiro", "mcp.json"),
                        Mac = Path.Combine(".kiro", "mcp.json"),
                        Linux = Path.Combine(".kiro", "mcp.json"),
                    },
                    InstallSteps = new[] { "Click Configure to write .kiro/mcp.json" },
                },
                new ClientProfile
                {
                    Id = "gemini-cli", DisplayName = "Gemini CLI",
                    Strategy = ConfigStrategy.JsonFile,
                    Paths = new PlatformPaths
                    {
                        Windows = Path.Combine(home, ".gemini", "settings.json"),
                        Mac = Path.Combine(home, ".gemini", "settings.json"),
                        Linux = Path.Combine(home, ".gemini", "settings.json"),
                    },
                    InstallSteps = new[] { "Click Configure to write Gemini CLI settings" },
                },
                new ClientProfile
                {
                    Id = "codex", DisplayName = "OpenAI Codex CLI",
                    Strategy = ConfigStrategy.JsonFile, IsProjectLevel = true,
                    Paths = new PlatformPaths
                    {
                        Windows = Path.Combine(".codex", "mcp.json"),
                        Mac = Path.Combine(".codex", "mcp.json"),
                        Linux = Path.Combine(".codex", "mcp.json"),
                    },
                    InstallSteps = new[] { "Click Configure to write .codex/mcp.json" },
                },
                new ClientProfile
                {
                    Id = "copilot-cli", DisplayName = "GitHub Copilot CLI",
                    Strategy = ConfigStrategy.JsonFile,
                    Paths = new PlatformPaths
                    {
                        Windows = Path.Combine(home, ".github-copilot", "mcp.json"),
                        Mac = Path.Combine(home, ".github-copilot", "mcp.json"),
                        Linux = Path.Combine(home, ".github-copilot", "mcp.json"),
                    },
                    InstallSteps = new[] { "Click Configure to write GitHub Copilot config" },
                },
                new ClientProfile
                {
                    Id = "kilo-code", DisplayName = "Kilo Code",
                    Strategy = ConfigStrategy.JsonFile,
                    Paths = new PlatformPaths
                    {
                        Windows = Path.Combine(home, "AppData", "Roaming", "Code", "User", "globalStorage", "kilocode.kilo-code", "settings", "mcp_settings.json"),
                        Mac = Path.Combine(home, "Library", "Application Support", "Code", "User", "globalStorage", "kilocode.kilo-code", "settings", "mcp_settings.json"),
                        Linux = Path.Combine(home, ".config", "Code", "User", "globalStorage", "kilocode.kilo-code", "settings", "mcp_settings.json"),
                    },
                    InstallSteps = new[] { "Click Configure to write Kilo Code settings" },
                },
                new ClientProfile
                {
                    Id = "open-code", DisplayName = "OpenCode",
                    Strategy = ConfigStrategy.JsonFile,
                    Paths = new PlatformPaths
                    {
                        Windows = Path.Combine(home, ".opencode", "mcp.json"),
                        Mac = Path.Combine(home, ".opencode", "mcp.json"),
                        Linux = Path.Combine(home, ".opencode", "mcp.json"),
                    },
                    InstallSteps = new[] { "Click Configure to write OpenCode config" },
                },
                new ClientProfile
                {
                    Id = "qwen-code", DisplayName = "Qwen Code",
                    Strategy = ConfigStrategy.JsonFile,
                    Paths = new PlatformPaths
                    {
                        Windows = Path.Combine(home, ".qwen-code", "mcp.json"),
                        Mac = Path.Combine(home, ".qwen-code", "mcp.json"),
                        Linux = Path.Combine(home, ".qwen-code", "mcp.json"),
                    },
                    InstallSteps = new[] { "Click Configure to write Qwen Code config" },
                },
                new ClientProfile
                {
                    Id = "codebuddy", DisplayName = "CodeBuddy CLI",
                    Strategy = ConfigStrategy.JsonFile,
                    Paths = new PlatformPaths
                    {
                        Windows = Path.Combine(home, ".codebuddy", "mcp.json"),
                        Mac = Path.Combine(home, ".codebuddy", "mcp.json"),
                        Linux = Path.Combine(home, ".codebuddy", "mcp.json"),
                    },
                    InstallSteps = new[] { "Click Configure to write CodeBuddy config" },
                },
            };
        }
    }
}
