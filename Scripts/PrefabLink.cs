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
        public GameObject prefab;

        private List<Component> componentsToAdd;
        private List<Component> componentsToRemove;
        [HideInInspector] public float startTime;
        [HideInInspector] public bool revertSuccessful;

        public bool Revert(bool revertChildren, bool ignoreTopTransform)
        {
            revertSuccessful = false;

            if (prefab != gameObject && prefab != null)
            {
                RemoveComponentsAndChildren();
                CopyComponentsAndChildren(ignoreTopTransform);

                if (revertChildren)
                {
                    foreach (PrefabLink directChildprefabLink in DirectChildPrefabLinks(gameObject))
                    {
                        directChildprefabLink.Revert(revertChildren, false);
                    }
                }
            }

            revertSuccessful = true;

            return revertSuccessful;
        }

        public void RemoveComponentsAndChildren()
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
                RemoveComponentAndRequiredComponents(componentsToRemove[0]);
            }
        }

        public void RemoveComponentAndRequiredComponents(Component component)
        {
            //TODO check for cicular dependancies

            componentsToRemove.Remove(component);

            if (component != this && component != transform)
            {
                List<Component> requiredByComponents = component.RequiredByComponents(component.gameObject.GetComponents<Component>().ToList());

                foreach (Component requiredByComponent in requiredByComponents)
                {
                    RemoveComponentAndRequiredComponents(requiredByComponent);
                }

                RemoveComponent(component);
            }
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

        public void CopyComponentsAndChildren(bool ignoreTransform)
        {
            if (prefab != null)
            {
                componentsToAdd = prefab.GetComponents<Component>().ToList();

                //Copy prefab components
                while (componentsToAdd.Count > 0)
                {
                    CopyComponentAndRequiredComponents(componentsToAdd[0], ignoreTransform);
                }

                //Copy prefab children
                foreach (Transform child in prefab.transform)
                {
                    Transform newChild = Instantiate(child, transform, false);
                    newChild.name = child.name;

                    #if UNITY_EDITOR
                    Undo.RegisterCreatedObjectUndo(newChild.gameObject, "Prefab Link: Copy child GameObject");
                    #endif
                }

                gameObject.name = prefab.name;
            }
        }

        public void CopyComponentAndRequiredComponents(Component component, bool ignoreTransform)
        {
            //TODO check for cicular dependancies

            componentsToAdd.Remove(component);

            bool ignore = false;

            if (component.GetType() == typeof(Transform) /*&& ignoreTransform*/)
            {
                ignore = true;
            }

            if (component.GetType() == typeof(PrefabLink))
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

        private Component CopyComponent(Component component)
        {
            return gameObject.CopyComponent(component);
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
    }
}
