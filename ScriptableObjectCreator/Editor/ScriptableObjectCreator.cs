using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEditor;
using UnityEditor.IMGUI.Controls;
using System.Reflection;
using UnityEditor.Compilation;

namespace AillieoUtils.Editor
{
    public class ScriptableObjectCreator : EditorWindow
    {
        [MenuItem("Assets/AillieoUtils/Create ScriptableObject")]
        [MenuItem("AillieoUtils/ScriptableObject Creator")]
        public static void OpenWindow()
        {
            GetWindow<ScriptableObjectCreator>("Scriptable Object Creator");
        }

        // 静态 一共存一份
        private static Type[] scriptableObjectTypes;

        // 窗口实例初始化一次
        private SearchField searchField;

        // 运行时窗口数据
        private List<Type> typesFiltered = new List<Type>();
        private string filter = string.Empty;
        private string className = string.Empty;
        private string assetName = string.Empty;
        private DefaultAsset folder;
        private Vector2 scrollPos;

        //static ScriptableObjectCreator()
        //{
        //}

        private void OnEnable()
        {
            if (scriptableObjectTypes == null)
            {
                var unityAssemblies = CompilationPipeline.GetAssemblies(AssembliesType.Editor);
                HashSet<string> exclude = new HashSet<string>(unityAssemblies.Select(asm => asm.name));

                var assemblies = AppDomain.CurrentDomain.GetAssemblies()
                    .Where(asm => exclude.Contains(asm.GetName().Name));

                var types = assemblies.SelectMany(asm => asm.GetTypes());
                scriptableObjectTypes = types.Where(
                    t => t.IsSubclassOf(typeof(ScriptableObject)) &&
                    !t.IsSubclassOf(typeof(EditorWindow)) &&
                    !t.IsSubclassOf(typeof(UnityEditor.Editor)) &&
                    !t.IsAbstract)
               .ToArray();
            }

            if (searchField == null)
            {
                searchField = new SearchField();
                searchField.autoSetFocusOnFindCommand = true;
            }

            typesFiltered.Clear();
            typesFiltered.AddRange(scriptableObjectTypes);
        }

        private void OnGUI()
        {
            DrawSearchField();
            DrawTypeList();
            DrawCreatePart();
        }

        private void DrawSearchField()
        {
            using (var scope = new EditorGUI.ChangeCheckScope())
            {
                filter = searchField.OnGUI(filter);
                if (scope.changed)
                {
                    typesFiltered.Clear();
                    if (string.IsNullOrWhiteSpace(filter))
                    {
                        typesFiltered.AddRange(scriptableObjectTypes);
                    }
                    else
                    {
                        typesFiltered.AddRange(scriptableObjectTypes.Where(t => ContainsIgnoreCase(t.FullName, filter)));
                    }

                    if (!typesFiltered.Any(t => t.Name == className))
                    {
                        className = string.Empty;
                    }
                }
            }
        }

        private void DrawTypeList()
        {
            using (var scope = new EditorGUILayout.ScrollViewScope(scrollPos, "box"))
            {
                foreach (var t in typesFiltered)
                {
                    string fullName = t.FullName;
                    bool selected = fullName == className;
                    if (GUILayout.Button(fullName, selected ? EditorStyles.boldLabel : EditorStyles.label))
                    {
                        className = fullName;
                    }
                }

                scrollPos = scope.scrollPosition;
            }
        }

        private void DrawCreatePart()
        {
            EditorGUILayout.LabelField($"Type to create:", className);

            folder = EditorGUILayout.ObjectField("Create in folder: ", folder, typeof(DefaultAsset), false) as DefaultAsset;
            assetName = EditorGUILayout.TextField("New asset name: ", assetName);
            var folderPath = AssetDatabase.GetAssetPath(folder);
            if (!AssetDatabase.IsValidFolder(folderPath))
            {
                folderPath = "Assets";
            }

            string newAssetName = assetName;
            if (string.IsNullOrWhiteSpace(newAssetName))
            {
                newAssetName = $"newAsset.asset";
            }

            if (!newAssetName.Contains("."))
            {
                newAssetName += ".asset";
            }

            string fullpath = $"{folderPath}/{newAssetName}";
            fullpath = AssetDatabase.GenerateUniqueAssetPath(fullpath);

            EditorGUILayout.LabelField($"Will create:", fullpath);

            bool invalid = string.IsNullOrEmpty(className) || !AssetDatabase.IsValidFolder(folderPath) || string.IsNullOrWhiteSpace(fullpath);

            using (var scope = new EditorGUI.DisabledScope(invalid))
            {
                if (GUILayout.Button("Create"))
                {
                    var newAsset = CreateInstance(className);
                    AssetDatabase.CreateAsset(newAsset, fullpath);
                    AssetDatabase.SaveAssets();
                    AssetDatabase.Refresh();
                    Selection.activeObject = newAsset;
                }
            }
        }

        private static bool ContainsIgnoreCase(string str, string value)
        {
            return str.IndexOf(value, StringComparison.OrdinalIgnoreCase) >= 0;
        }
    }
}
