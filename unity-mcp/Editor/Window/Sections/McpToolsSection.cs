using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.UIElements;
using UnityMcp.Editor.Core;

namespace UnityMcp.Editor.Window.Sections
{
    public class McpToolsSection
    {
        private readonly VisualElement _root;
        private readonly Label _toolCountLabel;
        private readonly Label _resourceCountLabel;
        private readonly Label _promptCountLabel;
        private readonly VisualElement _toolListContainer;
        private readonly TextField _searchField;
        private string _searchFilter = "";

        public McpToolsSection(VisualElement panel)
        {
            _root = panel;
            BuildUI();

            _toolCountLabel = _root.Q<Label>("badge-tools-count");
            _resourceCountLabel = _root.Q<Label>("badge-resources-count");
            _promptCountLabel = _root.Q<Label>("badge-prompts-count");
            _toolListContainer = _root.Q("tool-list-container");
            _searchField = _root.Q<TextField>("search-field");

            _searchField.RegisterValueChangedCallback(e =>
            {
                _searchFilter = e.newValue?.ToLower() ?? "";
                RefreshList();
            });

            RefreshList();
            _root.schedule.Execute(RefreshList).Every(5000);
        }

        private void BuildUI()
        {
            // Badges
            var badgeRow = new VisualElement();
            badgeRow.AddToClassList("badge-row");

            badgeRow.Add(CreateBadge("badge-tools-count", "Tools"));
            badgeRow.Add(CreateBadge("badge-resources-count", "Resources"));
            badgeRow.Add(CreateBadge("badge-prompts-count", "Prompts"));
            _root.Add(badgeRow);

            // Search
            var searchField = new TextField { name = "search-field" };
            searchField.AddToClassList("search-bar");
            searchField.Q(className: "unity-text-field__input").style.fontSize = 12;
            // placeholder
            searchField.value = "";
            searchField.tooltip = "Search tools by name or description...";
            _root.Add(searchField);

            // List container
            var container = new VisualElement { name = "tool-list-container" };
            _root.Add(container);
        }

        private static VisualElement CreateBadge(string countName, string label)
        {
            var badge = new VisualElement();
            badge.AddToClassList("badge");
            var countLabel = new Label("0") { name = countName };
            countLabel.AddToClassList("badge-count");
            badge.Add(countLabel);
            var textLabel = new Label(label);
            textLabel.AddToClassList("badge-label");
            badge.Add(textLabel);
            return badge;
        }

        private void RefreshList()
        {
            var registry = McpServer.Registry;
            if (registry == null)
            {
                _toolCountLabel.text = "0";
                _resourceCountLabel.text = "0";
                _promptCountLabel.text = "0";
                _toolListContainer.Clear();
                var msg = new Label("Server not initialized. Tools will appear after the server starts.");
                msg.AddToClassList("info-box");
                _toolListContainer.Add(msg);
                return;
            }

            _toolCountLabel.text = registry.ToolCount.ToString();
            _resourceCountLabel.text = registry.ResourceCount.ToString();
            _promptCountLabel.text = registry.PromptCount.ToString();

            _toolListContainer.Clear();

            // Tools section
            DrawToolsGroup(registry);

            // Resources section
            DrawResourcesGroup(registry);

            // Prompts section
            DrawPromptsGroup(registry);
        }

        private void DrawToolsGroup(ToolRegistry registry)
        {
            var allEntries = registry.GetAllToolEntries().ToList();
            var builtInByGroup = new SortedDictionary<string, List<(string name, string description)>>();
            var customTools = new List<(string name, string description)>();

            foreach (var (name, description, group, builtIn) in allEntries)
            {
                if (!string.IsNullOrEmpty(_searchFilter) &&
                    !name.ToLower().Contains(_searchFilter) &&
                    !(description?.ToLower().Contains(_searchFilter) ?? false))
                    continue;

                if (!builtIn)
                {
                    customTools.Add((name, description));
                    continue;
                }
                string groupKey = string.IsNullOrEmpty(group) ? "Other" : CapitalizeFirst(group);
                if (!builtInByGroup.ContainsKey(groupKey))
                    builtInByGroup[groupKey] = new List<(string, string)>();
                builtInByGroup[groupKey].Add((name, description));
            }

            // Built-in groups
            foreach (var kv in builtInByGroup)
            {
                var groupHeader = new Label($"{kv.Key} ({kv.Value.Count})");
                groupHeader.AddToClassList("tool-group-header");
                _toolListContainer.Add(groupHeader);

                var groupBox = new VisualElement();
                groupBox.AddToClassList("section-box");
                foreach (var (name, description) in kv.Value)
                    groupBox.Add(CreateToolRow(registry, name, description));
                _toolListContainer.Add(groupBox);
            }

            // Custom tools
            if (customTools.Count > 0)
            {
                var customHeader = new Label($"Custom ({customTools.Count})");
                customHeader.AddToClassList("tool-group-header");
                _toolListContainer.Add(customHeader);

                var customBox = new VisualElement();
                customBox.AddToClassList("section-box");
                foreach (var (name, description) in customTools)
                    customBox.Add(CreateToolRow(registry, name, description));
                _toolListContainer.Add(customBox);
            }
        }

