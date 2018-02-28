﻿using UnityEngine;
using UnityEditor;
using TP.ExtensionMethods;
using System;
using System.Linq;
using System.Collections.Generic;
using Object = UnityEngine.Object;
using System.IO;

namespace TP.Greenfab
{
    [CustomEditor(typeof(PrefabLink)), ExecuteInEditMode, CanEditMultipleObjects, InitializeOnLoad, Serializable]
    public class PrefabLinkEditor : Editor
    {
        [SerializeField] public string message = "";
        [SerializeField] public MessageType messageType = MessageType.None;
        [SerializeField] public float messageDuration = 1;
        [SerializeField] public List<PrefabLink> prefabLinks;
        [SerializeField] public bool triggerRevert = false;
        [SerializeField] public bool triggerRevertHierarchy = false;
        [SerializeField] public bool triggerApply = false;
        [SerializeField, HideInInspector] private static Dictionary<int, List<int>> prefabInstances = new Dictionary<int, List<int>>();
        [SerializeField, HideInInspector] private static GameObject lastSelectedGameObject;
        [SerializeField, HideInInspector] private static GameObject lastSelectedPrefab;

        public Texture2D prefabLinkIcon;

        static PrefabLinkEditor()
        {
            EditorApplication.hierarchyWindowItemOnGUI += HandleHierarchyWindowItemOnGUI;
            EditorApplication.hierarchyWindowChanged += HierarchyWindowChanged;
            EditorApplication.projectWindowChanged += ProjectWindowChanged;
            Selection.selectionChanged += OnSelectionChange;
        }

        private void OnEnable()
        {
            MovePrefabLinksToTop();
        }

        public static void OnSelectionChange()
        {
            GameObject selected = Selection.activeGameObject;

            if (selected != null)
            {
                if (selected.IsPrefab())
                {
                    lastSelectedPrefab = selected;
                    UpdateNewlyCreatedPrefabs();
                }
                else
                {
                    lastSelectedGameObject = selected;
                }
            }
        }

        private static void UpdateNewlyCreatedPrefabs()
        {
            //Check if new prefab created and update possible instance to connect PrefabLink target
            if (lastSelectedPrefab != null && lastSelectedGameObject != null)
            {
                PrefabLink prefabParent = lastSelectedPrefab.GetComponent<PrefabLink>();
                PrefabLink prefabInstance = lastSelectedGameObject.GetComponent<PrefabLink>();

                if (prefabParent != null && prefabInstance != null && prefabInstance.Prefab == null)
                {
                    Object foundParrent = PrefabUtility.GetPrefabParent(prefabInstance);
                    bool match = foundParrent != null && foundParrent.GetInstanceID() == prefabParent.GetInstanceID();

                    if (match)
                    {
                        prefabInstance.Prefab = prefabParent.gameObject;
                    }
                }
            }
        }

        private static void HierarchyWindowChanged()
        {
            BuildPrefabInstances();
        }

        private static void ProjectWindowChanged()
        {
            BuildPrefabInstances();
        }

        static void BuildPrefabInstances()
        {
            PrefabLink[] allPrefabLinksInScene = FindObjectsOfType<PrefabLink>();
            foreach (PrefabLink prefabLinkInSceen in allPrefabLinksInScene)
            {
                UpdatePrefabInstance(prefabLinkInSceen);
            }
        }

        static void UpdatePrefabInstance(PrefabLink prefabLinkInSceen)
        {
            Object prefabParent = PrefabUtility.GetPrefabParent(prefabLinkInSceen);

            if (prefabParent != null)
            {
                int prefabParentId = prefabParent.GetInstanceID();
                int prefabInstanceId = prefabLinkInSceen.GetInstanceID();
                List<int> instances = new List<int> { };

                if (prefabInstances.ContainsKey(prefabParentId))
                {
                    instances = prefabInstances[prefabParentId];
                }

                instances.AddUnique(prefabInstanceId);
                prefabInstances[prefabParentId] = instances;
            }
        }

