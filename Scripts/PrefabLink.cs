using TP.ExtensionMethods;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using System.Reflection;
using System;
using System.Linq;
using ValueEqualsReport = TP.ExtensionMethods.ExtensionMethods.ValueEqualsReport;
using ValueEqualsReportMatch = TP.ExtensionMethods.ExtensionMethods.ValueEqualsReportMatch;
using ValueEqualsReportMatchType = TP.ExtensionMethods.ExtensionMethods.ValueEqualsReportMatchType;

#if UNITY_EDITOR
using UnityEditor;
#endif

namespace TP.Greenfab
{

    [Serializable]
    public class PrefabLink : MonoBehaviour
    {
        public GameObject target;

        private bool revertSuccessful;
        private bool dirty;
        private ValueEqualsReport dirtyReport = new ValueEqualsReport();

        //Used by editor
        private float revertStartTime;
        public float updateDirtyStartTime;
        public static float dirtyChecksPerSecond = 1;
        public static bool ChangeNames
        {
            set { ExtensionMethods.ExtensionMethods.includeNames = value; }
            get { return ExtensionMethods.ExtensionMethods.includeNames; }
        }
        public static bool useUnityEditorApply = false;
        public static bool useUnityEditorRevert = false;
        public static bool advancedOptions = false;
        public static bool prefabOnlyOptions = false;
        public static bool debugInfo = false;
        public static bool showDirtyObjects = false;
        public static bool propogateChanges = false;

        public override bool Equals(object other)
        {
            bool equals = false;

            if (base.Equals(other))
            {
                equals = true;
            }
            else if (other is PrefabLink)
            {
                PrefabLink otherPrefabLink = other as PrefabLink;

                equals = Target == otherPrefabLink.Target;
            }

            return equals;
        }

        public override int GetHashCode()
        {
            return base.GetHashCode() + Target.GetHashCode();
        }

        public void UpdateDirty()
        {
            Dirty = false;

            int addLimit = 0;
            if (dirtyReport != null)
            {
                addLimit = dirtyReport.AddLimit;
            }

            dirtyReport = new ValueEqualsReport();
            dirtyReport.AddLimit = addLimit;
            dirtyReport.IgnoreComponents = new Type[] { typeof(Transform), typeof(PrefabLink) };
            dirtyReport.AddNonMatch(gameObject);
            dirtyReport.AddNonMatch(target);
            dirtyReport.AddMatch(gameObject, target, ValueEqualsReportMatchType.TARGET_EQUAL);

            if (gameObject != null && Target != null)
            {
                Dirty = !gameObject.ValueEquals(Target, dirtyReport);
            }

            //foreach (PrefabLink prefabLink in GetComponentsInChildren<PrefabLink>())
            //{
            //    dirtyReport.AddMatch(prefabLink.gameObject, prefabLink.target, ValueEqualsReportMatchType.TARGET_EQUAL);
            //}
        }
        
        public bool Revert(bool revertChildren=true, bool ignoreTopTransform=true, bool ignorePrefabLink=true)
        {
            GameObject from = Target;
            GameObject to = gameObject;
            
            Revert(from, to, ignoreTopTransform, ignorePrefabLink);

            if (revertChildren)
            {
                foreach (PrefabLink directChildprefabLink in DirectChildPrefabLinks(to))
                {
                    directChildprefabLink.Revert(revertChildren, false, ignorePrefabLink);
                }
            }

            return true;
        }

        public void Revert(GameObject from, GameObject to, bool ignoreTopTransform=true, bool ignorePrefabLink=true)
        {
            Copy(from, to, ignoreTopTransform, ignorePrefabLink);
            revertSuccessful = true;
        }

        public bool Apply(bool applyChildren=true, bool ignoreTopTransform=true, bool ignorePrefabLink=false)
        {
            GameObject from = Target;
            GameObject to = gameObject;

            if (applyChildren)
            {
                foreach (PrefabLink directChildprefabLink in DirectChildPrefabLinks(to))
                {
                    directChildprefabLink.Apply(applyChildren, false, ignorePrefabLink);
                }
            }

            GameObject updatedFrom = Apply(from, to, ignoreTopTransform, ignorePrefabLink);
            
            PrefabLink toPrefabLink = to.GetComponent<PrefabLink>();
            if (toPrefabLink != null)
            {
                toPrefabLink.Target = updatedFrom;
            }

            if (updatedFrom != null)
            {
                PrefabLink fromPrefabLink = updatedFrom.GetComponent<PrefabLink>();
                if (fromPrefabLink != null)
                {
                    fromPrefabLink.Target = updatedFrom;
                }
            }

            return true;
        }

