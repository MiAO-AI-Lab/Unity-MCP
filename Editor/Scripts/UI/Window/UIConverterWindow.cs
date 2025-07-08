using UnityEngine;
using UnityEditor;
using System.IO;

namespace com.miao.unity.mcp.Editor.API.Tool
{
    /// <summary>
    /// UXML to uGUI Converter Window
    /// </summary>
    public class UIConverterWindow : EditorWindow
    {
        private string _uxmlPath = "";
        private string _ussPath = "";
        private Transform _parentTransform;
        private string _folderPath = "";
        private Vector2 _scrollPosition;
        private string _lastResult = "";
        private bool _showResult = false;
        private float _canvasWidth = 1920f;
        private float _canvasHeight = 1080f;

        [MenuItem("Tools/UXML to uGUI Converter")]
        public static void ShowWindow()
        {
            var window = GetWindow<UIConverterWindow>("UXML to uGUI Converter");
            window.minSize = new Vector2(400, 300);
            window.Show();
        }

        private void OnGUI()
        {
            _scrollPosition = EditorGUILayout.BeginScrollView(_scrollPosition);
            
            GUILayout.Label("UXML to uGUI Converter", EditorStyles.boldLabel);
            GUILayout.Space(10);

            // Single file conversion area
            GUILayout.Label("Single File Conversion", EditorStyles.boldLabel);
            EditorGUILayout.BeginVertical("box");
            {
                // UXML file selection
                EditorGUILayout.BeginHorizontal();
                EditorGUILayout.LabelField("UXML File:", GUILayout.Width(80));
                _uxmlPath = EditorGUILayout.TextField(_uxmlPath);
                if (GUILayout.Button("Select", GUILayout.Width(50)))
                {
                    var path = EditorUtility.OpenFilePanel("Select UXML File", "Assets", "uxml");
                    if (!string.IsNullOrEmpty(path))
                    {
                        _uxmlPath = GetRelativePath(path);
                    }
                }
                EditorGUILayout.EndHorizontal();

                // USS file selection
                EditorGUILayout.BeginHorizontal();
                EditorGUILayout.LabelField("USS File:", GUILayout.Width(80));
                _ussPath = EditorGUILayout.TextField(_ussPath);
                if (GUILayout.Button("Select", GUILayout.Width(50)))
                {
                    var path = EditorUtility.OpenFilePanel("Select USS File", "Assets", "uss");
                    if (!string.IsNullOrEmpty(path))
                    {
                        _ussPath = GetRelativePath(path);
                    }
                }
                EditorGUILayout.EndHorizontal();

                // Parent transform
                EditorGUILayout.BeginHorizontal();
                EditorGUILayout.LabelField("Parent Object:", GUILayout.Width(80));
                _parentTransform = EditorGUILayout.ObjectField(_parentTransform, typeof(Transform), true) as Transform;
                EditorGUILayout.EndHorizontal();

                GUILayout.Space(10);
                
                // Convert button
                if (GUILayout.Button("Convert Single File", GUILayout.Height(30)))
                {
                    ConvertSingleFile();
                }
            }
            EditorGUILayout.EndVertical();

            GUILayout.Space(10);

            // Canvas size: width, height
            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.LabelField("Canvas Size:", GUILayout.Width(80));
            _canvasWidth = EditorGUILayout.FloatField(_canvasWidth);
            _canvasHeight = EditorGUILayout.FloatField(_canvasHeight);
            EditorGUILayout.EndHorizontal();
            GUILayout.Space(10);

            // Result display area
            if (_showResult)
            {
                GUILayout.Label("Conversion Result", EditorStyles.boldLabel);
                EditorGUILayout.BeginVertical("box");
                {
                    EditorGUILayout.TextArea(_lastResult, GUILayout.ExpandHeight(true));
                }
                EditorGUILayout.EndVertical();
            }

            GUILayout.Space(10);

            // Instructions area
            GUILayout.Label("Instructions", EditorStyles.boldLabel);
            EditorGUILayout.BeginVertical("box");
            {
                EditorGUILayout.HelpBox(
                    "1. Select the UXML file to convert and the corresponding USS style file (optional)\n" +
                    "2. You can select a parent object, and the converted UI will be created as its child\n" +
                    "3. Supports batch conversion of all UXML files in a folder\n" +
                    "4. Supported UI elements for conversion: VisualElement, Label, Button, TextField, Image, ScrollView, Slider, Toggle, Dropdown\n" +
                    "5. Supported CSS styles: width, height, background-color, color, font-size",
                    MessageType.Info);
            }
            EditorGUILayout.EndVertical();

            EditorGUILayout.EndScrollView();
        }

        private void ConvertSingleFile()
        {
            if (string.IsNullOrEmpty(_uxmlPath))
            {
                ShowResult("Error: Please select UXML file");
                return;
            }

            if (!File.Exists(_uxmlPath))
            {
                ShowResult($"Error: UXML file does not exist: {_uxmlPath}");
                return;
            }

            string ussPath = null;
            if (!string.IsNullOrEmpty(_ussPath) && File.Exists(_ussPath))
            {
                ussPath = _ussPath;
            }

            var result = UI.ConvertUXMLToUGUI(_uxmlPath, ussPath, _parentTransform, _canvasWidth, _canvasHeight);
            ShowResult(result);
        }

        private void BatchConvertFolder()
        {
            if (string.IsNullOrEmpty(_folderPath))
            {
                ShowResult("Error: Please select folder");
                return;
            }

            if (!Directory.Exists(_folderPath))
            {
                ShowResult($"Error: Folder does not exist: {_folderPath}");
                return;
            }

            var result = UI.BatchConvertUXMLToUGUI(_folderPath, _parentTransform);
            ShowResult(result);
        }

        private void ShowResult(string result)
        {
            _lastResult = result;
            _showResult = true;
            
            // If it's a success message, also output to console
            if (result.StartsWith("Success"))
            {
                Debug.Log($"[UXML Converter] {result}");
            }
            else
            {
                Debug.LogWarning($"[UXML Converter] {result}");
            }
        }

        private string GetRelativePath(string absolutePath)
        {
            if (absolutePath.StartsWith(Application.dataPath))
            {
                return "Assets" + absolutePath.Substring(Application.dataPath.Length);
            }
            return absolutePath;
        }
    }
} 