        void Reset()
        {
            if (prefabInstances.Count == 0)
            {
                BuildPrefabInstances();
            }

            PrefabLink[] prefabLinksTemp = Array.ConvertAll(targets, item => (PrefabLink)item);

            foreach (PrefabLink prefabLink in prefabLinksTemp)
            {
                UnityEditorInternal.ComponentUtility.MoveComponentUp(prefabLink.GetComponent<Component>());
                
                //Stops unity from breaking prefab reference when adding prefab to scene.
                if (prefabLink.Prefab && !prefabLink.Prefab.IsPrefab() && prefabLink.Prefab == prefabLink.gameObject)
                {
                    GameObject prefab = PrefabUtility.GetPrefabParent(prefabLink.gameObject) as GameObject;
                    PrefabUtility.DisconnectPrefabInstance(prefabLink.gameObject);

                    prefabLink.Prefab = prefab;
                }

                //When adding prefab link to a prefab automatically add reference to self.
                if (!prefabLink.Prefab && prefabLink.gameObject.IsPrefab())
                {
                    prefabLink.Prefab = prefabLink.gameObject;
                }
            }
        }

        public override void OnInspectorGUI()
        {            
            Object obj = Selection.activeObject;
            if (obj != null)
            {
                prefabLinkIcon = AssetDatabase.LoadAssetAtPath<Texture2D>("Assets/Packages/TP/Prefabulous/Textures/Prefabulous Icon Big.png");
                IconManager.SetIcon(obj as GameObject, prefabLinkIcon);
            }

            MovePrefabLinksToTop();

            float buttonWidth = (EditorGUIUtility.currentViewWidth / 3) - 20;
            float buttonHeight = EditorGUIUtility.singleLineHeight;
            
            prefabLinks = Array.ConvertAll(targets, item => (PrefabLink)item).ToList();
            PrefabLink firstPrefabLink = prefabLinks[0];
            int prefabLinksSelected = prefabLinks.Count;
            
            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.LabelField("Target", GUILayout.Width(40));

            EditorGUI.BeginChangeCheck();
            firstPrefabLink.Prefab = EditorGUILayout.ObjectField(firstPrefabLink.Prefab, typeof(GameObject), GUILayout.ExpandWidth(true)) as GameObject;
            if (EditorGUI.EndChangeCheck())
            {
                foreach (PrefabLink prefabLink in prefabLinks)
                {
                    prefabLink.Prefab = firstPrefabLink.Prefab;
                }
            }


            if (firstPrefabLink.Prefab == null)
            {
                string createPrefabButtonText = "Create Prefab";

                if (prefabLinksSelected > 1)
                {
                    createPrefabButtonText = "Create Prefabs (" + prefabLinksSelected + ")";
                }

                if (buttonWidth < 90)
                {
                    createPrefabButtonText = "Create";
                }

                if (GUILayout.Button(createPrefabButtonText, GUILayout.Width(buttonWidth)))
                {
                    int multiPrefabDialogueResult = -1;
                    if (prefabLinksSelected > 1)
                    {
                         multiPrefabDialogueResult = EditorUtility.DisplayDialogComplex("You've got multiple PrefabsLinks selected",
                            "We are about to try to create " + prefabLinksSelected + " prefabs. How would you like to continue?",
                            "Skip Propts", //0
                            "Show All Propts", //1
                            "Cancel"); //2 Close with x is also results in a 2.

                        if (multiPrefabDialogueResult != 2)
                        {
                            string absoluteDirectoryPath = EditorUtility.OpenFolderPanel("Select Prefab Folder", "", "");
                            bool processCanceled = false; 
                            int prefabsCreated = 0;

                            foreach(PrefabLink selectedPrefabLink in prefabLinks)
                            {
                                string saveAttemptAppend = "";
                                int fileSaveAttempts = 0;
                                bool saved = false;
                                string prefabName = "";
                                string prefabPath = "";

                                while (!saved)
                                {
                                    prefabName = selectedPrefabLink.name + saveAttemptAppend + ".prefab";
                                    prefabPath = AbsolutePathToRelative(absoluteDirectoryPath) + "/" + prefabName;

                                    if (!File.Exists(prefabPath))
                                    {
                                        saved = true;
                                    }
                                    else
                                    {
                                        fileSaveAttempts++;
                                        saveAttemptAppend = " (" + fileSaveAttempts + ")";
                                    }
                                }

                                if (multiPrefabDialogueResult == 1)
                                {
                                    string absolutePath = EditorUtility.SaveFilePanel(
                                        "Save new Prefab Target",
                                        absoluteDirectoryPath,
                                        prefabName,
                                        "prefab");

                                    if (absolutePath.Length > 0)
                                    {
                                        prefabPath = AbsolutePathToRelative(absolutePath);
                                    }
                                    else
                                    {
                                        processCanceled = true;

                                        EditorUtility.DisplayDialog(
                                            "Process Aborted",
                                            "Process Aborted after creating " + prefabsCreated + " of " + prefabLinksSelected + " prefabs",
                                            "Ok");

                                        break;
                                    }
                                }

                                if (!processCanceled)
                                {
                                    selectedPrefabLink.Prefab = PrefabUtility.CreatePrefab(prefabPath, selectedPrefabLink.gameObject);
                                    TriggerApply();
                                    prefabsCreated++;
                                }
                                else
                                {
                                    break;
                                }
                            }
                        }
                    } else  {
                        string absolutePath = EditorUtility.SaveFilePanel(
                            "Save new Prefab Target",
                            "",
                            firstPrefabLink.name + ".prefab",
                            "prefab");

                        if (absolutePath.Length > 0)
                        {
                            firstPrefabLink.Prefab = PrefabUtility.CreatePrefab(AbsolutePathToRelative(absolutePath), firstPrefabLink.gameObject);
                            TriggerApply();
                        }
                    }

                }
            }
            EditorGUILayout.EndHorizontal();
            EditorGUILayout.BeginHorizontal();
            bool prefabFileSelected = false;

            bool canRevert = true;
            bool canRevertAll = true;

            if (firstPrefabLink.Prefab == null)
            {
                canRevert = false;
                canRevertAll = false;
            }
            else
            {
                foreach (PrefabLink prefabLink in prefabLinks)
                {
                    if (prefabLink.gameObject.IsPrefab())
                    {
                        prefabFileSelected = true;
                    }

                    if (prefabLink.Prefab.GetInstanceID() == prefabLink.gameObject.GetInstanceID())
                    {
                        canRevert = false;
                    }
                }
            }

            GUI.enabled = canRevert;
            
            if (GUILayout.Button("Revert", GUILayout.Width(buttonWidth)))
            {
                TriggerRevert();
            }
            
            GUI.enabled = canRevertAll;

            if (GUILayout.Button("Revert All", GUILayout.Width(buttonWidth)))
            {
                TriggerRevertHierarchy();
            }
            
            GUI.enabled = canRevert;

            if (GUILayout.Button("Apply", GUILayout.Width(buttonWidth)))
            {
                TriggerApply();
            }

            EditorGUILayout.EndHorizontal();
            
            GUI.enabled = true;

            foreach (PrefabLink prefabLink in prefabLinks)
            {
                float startTime = prefabLink.startTime;
                bool revertSuccessful = prefabLink.revertSuccessful;
                float messageDisplayTime = (float)EditorApplication.timeSinceStartup - startTime;

                if (messageDisplayTime < messageDuration && messageDisplayTime > 0)
                {
                    if (!revertSuccessful)
                    {
                        message = prefabLink.name + " - Reverting may have ran into issues. See console output.";
                        messageType = MessageType.Warning;
                    }
                    else
                    {
                        message = prefabLink.name + " - Reverting succesful.";
                        messageType = MessageType.Info;
                    }

                    EditorGUILayout.HelpBox(message, messageType);
                }

            }

            //DEBUGING FIELDS
            
            //EditorGUILayout.TextArea("timeSinceStartup: " + EditorApplication.timeSinceStartup);
            //EditorGUILayout.TextArea("messageStartTime: " + firstPrefabLink.startTime);
            //EditorGUILayout.TextArea("hierarchyCount: " + firstPrefabLink.transform.hierarchyCount);
            //EditorGUILayout.TextArea("hierarchyCapacity: " + firstPrefabLink.transform.hierarchyCapacity);
            //EditorGUILayout.TextArea("childCount: " + firstPrefabLink.transform.childCount);
            //EditorGUILayout.TextArea("parentDepth: " + firstPrefabLink.transform.ParentDepth());

        }