        public GameObject Apply(GameObject from, GameObject to, bool ignoreTopTransform=true, bool ignorePrefabLink=true)
        {
            return Copy(to, from, ignoreTopTransform, ignorePrefabLink);
        }

        public GameObject Copy(GameObject from, GameObject to, bool ignoreTopTransform=true, bool ignorePrefabLink=true)
        {
            GameObject updatedTo = to;

            if (from != to && from != null && to != null)
            {
                //bool unityEditor = false;
                //#if UNITY_EDITOR
                //    if (to.IsPrefab())
                //    {
                //        unityEditor = true; //Cant modify prefab parrents so have to use unity editor.
                //    }
                //#endif

                //if (unityEditor)
                //{
                //    updatedTo = PrefabUtility.ReplacePrefab(from, to);
                //}
                //else
                //{
                //    RemoveComponentsAndChildren(to, ignoreTopTransform, ignorePrefabLink);
                //    CopyComponentsAndChildren(from, to, ignoreTopTransform, ignorePrefabLink);
                //}
                
                UpdateDirty();

                RemoveUnmatchedComponentsAndChildren(to, ignoreTopTransform, true);
                CopyComponentsAndChildren(from, to, ignoreTopTransform, true);
                
                //Repair local object references.

                UpdateDirty();
            }

            return updatedTo;
        }

        public void RemoveUnmatchedComponentsAndChildren(GameObject from, bool ignoreTopTransform=true, bool ignorePrefabLink=true)
        {
            if (from == null) { from = gameObject; }

            //Remove children that dont have a matching gameObject in the target
            for (int i = from.transform.childCount - 1; i >= 0; i--)
            {
                GameObject child = from.transform.GetChild(i).gameObject;
                if (!dirtyReport.Matched(child))
                {
                    RemoveGameObject(child);
                }
                else
                {
                    RemoveUnmatchedComponentsAndChildren(child, ignoreTopTransform, ignorePrefabLink);
                }
            }

            //Remove components that dont have a matching component in the target
            List<Component> componentsToRemove = from.GetComponents<Component>()
                .Where((item) => !dirtyReport.Matched(item))
                .ToList();

            while (componentsToRemove.Count > 0)
            {
                Component component = componentsToRemove[0];

                if (ignoreTopTransform && component.GetType() == typeof(Transform) ||
                    ignorePrefabLink && component.GetType() == typeof(PrefabLink))
                {
                    componentsToRemove.Remove(component);
                }
                else
                {
                    List<Component> removed = new List<Component> { };
                    RemoveComponentAndRequiredComponents(component, removed);

                    componentsToRemove.RemoveRange(removed);
                }
            }
        }

        public void RemoveComponentAndRequiredComponents(Component component, List<Component> removed)
        {
            //TODO check for cicular dependancies

            removed.Add(component);
            
            List<Component> requiredByComponents = component.RequiredByComponents(component.gameObject.GetComponents<Component>().ToList());

            foreach (Component requiredByComponent in requiredByComponents)
            {
                RemoveComponentAndRequiredComponents(requiredByComponent, removed);
            }

            RemoveComponent(component);
        }

        private void RemoveGameObject(GameObject gameObject)
        {
            if (Application.isPlaying)
            {
                Destroy(gameObject);
            }
            else
            {
                #if UNITY_EDITOR
                try {
                    if (gameObject.IsPrefab())
                    {
                        Debug.Log("If you get a warning about 'Setting the parent of a transform..'" +
                            " it can be ignored.");
                    }

                    Undo.DestroyObjectImmediate(gameObject); //Thows unity warning but works
                    
                    //DestroyImmediate(gameObject, true); //Can't undo
                }
                catch (Exception exception)
                {
                    Debug.LogWarning(exception);
                }
                #endif
            }
        }

        private void RemoveComponent(Component component)
        {
            if (Application.isPlaying)
            {
                Destroy(component);
            }
            else
            {
                #if UNITY_EDITOR
                Undo.DestroyObjectImmediate(component);
                #endif
            }
        }

