using TP.ExtensionMethods;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using System.Reflection;
using System;
using System.Linq;

#if UNITY_EDITOR
using UnityEditor;
#endif

namespace TP.Greenfab
{

    [Serializable]
    public class PrefabLink : MonoBehaviour
    {
        public GameObject target;

        private List<Component> componentsToAdd;
        private List<Component> componentsToRemove;
        private bool revertSuccessful;
        private bool dirty;

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

                equals = target == otherPrefabLink.target;
            }

            return equals;
        }

        public void UpdateDirty()
        {
            Dirty = false;

            if (gameObject != null && target != null)
            {
                Dirty = !gameObject.ValueEquals(target);
            }
        }

        public bool Revert(bool revertChildren=true, bool ignoreTopTransform=true, bool ignorePrefabLink=true)
        {
            return CopyFrom(false, revertChildren, ignoreTopTransform, true);
        }

        public bool Apply(bool applyChildren=true, bool ignoreTopTransform=true, bool ignorePrefabLink=true)
        {
            PrefabLink targetPrefabLink = target.GetComponent<PrefabLink>();
            if (targetPrefabLink == null)
            {
                targetPrefabLink = target.AddComponent<PrefabLink>();
            }
            targetPrefabLink.target = gameObject;

            bool revertSuccessful = targetPrefabLink.CopyFrom(applyChildren, false, ignoreTopTransform, true);
            
            targetPrefabLink.target = target;

            return revertSuccessful;
        }

        public bool CopyFrom(bool applyChildren=false, bool revertChildren=true, bool ignoreTopTransform=true, bool ignorePrefabLink=true)
        {
            return CopyFrom(target, applyChildren, revertChildren, ignoreTopTransform, true);
        }

        public bool CopyFrom(GameObject from, bool applyChildren=false, bool revertChildren=true, bool ignoreTopTransform=true, bool ignorePrefabLink=true)
        {
            RevertSuccessful = false;

            if (from != gameObject && from != null)
            {
                if (applyChildren)
                {
                    foreach (PrefabLink directChildprefabLink in DirectChildPrefabLinks(gameObject))
                    {
                        directChildprefabLink.Apply(revertChildren, false, ignorePrefabLink);
                    }
                }

                RemoveComponentsAndChildren(ignoreTopTransform, ignorePrefabLink);
                CopyComponentsAndChildren(ignoreTopTransform, ignorePrefabLink);

                if (revertChildren)
                {
                    foreach (PrefabLink directChildprefabLink in DirectChildPrefabLinks(gameObject))
                    {
                        directChildprefabLink.Revert(revertChildren, false, ignorePrefabLink);
                    }
                }
            }

            RevertSuccessful = true;

            return RevertSuccessful;
        }

        public bool Revert(GameObject from, bool revertChildren=true, bool ignoreTopTransform=true, bool ignorePrefabLink=true)
        {
            RevertSuccessful = false;

            if (from != gameObject && from != null)
            {
                RemoveComponentsAndChildren(ignoreTopTransform, ignorePrefabLink);
                CopyComponentsAndChildren(ignoreTopTransform, ignorePrefabLink);

                if (revertChildren)
                {
                    foreach (PrefabLink directChildprefabLink in DirectChildPrefabLinks(gameObject))
                    {
                        directChildprefabLink.Revert(revertChildren, false, ignorePrefabLink);
                    }
                }
            }

            RevertSuccessful = true;

            return RevertSuccessful;
        }

        public void RemoveComponentsAndChildren(bool ignoreTopTransform=true, bool ignorePrefabLink=true)
        {
            //Remove children
            for (int i = transform.childCount - 1; i >= 0; i--)
            {
                RemoveGameObject(transform.GetChild(i).gameObject);
            }

            //Remove components
            componentsToRemove = gameObject.GetComponents<Component>().ToList();

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
                    RemoveComponentAndRequiredComponents(component);
                }
            }
        }

        public void RemoveComponentAndRequiredComponents(Component component)
        {
            //TODO check for cicular dependancies

            componentsToRemove.Remove(component);
            
            List<Component> requiredByComponents = component.RequiredByComponents(component.gameObject.GetComponents<Component>().ToList());

            foreach (Component requiredByComponent in requiredByComponents)
            {
                RemoveComponentAndRequiredComponents(requiredByComponent);
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
                Undo.DestroyObjectImmediate(gameObject);
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

        public void CopyComponentsAndChildren(bool ignoreTransform=true, bool ignorePrefabLink=true)
        {
            if (target != null)
            {
                componentsToAdd = target.GetComponents<Component>().ToList();

                //Copy prefab components
                while (componentsToAdd.Count > 0)
                {
                    CopyComponentAndRequiredComponents(componentsToAdd[0], ignoreTransform, ignorePrefabLink);
                }

                //Copy prefab children
                foreach (Transform child in target.transform)
                {
                    Transform newChild = Instantiate(child, transform, false);
                    newChild.name = child.name;

                    #if UNITY_EDITOR
                    Undo.RegisterCreatedObjectUndo(newChild.gameObject, "Prefab Link: Copy child GameObject");
                    #endif
                }

                if (ExtensionMethods.ExtensionMethods.includeNames)
                {
                    gameObject.name = target.name;
                }
            }
        }

        public void CopyComponentAndRequiredComponents(Component component, bool ignoreTransform=true, bool ignorePrefabLink=true)
        {
            //TODO check for cicular dependancies

            componentsToAdd.Remove(component);

            bool ignore = false;

            if ( /*ignoreTransform &&*/ component.GetType() == typeof(Transform) || 
                ignorePrefabLink && component.GetType() == typeof(PrefabLink))
            {
                ignore = true;
            }

            if (ignorePrefabLink && component.GetType() == typeof(PrefabLink))
            {
                ignore = true;
            }

            if (!ignore)
            {
                List<Type> requireComponentTypes = component.RequiredComponents();

                foreach (Type type in requireComponentTypes)
                {
                    if (gameObject.GetComponent(type) == null)
                    {
                        Component requiredComponent = componentsToAdd.Find((item) => item.GetType() == type);
                        CopyComponentAndRequiredComponents(requiredComponent, ignoreTransform);
                    }
                }

                CopyComponent(component);
            }
        }

        private Component CopyComponent(Component component, GameObject copyTo = null)
        {
            if (copyTo == null)
            {
                copyTo = gameObject;
            }

            return copyTo.CopyComponent(component);
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

        public bool RevertSuccessful
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
    }
}