        private void TriggerRevert()
        {
            triggerRevert = true;
            EditorApplication.update += Update;
        }

        private void TriggerRevertHierarchy()
        {
            triggerRevertHierarchy = true;
            EditorApplication.update += Update;
        }

        private void TriggerApply()
        {
            triggerApply = true;
            EditorApplication.update += Update;
        }

        private void Update()
        {
            EditorApplication.update -= Update;
            
            if (triggerRevert)
            {
                foreach (PrefabLink prefabLink in prefabLinks)
                {
                    Undo.RegisterFullObjectHierarchyUndo(prefabLink, "Prefab Link");
                    prefabLink.startTime = (float)EditorApplication.timeSinceStartup;
                    prefabLink.Revert(false, true);
                    EditorUtility.SetDirty(prefabLink);
                }
            }

            if (triggerRevertHierarchy)
            {
                foreach (PrefabLink prefabLink in prefabLinks)
                {
                    Undo.RegisterFullObjectHierarchyUndo(prefabLink, "Prefab Link");
                    prefabLink.startTime = (float)EditorApplication.timeSinceStartup;
                    prefabLink.Revert(true, true);
                    EditorUtility.SetDirty(prefabLink);
                }
            }

            if (triggerApply)
            {
                foreach (PrefabLink prefabLink in prefabLinks)
                {
                    //Uhis for applying prefabse is broken 
                    //https://issuetracker.unity3d.com/issues/reverting-changes-on-applied-prefab-crashes-unity
                    //if (prefabLink.prefab != null)
                    //{
                    //    Undo.RegisterFullObjectHierarchyUndo(prefabLink.prefab, "Prefab Link - Prefab");
                    //}
                    if (prefabLink.Prefab != null)
                    {
                        GameObject newPrefab = PrefabUtility.ReplacePrefab(prefabLink.gameObject, prefabLink.Prefab);
                        prefabLink.Prefab = newPrefab;
                    }
                    //EditorUtility.SetDirty(prefabLink.prefab); 
                }
            }

            Undo.FlushUndoRecordObjects();

            triggerRevert = false;
            triggerRevertHierarchy = false;
            triggerApply = false;
        }