        public void CopyComponentsAndChildren(GameObject from, GameObject to, bool ignoreTransform=true, bool ignorePrefabLink=true)
        {
            bool copyEvenWhenEqual = true;

            if (from != null)
            {
                //Copy prefab children
                foreach (Transform child in from.transform)
                {
                    if (!dirtyReport.Matched(child.gameObject))
                    {
                        Transform newChild = Instantiate(child, to.transform, false);
                        newChild.name = child.name;

                        #if UNITY_EDITOR
                        Undo.RegisterCreatedObjectUndo(newChild.gameObject, "Prefab Link: Copy child GameObject");
                        #endif
                    }
                    else
                    {   
                        GameObject fromChild = child.gameObject;
                        GameObject toChild = dirtyReport.GetMatch(fromChild) as GameObject;
                        CopyComponentsAndChildren(fromChild, toChild, ignoreTransform, ignorePrefabLink);
                    }
                }
                
                List<Component> needsAdding = new List<Component> { };
                List<Component> needsCopying = new List<Component> { };
                foreach (Component component in from.GetComponents<Component>())
                {
                    bool ignore = false;
                    //Not using ignoreTransform and always ignoring no matter what. 
                    if ( /*ignoreTransform &&*/ component.GetType() == typeof(Transform) || 
                        ignorePrefabLink && component.GetType() == typeof(PrefabLink))
                    {
                        ignore = true;
                    }

                    if (!ignore)
                    {
                        if (!dirtyReport.Matched(component))
                        {
                            needsAdding.Add(component);
                        }
                        else
                        {
                            ValueEqualsReportMatch match = dirtyReport.GetMatchDetails(component);

                            if (match.Type == ValueEqualsReportMatchType.NAMES_EQUAL || copyEvenWhenEqual)
                            {
                                Component toComponent = match.Match as Component;
                                toComponent.CopyComponent(component);
                            }
                        }
                    }
                }

                while (needsAdding.Count > 0)
                {
                    CopyComponentFromList(needsAdding[0], needsAdding, to, ignoreTransform, ignorePrefabLink);
                }

                //List<Component> componentsToAdd = from.GetComponents<Component>()
                //    .Where((item) => !dirtyReport.Matched(item))
                //    .ToList();

                ////Copy prefab components
                //while (componentsToAdd.Count > 0)
                //{
                //    CopyComponentAndRequiredComponents(componentsToAdd[0], componentsToAdd, to, ignoreTransform, ignorePrefabLink);
                //}

                if (ExtensionMethods.ExtensionMethods.includeNames)
                {
                    if (to != null && from != null)
                    {
                        to.name = from.name;
                    }
                }
            }
        }

        public void CopyComponentFromList(Component component, List<Component> componentsToAdd, GameObject to, 
            bool copyRequiredComponents=true, bool ignoreTransform=true, bool ignorePrefabLink=false)
        {
            componentsToAdd.Remove(component);
            
            if (copyRequiredComponents)
            {
                List<Type> requireComponentTypes = component.RequiredComponents();

                foreach (Type type in requireComponentTypes)
                {
                    if (to.GetComponent(type) == null)
                    {
                        Component requiredComponent = componentsToAdd.Find((item) => item.GetType() == type);
                        CopyComponentFromList(requiredComponent, componentsToAdd, to, ignoreTransform);
                    }
                }
            }

            to.CopyComponent(component);
        }

        private Component CopyComponent(Component component, Component to)
        {
            return to.CopyComponent(component);
        }

        private Component CopyComponent(Component component, GameObject to)
        {
            if (to == null)
            {
                to = gameObject;
            }

            return to.CopyComponent(component);
        }

        private List<PrefabLink> DirectChildPrefabLinks(GameObject gameObject)
        {
            List<PrefabLink> directChildPrefabLinks = new List<PrefabLink> { };

            foreach (Transform child in gameObject.transform)
            {
                PrefabLink directChildPrefabLink = child.gameObject.GetComponent<PrefabLink>();
                bool isPrefabLink = directChildPrefabLink != null;

                if (isPrefabLink)
                {
                    directChildPrefabLinks.Add(directChildPrefabLink);
                }
                else
                {
                    directChildPrefabLinks.AddRange(DirectChildPrefabLinks(child.gameObject));
                }
            }

            return directChildPrefabLinks;
        }

        public float StartTime
        {
            get
            {
                return revertStartTime;
            }

            set
            {
                revertStartTime = value;
            }
        }

        public bool copySuccessful
        {
            get
            {
                return revertSuccessful;
            }

            set
            {
                revertSuccessful = value;
            }
        }

        public bool Dirty
        {
            get
            {
                return dirty;
            }

            set
            {
                dirty = value;
            }
        }

        public GameObject Target
        {
            get
            {
                return target;
            }

            set
            {
                target = value;
            }
        }

        public ValueEqualsReport DirtyReport
        {
            get { return dirtyReport; }
        }
    }
}
