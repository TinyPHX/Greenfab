using UnityEngine;
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
        [SerializeField] private string message = "";
        [SerializeField] private MessageType messageType = MessageType.None;
        [SerializeField] private float messageDuration = 1;
        [SerializeField] private bool triggerRevert = false;
        [SerializeField] private bool triggerRevertHierarchy = false;
        [SerializeField] private bool triggerRevertAllInstances = false;
        [SerializeField] private bool triggerApply = false;
        [SerializeField] private bool triggerApplyAll = false;
        [SerializeField] private static List<PrefabLink> prefabLinksInScene;
        [SerializeField] private List<PrefabLink> prefabLinks;
        [SerializeField] private PrefabLink firstPrefabLink;
        [SerializeField, HideInInspector] private static Dictionary<GameObject, List<GameObject>> prefabInstances = new Dictionary<GameObject, List<GameObject>>();
        [SerializeField, HideInInspector] private static GameObject lastSelectedGameObject;
        [SerializeField, HideInInspector] private static GameObject lastSelectedPrefab;

        [SerializeField, HideInInspector] private static bool projectWindowChangedHandled;
        [SerializeField, HideInInspector] private static float projectWindowChangedTime;
        
        //Editor colors
        public static Color editorGray = new Color32(194, 194, 194, 255);
        public static Color selectedBackgroundBlue = new Color(.24f, .48f, .90f);
        public static Color textEditorBlack = new Color32(0, 0, 0, 255);
        public static Color textNormalDefault = new Color32(0, 125, 0, 255);
        public static Color textNormalSelected = new Color32(100, 200, 100, 255);
        public static Color textNormalDisabled = new Color32(100, 150, 100, 255);
        public static Color textNoTargetDefault = new Color32(100, 100, 100, 255);
        public static Color textNoTargetSelected = new Color32(175, 175, 175, 255);
        public static Color textNoTargetDisabled = new Color32(125, 125, 125, 255);

        public Texture2D prefabLinkIcon;

        static PrefabLinkEditor()
        {
            EditorApplication.hierarchyWindowItemOnGUI += HandleHierarchyWindowItemOnGUI;
            EditorApplication.hierarchyWindowChanged += HierarchyWindowChanged;
            EditorApplication.projectWindowChanged += ProjectWindowChanged;
            Selection.selectionChanged += OnSelectionChange;

            if (ExtensionMethods.ExtensionMethods.masterVerbose)
            {
                Debug.Log("PrefabLinkEdior Initilized.");
            }
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
            PrefabLink prefab = lastSelectedPrefab.GetComponent<PrefabLink>();
            PrefabLink prefabInstance = lastSelectedGameObject.GetComponent<PrefabLink>();

            if (prefab != null && prefabInstance != null)
            {
                prefabInstance.Target = prefab.gameObject;
                prefab.Target = prefab.gameObject;
            }

            lastSelectedPrefab = null;
            lastSelectedGameObject = null;

            if (ExtensionMethods.ExtensionMethods.masterVerbose)
            {
                Debug.Log("PrefabLink: New prefab (" + prefab.name + ") created from gameObject (" + prefabInstance.gameObject + ").");
            }
        }

        private static void HierarchyWindowChanged()
        {
            BuildPrefabInstances();

            //Check if prefab instance was just created
            GameObject activeGameObject = Selection.activeGameObject;
            if (activeGameObject != null)
            {
                if (PrefabUtility.GetPrefabType(activeGameObject) == PrefabType.PrefabInstance)
                {
                    PrefabLink prefabLink = activeGameObject.GetComponent<PrefabLink>();

                    if (prefabLink != null)
                    {
                        GameObject prefab = PrefabUtility.GetPrefabParent(prefabLink.gameObject) as GameObject;
                        PrefabUtility.DisconnectPrefabInstance(prefabLink.gameObject);

                        prefabLink.Target = prefab;
                    }
                }
            }
        }

        private static void ProjectWindowChanged()
        {
            BuildPrefabInstances();

            //Update newly created prefabs
            projectWindowChangedHandled = false;
            EditorApplication.update -= ProjectWindowChanged;
            if (projectWindowChangedTime == 0)
            {
                projectWindowChangedTime = (float)EditorApplication.timeSinceStartup;
            }

            if (lastSelectedPrefab != null && lastSelectedGameObject != null)
            {
                UpdateNewlyCreatedPrefabs();
                
                projectWindowChangedHandled = true;
            }
            
            if (projectWindowChangedHandled || projectWindowChangedTime - EditorApplication.timeSinceStartup > .1f)
            {
                projectWindowChangedTime = 0;
            }
            else
            {
                EditorApplication.update += ProjectWindowChanged;
            }
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
            object parentObj = PrefabUtility.GetPrefabParent(prefabLinkInSceen);

            if (parentObj != null)
            {
                GameObject parentGameObject = parentObj as GameObject;

                if (parentGameObject != null)
                {
                    List<GameObject> instances = new List<GameObject> { };

                    if (prefabInstances.ContainsKey(parentGameObject))
                    {
                        instances = prefabInstances[parentGameObject];
                    }

                    instances.AddUnique(prefabLinkInSceen.gameObject);
                    prefabInstances[parentGameObject] = instances;
                }
            }
        }

        void Reset()
        {
            if (prefabInstances.Count == 0)
            {
                BuildPrefabInstances();
            }

            UnityEditorInternal.ComponentUtility.MoveComponentUp(FirstPrefabLink.GetComponent<Component>());

            foreach (PrefabLink prefabLink in PrefabLinks)
            {
                //When adding prefabLink component to a prefab automatically add reference to self.
                if (prefabLink.Target == null && prefabLink.gameObject.IsPrefab())
                {
                    prefabLink.Target = prefabLink.gameObject;
                }
            }
        }

        public List<PrefabLink> PrefabLinks
        {
            get
            {
                if (prefabLinks == null || prefabLinks.Count == 0)
                {
                    prefabLinks = Array.ConvertAll(targets, item => (PrefabLink)item).ToList();
                }

                return prefabLinks;
            }
        }

        public PrefabLink FirstPrefabLink
        {
            get
            {
                if (firstPrefabLink == null)
                {
                    firstPrefabLink = PrefabLinks[0];
                }

                return firstPrefabLink;
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

            int columns = 4;
            float padding = 60;

            float buttonWidth = (EditorGUIUtility.currentViewWidth / columns) - (padding / columns);
            float buttonHeight = EditorGUIUtility.singleLineHeight;
            bool smallButtons = buttonWidth < 90;

            int prefabLinksSelected = PrefabLinks.Count;
            
            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.LabelField("Target", GUILayout.Width(40));

            EditorGUI.BeginChangeCheck();
            GameObject prefabLinkTarget = EditorGUILayout.ObjectField(FirstPrefabLink.Target, typeof(GameObject), GUILayout.ExpandWidth(true)) as GameObject;
            if (EditorGUI.EndChangeCheck())
            {
                foreach (PrefabLink prefabLink in PrefabLinks)
                {
                    prefabLink.Target = prefabLinkTarget;
                }
            }


            if (FirstPrefabLink.Target == null)
            {
                string createPrefabButtonText = "Create Prefab";

                if (prefabLinksSelected > 1)
                {
                    createPrefabButtonText = "Create Prefabs (" + prefabLinksSelected + ")";
                }

                if (smallButtons)
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
                            "Skip Prompts", //0
                            "Show All Prompts", //1
                            "Cancel"); //2 Close with x is also results in a 2.

                        if (multiPrefabDialogueResult != 2)
                        {
                            string absoluteDirectoryPath = EditorUtility.OpenFolderPanel("Select Prefab Folder", "", "");
                            bool processCanceled = false; 
                            int prefabsCreated = 0;

                            foreach(PrefabLink selectedPrefabLink in PrefabLinks)
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
                                    selectedPrefabLink.Target = PrefabUtility.CreatePrefab(prefabPath, selectedPrefabLink.gameObject);
                                    TriggerApplyAll();
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
                            FirstPrefabLink.name + ".prefab",
                            "prefab");

                        if (absolutePath.Length > 0)
                        {
                            FirstPrefabLink.Target = PrefabUtility.CreatePrefab(AbsolutePathToRelative(absolutePath), FirstPrefabLink.gameObject);
                            TriggerApplyAll();
                        }
                    }

                }
            }
            EditorGUILayout.EndHorizontal();
            EditorGUILayout.BeginHorizontal();
            bool prefabFileSelected = false;

            bool canRevert = true;
            bool canRevertAll = true;
            bool canApply = true;
            bool canApplyAll = true;

            if (FirstPrefabLink.Target == null)
            {
                canRevert = false;
                canRevertAll = false;
                canApply = false;
                canApplyAll = false;
            }
            else
            {
                foreach (PrefabLink prefabLink in PrefabLinks)
                {
                    if (prefabLink.gameObject.IsPrefab())
                    {
                        prefabFileSelected = true;
                    }

                    if (prefabLink.Target == prefabLink.gameObject)
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
            
            GUI.enabled = canRevertAll && !PrefabLink.useUnityEditorRevert;
            
            if (GUILayout.Button(smallButtons ? "All" : "Revert All", GUILayout.Width(buttonWidth)))
            {
                TriggerRevertHierarchy();
            }
            
            GUI.enabled = canApply;

            if (GUILayout.Button("Apply", GUILayout.Width(buttonWidth)))
            {
                TriggerApply();
            }

            GUI.enabled = canApplyAll && !PrefabLink.useUnityEditorApply;

            if (GUILayout.Button(smallButtons ? "All" : "Apply All", GUILayout.Width(buttonWidth)))
            {
                TriggerApplyAll();
            }

            EditorGUILayout.EndHorizontal();
            
            GUI.enabled = true;

            foreach (PrefabLink prefabLink in PrefabLinks)
            {
                float startTime = prefabLink.StartTime;
                bool revertSuccessful = prefabLink.copySuccessful;
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
            
            PrefabLink.advancedOptions = EditorGUILayout.Foldout(PrefabLink.advancedOptions, "Advanced");
            if (PrefabLink.advancedOptions)
            {
                PrefabLink.ChangeNames = EditorGUILayout.Toggle("Change Names", PrefabLink.ChangeNames);
                PrefabLink.useUnityEditorRevert = EditorGUILayout.Toggle("Use Unity Revert", PrefabLink.useUnityEditorRevert);
                PrefabLink.useUnityEditorApply = EditorGUILayout.Toggle("Use Unity Apply", PrefabLink.useUnityEditorApply);
                ExtensionMethods.ExtensionMethods.masterVerbose = EditorGUILayout.Toggle("Verbose", ExtensionMethods.ExtensionMethods.masterVerbose);
                GUI.enabled = false;
                EditorGUILayout.Toggle("Is Dirty", firstPrefabLink.Dirty);
                GUI.enabled = true;
                PrefabLink.dirtyChecksPerSecond = EditorGUILayout.Slider("Dirty Checks/Sec", PrefabLink.dirtyChecksPerSecond, 0, 10);
                if (PrefabLink.dirtyChecksPerSecond == 0)
                {
                    if (GUILayout.Button(smallButtons ? "Check" : "Check Dirty", GUILayout.Width(buttonWidth)))
                    {
                        foreach (PrefabLink prefabLink in PrefabLinks)
                        {
                            prefabLink.UpdateDirty();
                        }
                        EditorApplication.RepaintHierarchyWindow();
                    }
                }

                EditorGUI.indentLevel++;
                PrefabLink.debugInfo = EditorGUILayout.Foldout(PrefabLink.debugInfo, "Debug Info");
                if (PrefabLink.debugInfo)
                {
                    EditorGUILayout.TextArea("dirty: " + FirstPrefabLink.Dirty);
                    EditorGUILayout.TextArea("isPrefab: " + FirstPrefabLink.gameObject.IsPrefab());
                    EditorGUILayout.TextArea("hierarchyCount: " + FirstPrefabLink.transform.hierarchyCount);
                    EditorGUILayout.TextArea("hierarchyCapacity: " + FirstPrefabLink.transform.hierarchyCapacity);
                    EditorGUILayout.TextArea("childCount: " + FirstPrefabLink.transform.childCount);
                    EditorGUILayout.TextArea("parentDepth: " + FirstPrefabLink.transform.ParentDepth());
                    EditorGUILayout.TextArea("PrefabLink ID: " + FirstPrefabLink.GetInstanceID());
                    EditorGUILayout.TextArea("PrefabLink.gameObject ID: " + FirstPrefabLink.gameObject.GetInstanceID());
                    if (FirstPrefabLink.Target != null)
                    {
                        EditorGUILayout.TextArea("PrefabLink.target ID: " + FirstPrefabLink.Target.GetInstanceID());
                    }
                }
                EditorGUI.indentLevel--;
            }
            
            if (FirstPrefabLink.gameObject.IsPrefab())
            {
                PrefabLink.prefabOnlyOptions = EditorGUILayout.Foldout(PrefabLink.prefabOnlyOptions, "Prefab Options");
                if (PrefabLink.prefabOnlyOptions)
                {
                    EditorGUILayout.HelpBox("This is experimental and might blow your stuff up.", MessageType.Info);
                    //TODO: Need to check if any prefab links refeence this since it you can revert from scene objects
                    GUI.enabled = false;
                    PrefabLink.propogateChanges = EditorGUILayout.Toggle("Auto Propogate Changes", PrefabLink.propogateChanges);
                    EditorGUILayout.TextArea("Auto Propogate Changes doesn't work yet. It still can't \ncheck changes to avoid overwriting them.");
                    GUI.enabled = true;

                    if (!PrefabLink.propogateChanges)
                    {
                        if (GUILayout.Button("Revert Instances", GUILayout.Width(buttonWidth * 2)))
                        {
                            TriggerRevertAllInstances();
                        }
                    }
                }
            }
            
            UpdateDirtyPrefabLinks();
        }
        
        //TODO: Only update dirty onGUI on the active selection prefabLink and prefabLinks referencing the active selection.
        // Need a better prefablink prefab map to do this.
        private void UpdateDirtyPrefabLinks()
        {
            if (firstPrefabLink != null)
            {
                float timeSinceStartup = (float)EditorApplication.timeSinceStartup;
                if (timeSinceStartup - firstPrefabLink.updateDirtyStartTime > 1 / PrefabLink.dirtyChecksPerSecond)
                {
                    firstPrefabLink.UpdateDirty();
                    firstPrefabLink.updateDirtyStartTime = timeSinceStartup;

                    EditorApplication.RepaintHierarchyWindow();
                }
            }
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

        private void TriggerRevertAllInstances()
        {
            triggerRevertAllInstances = true;
            EditorApplication.update += Update;
        }

        private void TriggerApply()
        {
            triggerApply = true;
            EditorApplication.update += Update;
        }

        private void TriggerApplyAll()
        {
            triggerApplyAll = true;
            EditorApplication.update += Update;
        }

        private void Update()
        {
            EditorApplication.update -= Update;
            
            if (triggerRevert || triggerRevertHierarchy)
            {
                foreach (PrefabLink prefabLink in PrefabLinks)
                {
                    if (PrefabLink.useUnityEditorRevert || prefabLink.gameObject.IsPrefab())
                    {
                        PrefabUtility.ResetToPrefabState(prefabLink);
                    }
                    else
                    {
                        Undo.RegisterFullObjectHierarchyUndo(prefabLink, "Prefab Link Revert");
                        prefabLink.StartTime = (float)EditorApplication.timeSinceStartup;
                        prefabLink.Revert(triggerRevertHierarchy);
                        if (prefabLink != null)
                        {
                            EditorUtility.SetDirty(prefabLink);
                        }
                    }
                }
            }

            if (triggerRevertAllInstances)
            {
                BuildPrefabInstances();

                foreach (PrefabLink prefabLink in PrefabLinks)
                {
                    foreach (GameObject instance in prefabInstances[prefabLink.gameObject])
                    {
                        if (PrefabLink.useUnityEditorRevert)
                        {
                            PrefabUtility.ResetToPrefabState(instance);
                        }
                        else
                        {
                            PrefabLink prefabLinkInstance = instance.GetComponent<PrefabLink>();

                            if (prefabLinkInstance != null)
                            {
                                Undo.RegisterFullObjectHierarchyUndo(prefabLinkInstance, "Prefab Link Revert");
                                prefabLinkInstance.StartTime = (float)EditorApplication.timeSinceStartup;
                                prefabLinkInstance.Revert(triggerRevertHierarchy);
                                EditorUtility.SetDirty(prefabLinkInstance);
                            }
                        }
                    }
                }
            }

            if (triggerApply || triggerApplyAll)
            {
                foreach (PrefabLink prefabLink in PrefabLinks)
                {
                    if (PrefabLink.useUnityEditorApply)
                    {
                        //Undo for applying prefabse is broken 
                        //https://issuetracker.unity3d.com/issues/reverting-changes-on-applied-prefab-crashes-unity

                        if (prefabLink.Target == null)
                        {
                            Debug.LogWarning("Cannot apply changes to a null prefab target.");
                        }
                        else if (!prefabLink.Target.IsPrefab())
                        {
                            Debug.LogWarning("Cannot use UnityEditorApply on a scene object. Only Unity prefab are allowed.");
                        }
                        else
                        {
                            GameObject newPrefab = PrefabUtility.ReplacePrefab(prefabLink.gameObject, prefabLink.Target);
                            prefabLink.Target = newPrefab;
                        }
                    }
                    else
                    {
                        if (prefabLink.Target != null)
                        {
                            Undo.RegisterFullObjectHierarchyUndo(prefabLink.Target, "Prefab Link Apply");
                        }

                        prefabLink.Apply(triggerApplyAll);

                        if (prefabLink.Target != null)
                        {
                            EditorUtility.SetDirty(prefabLink.Target);
                        }
                    }
                }
            }

            Undo.FlushUndoRecordObjects();

            triggerRevert = false;
            triggerRevertHierarchy = false;
            triggerApply = false;
            triggerApplyAll = false;
            triggerRevertAllInstances = false;
        }

        private void MovePrefabLinksToTop()
        {
            foreach (PrefabLink prefabLink in PrefabLinks)
            {
                UnityEditorInternal.ComponentUtility.MoveComponentUp(prefabLink);
            }
        }

        private static void HandleHierarchyWindowItemOnGUI(int instanceID, Rect selectionRect)
        {            
            UnityEngine.Object obj = EditorUtility.InstanceIDToObject(instanceID);

            if (obj != null)
            {
                if (obj is GameObject)
                {
                    GameObject gameObject = obj as GameObject;
                    PrefabLink prefabLink = gameObject.gameObject.GetComponent<PrefabLink>();

                    if (prefabLink != null)
                    {
                        FontStyle prefabLinkFontStyle = FontStyle.Normal;
                        Color prefabLinkColor = textEditorBlack;
                        Color backgroundColor = editorGray;
                        
                        float timeSinceStartup = (float)EditorApplication.timeSinceStartup;
                        if (timeSinceStartup - prefabLink.updateDirtyStartTime > 1 / PrefabLink.dirtyChecksPerSecond)
                        { 
                            prefabLink.UpdateDirty();
                            prefabLink.updateDirtyStartTime = timeSinceStartup;
                        }

                        bool selected = Selection.instanceIDs.Contains(instanceID);
                        bool disabled = !gameObject.activeInHierarchy;
                        bool noTarget = prefabLink.target == null;
                        bool dirty = prefabLink.Dirty;
                        
                        if (dirty || noTarget)
                        {
                            prefabLinkFontStyle = FontStyle.Bold;
                        }

                        if (selected)
                        {
                            backgroundColor = selectedBackgroundBlue;
                        }

                        if (noTarget)
                        {   
                            if (selected)
                            {
                                prefabLinkColor = textNoTargetSelected;
                            }
                            else if (disabled)
                            {
                                prefabLinkColor = textNoTargetDisabled;
                            }
                            else 
                            {
                                prefabLinkColor = textNoTargetDefault;
                            }
                        }
                        else
                        {
                            if (selected)
                            {
                                prefabLinkColor = textNormalSelected;
                            }
                            else if (disabled)
                            {
                                prefabLinkColor = textNormalDisabled;
                            }
                            else 
                            {
                                prefabLinkColor = textNormalDefault;
                            }

                        }

                        Rect offsetRect = new Rect(selectionRect.position + new Vector2(0, 2), selectionRect.size);
                        EditorGUI.DrawRect(selectionRect, backgroundColor);
                        EditorGUI.LabelField(offsetRect, obj.name, new GUIStyle()
                        {
                            normal = new GUIStyleState() { textColor = prefabLinkColor },
                            fontStyle = prefabLinkFontStyle
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