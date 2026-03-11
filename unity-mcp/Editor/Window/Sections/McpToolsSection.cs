using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;
using UnityMcp.Editor.Core;

namespace UnityMcp.Editor.Window.Sections
{
    public class McpToolsSection
    {
        private readonly VisualElement _root;
        private VisualElement _listContainer;
        private VisualElement _actionBar;
        private TextField _searchField;
        private string _searchFilter = "";

        // Sub-tabs
        private Button[] _subTabs;
        private int _activeSubTab;

        // Foldout tracking for expand/collapse all
        private readonly List<Foldout> _activeFoldouts = new();

        private enum SubTab { Tools = 0, Resources = 1, Prompts = 2 }

        public McpToolsSection(VisualElement panel)
        {
            _root = panel;
            BuildUI();
            RefreshList();
            _root.schedule.Execute(RefreshList).Every(5000);
        }

        private void BuildUI()
        {
            // Sub-tabs row
            var tabRow = new VisualElement();
            tabRow.AddToClassList("sub-tab-row");

            _subTabs = new Button[3];
            string[] tabLabels = { "Tools", "Resources", "Prompts" };
            for (int i = 0; i < 3; i++)
            {
                int idx = i;
                var btn = new Button(() => SwitchSubTab(idx)) { text = tabLabels[i] };
                btn.AddToClassList("sub-tab-btn");
                tabRow.Add(btn);
                _subTabs[i] = btn;
            }
            _root.Add(tabRow);

            // Action bar
            _actionBar = new VisualElement();
            _actionBar.AddToClassList("tools-action-bar");
            _root.Add(_actionBar);

            // Search
            _searchField = new TextField { name = "search-field" };
            _searchField.AddToClassList("search-bar");
            _searchField.Q(className: "unity-text-field__input").style.fontSize = 12;
            _searchField.tooltip = "Search by name or description...";
            _searchField.RegisterValueChangedCallback(e =>
            {
                _searchFilter = e.newValue?.ToLower() ?? "";
                RefreshList();
            });
            _root.Add(_searchField);

            // List container
            _listContainer = new VisualElement { name = "tool-list-container" };
            _root.Add(_listContainer);

            SwitchSubTab(0);
        }

        private void RebuildActionBar()
        {
            _actionBar.Clear();

            var registry = McpServer.Registry;
            bool hasItems = registry != null;

            switch ((SubTab)_activeSubTab)
            {
                case SubTab.Tools:
                {
                    var selectAllBtn = new Button(OnSelectAllTools) { text = "Select All" };
                    selectAllBtn.AddToClassList("tools-action-btn");
                    selectAllBtn.SetEnabled(hasItems);
                    _actionBar.Add(selectAllBtn);

                    var deselectAllBtn = new Button(OnDeselectAllTools) { text = "Deselect All" };
                    deselectAllBtn.AddToClassList("tools-action-btn");
                    deselectAllBtn.SetEnabled(hasItems);
                    _actionBar.Add(deselectAllBtn);

                    AddSpacer();

                    var expandBtn = new Button(OnExpandAll) { text = "Expand All" };
                    expandBtn.AddToClassList("tools-action-btn");
                    _actionBar.Add(expandBtn);

                    var collapseBtn = new Button(OnCollapseAll) { text = "Collapse All" };
                    collapseBtn.AddToClassList("tools-action-btn");
                    _actionBar.Add(collapseBtn);

                    AddSpacer();

                    var rescanBtn = new Button(OnRescan) { text = "Rescan" };
                    rescanBtn.AddToClassList("tools-action-btn");
                    _actionBar.Add(rescanBtn);
                    break;
                }
                case SubTab.Resources:
                {
                    var selectAllBtn = new Button(OnSelectAllResources) { text = "Select All" };
                    selectAllBtn.AddToClassList("tools-action-btn");
                    selectAllBtn.SetEnabled(hasItems);
                    _actionBar.Add(selectAllBtn);

                    var deselectAllBtn = new Button(OnDeselectAllResources) { text = "Deselect All" };
                    deselectAllBtn.AddToClassList("tools-action-btn");
                    deselectAllBtn.SetEnabled(hasItems);
                    _actionBar.Add(deselectAllBtn);
                    break;
                }
                case SubTab.Prompts:
                {
                    var selectAllBtn = new Button(OnSelectAllPrompts) { text = "Select All" };
                    selectAllBtn.AddToClassList("tools-action-btn");
                    selectAllBtn.SetEnabled(hasItems);
                    _actionBar.Add(selectAllBtn);

                    var deselectAllBtn = new Button(OnDeselectAllPrompts) { text = "Deselect All" };
                    deselectAllBtn.AddToClassList("tools-action-btn");
                    deselectAllBtn.SetEnabled(hasItems);
                    _actionBar.Add(deselectAllBtn);
                    break;
                }
            }

            void AddSpacer()
            {
                var spacer = new VisualElement();
                spacer.style.width = 8;
                _actionBar.Add(spacer);
            }
        }