        private static VisualElement CreateToolRow(ToolRegistry registry, string name, string description)
        {
            var row = new VisualElement();
            row.AddToClassList("tool-row");

            bool enabled = registry.IsToolEnabled(name);
            var toggle = new Toggle { value = enabled };
            toggle.style.marginRight = 4;
            toggle.RegisterValueChangedCallback(e => registry.SetToolEnabled(name, e.newValue));
            row.Add(toggle);

            var nameLabel = new Label(name);
            nameLabel.AddToClassList("tool-name");
            if (!enabled) nameLabel.style.color = new Color(0.5f, 0.5f, 0.5f);
            row.Add(nameLabel);

            if (!string.IsNullOrEmpty(description))
            {
                var descLabel = new Label(description);
                descLabel.AddToClassList("tool-desc");
                descLabel.tooltip = description;
                row.Add(descLabel);
            }

            return row;
        }

        private void DrawResourcesGroup(ToolRegistry registry)
        {
            var resources = registry.GetAllResourceEntries().ToList();
            var filtered = resources.Where(r =>
                string.IsNullOrEmpty(_searchFilter) ||
                r.name.ToLower().Contains(_searchFilter) ||
                (r.description?.ToLower().Contains(_searchFilter) ?? false)).ToList();

            if (filtered.Count == 0) return;

            var header = new Label($"Resources ({filtered.Count})");
            header.AddToClassList("tool-group-header");
            header.style.marginTop = 8;
            _toolListContainer.Add(header);

            var box = new VisualElement();
            box.AddToClassList("section-box");
            foreach (var (name, description, _) in filtered)
            {
                var row = new VisualElement();
                row.AddToClassList("tool-row");
                var nameLabel = new Label(name);
                nameLabel.AddToClassList("tool-name");
                row.Add(nameLabel);
                if (!string.IsNullOrEmpty(description))
                {
                    var descLabel = new Label(description);
                    descLabel.AddToClassList("tool-desc");
                    row.Add(descLabel);
                }
                box.Add(row);
            }
            _toolListContainer.Add(box);
        }

        private void DrawPromptsGroup(ToolRegistry registry)
        {
            var prompts = registry.GetAllPromptEntries().ToList();
            var filtered = prompts.Where(p =>
                string.IsNullOrEmpty(_searchFilter) ||
                p.name.ToLower().Contains(_searchFilter) ||
                (p.description?.ToLower().Contains(_searchFilter) ?? false)).ToList();

            if (filtered.Count == 0) return;

            var header = new Label($"Prompts ({filtered.Count})");
            header.AddToClassList("tool-group-header");
            header.style.marginTop = 8;
            _toolListContainer.Add(header);

            var box = new VisualElement();
            box.AddToClassList("section-box");
            foreach (var (name, description, _) in filtered)
            {
                var row = new VisualElement();
                row.AddToClassList("tool-row");
                var nameLabel = new Label(name);
                nameLabel.AddToClassList("tool-name");
                row.Add(nameLabel);
                if (!string.IsNullOrEmpty(description))
                {
                    var descLabel = new Label(description);
                    descLabel.AddToClassList("tool-desc");
                    row.Add(descLabel);
                }
                box.Add(row);
            }
            _toolListContainer.Add(box);
        }

        private static string CapitalizeFirst(string s)
        {
            if (string.IsNullOrEmpty(s)) return s;
            return char.ToUpper(s[0]) + s.Substring(1);
        }
    }
}