        private void MovePrefabLinksToTop()
        {
            PrefabLink[] prefabLinksTemp = Array.ConvertAll(targets, item => (PrefabLink)item);

            foreach (PrefabLink prefabLink in prefabLinksTemp)
            {
                UnityEditorInternal.ComponentUtility.MoveComponentUp(prefabLink);
            }
        }

        private static void HandleHierarchyWindowItemOnGUI(int instanceID, Rect selectionRect)
        {
            Color prefabLinkColor = new Color32(0, 125, 0, 255);
            Color background = Color.blue;
            Color backgroundColor = new Color32(194, 194, 194, 255);

            UnityEngine.Object obj = EditorUtility.InstanceIDToObject(instanceID);

            if (obj != null)
            {
                if (obj is GameObject)
                {
                    GameObject gameObject = obj as GameObject;

                    if (gameObject.gameObject.GetComponent<PrefabLink>() != null)
                    {
                        if (Selection.instanceIDs.Contains(instanceID))
                        {
                            prefabLinkColor = new Color32(100, 200, 100, 255);
                            backgroundColor = new Color(.24f, .48f, .90f);
                        }

                        if (!gameObject.activeInHierarchy)
                        {
                            prefabLinkColor = new Color32(100, 150, 100, 255);
                        }

                        Rect offsetRect = new Rect(selectionRect.position + new Vector2(0, 2), selectionRect.size);
                        EditorGUI.DrawRect(selectionRect, backgroundColor);
                        EditorGUI.LabelField(offsetRect, obj.name, new GUIStyle()
                        {
                            normal = new GUIStyleState() { textColor = prefabLinkColor },
                            fontStyle = FontStyle.Normal
                        });
                    }
                }
            }
        }

        private string AbsolutePathToRelative(string absolutePath)
        {
            string relativePath = "Assets" + absolutePath.Substring(Application.dataPath.Length);

            return relativePath;
        }
    }
}