        private void SwitchSubTab(int idx)
        {
            _activeSubTab = idx;
            for (int i = 0; i < _subTabs.Length; i++)
            {
                if (i == idx)
                    _subTabs[i].AddToClassList("sub-tab-active");
                else
                    _subTabs[i].RemoveFromClassList("sub-tab-active");
            }
            RefreshList();
        }

        private void RefreshList()
        {
            var registry = McpServer.Registry;
            _listContainer.Clear();
            _activeFoldouts.Clear();

            RebuildActionBar();

            if (registry == null)
            {
                var msg = new Label("Server not initialized. Items will appear after the server starts.");
                msg.AddToClassList("info-box");
                _listContainer.Add(msg);
                UpdateTabCounts(0, 0, 0);
                return;
            }

            UpdateTabCounts(registry.ToolCount, registry.ResourceCount, registry.PromptCount);

            switch ((SubTab)_activeSubTab)
            {
                case SubTab.Tools:
                    DrawToolsTab(registry);
                    break;
                case SubTab.Resources:
                    DrawResourcesTab(registry);
                    break;
                case SubTab.Prompts:
                    DrawPromptsTab(registry);
                    break;
            }
        }

        private void UpdateTabCounts(int tools, int resources, int prompts)
        {
            if (_subTabs == null) return;
            _subTabs[0].text = $"Tools ({tools})";
            _subTabs[1].text = $"Resources ({resources})";
            _subTabs[2].text = $"Prompts ({prompts})";
        }

        // =====================
        //  Tools tab
        // =====================

