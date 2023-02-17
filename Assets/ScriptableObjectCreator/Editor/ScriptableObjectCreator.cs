// -----------------------------------------------------------------------
// <copyright file="ScriptableObjectCreator.cs" company="AillieoTech">
// Copyright (c) AillieoTech. All rights reserved.
// </copyright>
// -----------------------------------------------------------------------

namespace AillieoUtils
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Reflection;
    using UnityEditor;
    using UnityEditor.Compilation;
    using UnityEditor.IMGUI.Controls;
    using UnityEngine;

    internal class ScriptableObjectCreator : EditorWindow
    {
        private static readonly string keyCloseAfterCreation = "AillieoUtils.ScriptableObjectCreator.closeAfterCreation";

        // 静态 一共存一份
        private static Type[] scriptableObjectTypes;

        // 窗口实例初始化一次
        private SearchField searchField;

        // 运行时窗口数据
        private bool closeAfterCreation;
        private List<Type> typesFiltered = new List<Type>();
        private string filter = string.Empty;
        private string className = string.Empty;
        private string assetName = string.Empty;
        private DefaultAsset folder;
        private Vector2 scrollPos;

        [MenuItem("Assets/AillieoUtils/Create ScriptableObject")]
        [MenuItem("AillieoUtils/ScriptableObject Creator")]
        public static void OpenWindow()
        {
            ScriptableObjectCreator creator = GetWindow<ScriptableObjectCreator>("Scriptable Object Creator");

            if (Selection.activeObject is MonoScript mono)
            {
                Type selectedClass = mono.GetClass();
                if (IsValidType(selectedClass))
                {
                    creator.filter = selectedClass.FullName;
                    creator.UpdateFilteredTypeList();
                    creator.className = selectedClass.FullName;
                }
            }

            if (Selection.activeObject is DefaultAsset defaultAsset && AssetDatabase.IsValidFolder(AssetDatabase.GetAssetPath(defaultAsset)))
            {
                creator.folder = defaultAsset;
            }
            else
            {
                var currentFolder = GetCurrentFolder();
                if (!string.IsNullOrEmpty(currentFolder))
                {
                    creator.folder = AssetDatabase.LoadAssetAtPath<DefaultAsset>(currentFolder);
                }
            }

            creator.closeAfterCreation = EditorPrefs.GetBool(keyCloseAfterCreation, true);
        }

        private static bool ContainsIgnoreCase(string str, string value)
        {
            return str.IndexOf(value, StringComparison.OrdinalIgnoreCase) >= 0;
        }

        private static string GetCurrentFolder()
        {
            MethodInfo methodInfo =
                typeof(ProjectWindowUtil)
                .GetMethod("GetActiveFolderPath", BindingFlags.Static | BindingFlags.NonPublic);
            if (methodInfo == null)
            {
                return null;
            }

            return (string)methodInfo.Invoke(null, null);
        }

        private static bool IsValidType(Type type)
        {
            return type.IsSubclassOf(typeof(ScriptableObject)) &&
            !type.IsSubclassOf(typeof(EditorWindow)) &&
            !type.IsSubclassOf(typeof(UnityEditor.Editor)) &&
            !type.IsAbstract;
        }

        private void OnEnable()
        {
            if (scriptableObjectTypes == null)
            {
                var unityAssemblies = CompilationPipeline.GetAssemblies(AssembliesType.Editor);
                var exclude = new HashSet<string>(unityAssemblies.Select(asm => asm.name));

                var assemblies = AppDomain.CurrentDomain.GetAssemblies()
                    .Where(asm => exclude.Contains(asm.GetName().Name));

                var types = assemblies.SelectMany(asm => asm.GetTypes());
                scriptableObjectTypes = types.Where(IsValidType)
               .ToArray();
            }

            if (this.searchField == null)
            {
                this.searchField = new SearchField
                {
                    autoSetFocusOnFindCommand = true,
                };
            }

            this.typesFiltered.Clear();
            this.typesFiltered.AddRange(scriptableObjectTypes);
        }

        private void OnGUI()
        {
            this.DrawSearchField();
            this.DrawTypeList();
            this.DrawCreatePart();
        }

        private void DrawSearchField()
        {
            using (var scope = new EditorGUI.ChangeCheckScope())
            {
                this.filter = this.searchField.OnGUI(this.filter);
                if (scope.changed)
                {
                    this.UpdateFilteredTypeList();
                }
            }
        }

        private void UpdateFilteredTypeList()
        {
            this.typesFiltered.Clear();
            if (string.IsNullOrWhiteSpace(this.filter))
            {
                this.typesFiltered.AddRange(scriptableObjectTypes);
            }
            else
            {
                this.typesFiltered.AddRange(scriptableObjectTypes.Where(t => ContainsIgnoreCase(t.FullName, this.filter)));
            }

            if (!this.typesFiltered.Any(t => t.Name == this.className))
            {
                this.className = string.Empty;
            }
        }

        private void DrawTypeList()
        {
            using (var scope = new EditorGUILayout.ScrollViewScope(this.scrollPos, "box"))
            {
                foreach (var t in this.typesFiltered)
                {
                    var fullName = t.FullName;
                    var selected = fullName == this.className;
                    if (GUILayout.Button(fullName, selected ? EditorStyles.boldLabel : EditorStyles.label))
                    {
                        this.className = fullName;
                    }
                }

                this.scrollPos = scope.scrollPosition;
            }
        }

        private void DrawCreatePart()
        {
            EditorGUILayout.LabelField($"Type to create:", this.className);

            this.folder = EditorGUILayout.ObjectField("Create in folder: ", this.folder, typeof(DefaultAsset), false) as DefaultAsset;
            this.assetName = EditorGUILayout.TextField("New asset name: ", this.assetName);
            var folderPath = AssetDatabase.GetAssetPath(this.folder);
            if (!AssetDatabase.IsValidFolder(folderPath))
            {
                folderPath = "Assets";
            }

            var newAssetName = this.assetName;
            if (string.IsNullOrWhiteSpace(newAssetName))
            {
                newAssetName = $"newAsset.asset";
            }

            if (!newAssetName.Contains("."))
            {
                newAssetName += ".asset";
            }

            var fullpath = $"{folderPath}/{newAssetName}";
            fullpath = AssetDatabase.GenerateUniqueAssetPath(fullpath);

            EditorGUILayout.LabelField($"Will create:", fullpath);

            var invalid = string.IsNullOrEmpty(this.className) || !AssetDatabase.IsValidFolder(folderPath) || string.IsNullOrWhiteSpace(fullpath);

            EditorGUILayout.BeginHorizontal();

            using (var scope = new EditorGUI.DisabledScope(invalid))
            {
                if (GUILayout.Button("Create"))
                {
                    var newAsset = CreateInstance(this.className);
                    AssetDatabase.CreateAsset(newAsset, fullpath);
                    AssetDatabase.SaveAssets();
                    AssetDatabase.Refresh();
                    Selection.activeObject = newAsset;

                    if (this.closeAfterCreation)
                    {
                        this.Close();
                    }
                }
            }

            using (var scope = new EditorGUI.ChangeCheckScope())
            {
                this.closeAfterCreation = EditorGUILayout.ToggleLeft(
                    "Close After Creation",
                    this.closeAfterCreation,
                    GUILayout.ExpandWidth(false));

                if (scope.changed)
                {
                    EditorPrefs.SetBool(keyCloseAfterCreation, this.closeAfterCreation);
                }
            }

            EditorGUILayout.EndHorizontal();
        }
    }
}