        private void DrawToolsTab(ToolRegistry registry)
        {
            var allEntries = registry.GetAllToolEntries().ToList();
            var builtInByGroup = new SortedDictionary<string, List<(string name, string description)>>();
            var customTools = new List<(string name, string description)>();

            foreach (var (name, description, group, builtIn) in allEntries)
            {
                if (!MatchesFilter(name, description)) continue;

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

            if (builtInByGroup.Count == 0 && customTools.Count == 0)
            {
                var msg = new Label("No tools found.");
                msg.AddToClassList("info-box");
                _listContainer.Add(msg);
                return;
            }

            // Built-in category
            if (builtInByGroup.Count > 0)
            {
                var builtInHeader = new Label("Built-in");
                builtInHeader.AddToClassList("tool-category-label");
                builtInHeader.AddToClassList("tool-category-builtin");
                _listContainer.Add(builtInHeader);

                foreach (var kv in builtInByGroup)
                    BuildToolGroupFoldout(registry, kv.Key, kv.Value, $"builtin-{kv.Key}");
            }

            // Custom category
            if (customTools.Count > 0)
            {
                var customHeader = new Label("Custom");
                customHeader.AddToClassList("tool-category-label");
                customHeader.AddToClassList("tool-category-custom");
                _listContainer.Add(customHeader);

                BuildToolGroupFoldout(registry, "Custom", customTools, "custom");
            }
        }

        private void BuildToolGroupFoldout(ToolRegistry registry, string title,
            List<(string name, string desc)> tools, string prefsSuffix)
        {
            int enabledCount = tools.Count(t => registry.IsToolEnabled(t.name));
            string prefKey = $"UnityMcp_Foldout_{prefsSuffix}";
            bool defaultOpen = prefsSuffix.StartsWith("builtin-");

            var foldout = new Foldout
            {
                text = $"{title} ({enabledCount}/{tools.Count})",
                value = EditorPrefs.GetBool(prefKey, defaultOpen)
            };
            foldout.AddToClassList("tool-group-foldout");

            foldout.RegisterValueChangedCallback(evt =>
            {
                if (evt.target != foldout) return;
                EditorPrefs.SetBool(prefKey, evt.newValue);
            });

            // Group toggle checkbox in foldout header
            bool allEnabled = enabledCount == tools.Count;
            var groupToggle = new Toggle { value = allEnabled };
            groupToggle.AddToClassList("group-header-toggle");
            groupToggle.tooltip = $"Toggle all tools in \"{title}\"";
            groupToggle.RegisterCallback<ClickEvent>(evt => evt.StopPropagation());
            groupToggle.RegisterValueChangedCallback(evt =>
            {
                evt.StopPropagation();
                foreach (var (name, _) in tools)
                    registry.SetToolEnabled(name, evt.newValue);
                RefreshList();
            });

            foldout.Q<Toggle>()?.Add(groupToggle);

            foreach (var (name, desc) in tools)
                foldout.Add(CreateToolRow(registry, name, desc));

            _activeFoldouts.Add(foldout);
            _listContainer.Add(foldout);
        }

        private VisualElement CreateToolRow(ToolRegistry registry, string name, string description)
        {
            var row = new VisualElement();
            row.AddToClassList("tool-row");

            bool enabled = registry.IsToolEnabled(name);
            var toggle = new Toggle { value = enabled };
            toggle.style.marginRight = 4;
            toggle.RegisterValueChangedCallback(e =>
            {
                registry.SetToolEnabled(name, e.newValue);
                RefreshList();
            });
            row.Add(toggle);

            var nameLabel = new Label(name);
            nameLabel.AddToClassList("tool-name");
            if (!enabled) nameLabel.AddToClassList("tool-name-disabled");
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

        // =====================
        //  Resources tab
        // =====================

        private void DrawResourcesTab(ToolRegistry registry)
        {
            var resources = registry.GetAllResourceEntries().ToList();
            var filtered = resources.Where(r => MatchesFilter(r.name, r.description)).ToList();

            if (filtered.Count == 0)
            {
                var msg = new Label("No resources found.");
                msg.AddToClassList("info-box");
                _listContainer.Add(msg);
                return;
            }

            var box = new VisualElement();
            box.AddToClassList("section-box");
            foreach (var (name, description, uri, _) in filtered)
            {
                var row = new VisualElement();
                row.AddToClassList("tool-row");

                bool enabled = registry.IsResourceEnabled(uri);
                var toggle = new Toggle { value = enabled };
                toggle.style.marginRight = 4;
                toggle.RegisterValueChangedCallback(e =>
                {
                    registry.SetResourceEnabled(uri, e.newValue);
                    RefreshList();
                });
                row.Add(toggle);

                var nameLabel = new Label(name);
                nameLabel.AddToClassList("tool-name");
                if (!enabled) nameLabel.AddToClassList("tool-name-disabled");
                row.Add(nameLabel);

                if (!string.IsNullOrEmpty(description))
                {
                    var descLabel = new Label(description);
                    descLabel.AddToClassList("tool-desc");
                    descLabel.tooltip = description;
                    row.Add(descLabel);
                }
                box.Add(row);
            }
            _listContainer.Add(box);
        }

        // =====================
        //  Prompts tab
        // =====================

        private void DrawPromptsTab(ToolRegistry registry)
        {
            var prompts = registry.GetAllPromptEntries().ToList();
            var filtered = prompts.Where(p => MatchesFilter(p.name, p.description)).ToList();

            if (filtered.Count == 0)
            {
                var msg = new Label("No prompts found.");
                msg.AddToClassList("info-box");
                _listContainer.Add(msg);
                return;
            }

            var box = new VisualElement();
            box.AddToClassList("section-box");
            foreach (var (name, description, _) in filtered)
            {
                var row = new VisualElement();
                row.AddToClassList("tool-row");

                bool enabled = registry.IsPromptEnabled(name);
                var toggle = new Toggle { value = enabled };
                toggle.style.marginRight = 4;
                toggle.RegisterValueChangedCallback(e =>
                {
                    registry.SetPromptEnabled(name, e.newValue);
                    RefreshList();
                });
                row.Add(toggle);

                var nameLabel = new Label(name);
                nameLabel.AddToClassList("tool-name");
                if (!enabled) nameLabel.AddToClassList("tool-name-disabled");
                row.Add(nameLabel);

                if (!string.IsNullOrEmpty(description))
                {
                    var descLabel = new Label(description);
                    descLabel.AddToClassList("tool-desc");
                    descLabel.tooltip = description;
                    row.Add(descLabel);
                }
                box.Add(row);
            }
            _listContainer.Add(box);
        }

        // =====================
        //  Actions
        // =====================

        private void OnSelectAllTools()
        {
            var registry = McpServer.Registry;
            if (registry == null) return;
            registry.SetAllToolsEnabled(true);
            RefreshList();
        }

        private void OnDeselectAllTools()
        {
            var registry = McpServer.Registry;
            if (registry == null) return;
            registry.SetAllToolsEnabled(false);
            RefreshList();
        }

        private void OnSelectAllResources()
        {
            var registry = McpServer.Registry;
            if (registry == null) return;
            registry.SetAllResourcesEnabled(true);
            RefreshList();
        }

        private void OnDeselectAllResources()
        {
            var registry = McpServer.Registry;
            if (registry == null) return;
            registry.SetAllResourcesEnabled(false);
            RefreshList();
        }

        private void OnSelectAllPrompts()
        {
            var registry = McpServer.Registry;
            if (registry == null) return;
            registry.SetAllPromptsEnabled(true);
            RefreshList();
        }

        private void OnDeselectAllPrompts()
        {
            var registry = McpServer.Registry;
            if (registry == null) return;
            registry.SetAllPromptsEnabled(false);
            RefreshList();
        }

        private void OnExpandAll()
        {
            foreach (var foldout in _activeFoldouts)
                foldout.value = true;
        }

        private void OnCollapseAll()
        {
            foreach (var foldout in _activeFoldouts)
                foldout.value = false;
        }

        private void OnRescan()
        {
            var registry = McpServer.Registry;
            if (registry == null) return;
            registry.ScanAll();
            RefreshList();
        }

        // =====================
        //  Helpers
        // =====================

        private bool MatchesFilter(string name, string description)
        {
            if (string.IsNullOrEmpty(_searchFilter)) return true;
            return name.ToLower().Contains(_searchFilter) ||
                   (description?.ToLower().Contains(_searchFilter) ?? false);
        }

        private static string CapitalizeFirst(string s)
        {
            if (string.IsNullOrEmpty(s)) return s;
            return char.ToUpper(s[0]) + s.Substring(1);
        }
    }
}
