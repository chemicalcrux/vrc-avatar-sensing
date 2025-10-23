using System;
using System.Reflection;
using UnityEditor;
using UnityEditor.UIElements;
using UnityEngine;
using UnityEngine.UIElements;

namespace Crux.AvatarSensing.Editor.ExtensionMethods
{
    public static class VisualElementExtensionMethods
    {
        /// <summary>
        /// This is somewhat misleading: it actually tracks the entire SerializedObject.
        /// This is necessary because tracking a single property will *not* alert us to
        /// all changes (notably, changing a child array does nothing).
        /// <br />
        /// However, I do not see a clear way to access the SerializedObject that we're
        /// binding to, so the roundabout process is:
        /// <ul>
        /// <li>Add a PropertyField that binds to a specific property</li>
        /// <li>Wait one frame, then extract the SerializedProperty from the PropertyField</li>
        /// <li>Access the SerializedObject from the SerializedProperty</li>
        /// <li>Watch for changes to this object</li>
        /// </ul>
        /// </summary>
        /// <param name="element">The element that will receive the PropertyField</param>
        /// <param name="propertyName">Any valid property name</param>
        /// <param name="callback">The method to invoke when the SerializedObject changes</param>
        public static void TrackPropertyByName(this VisualElement element, string propertyName, Action callback)
        {
            
            var propertyField = new PropertyField()
            {
                bindingPath = propertyName,
                style =
                {
                    display = DisplayStyle.None
                }
            };

            element.Add(propertyField);
            
            propertyField.schedule.Execute(_ =>
            {
                FieldInfo fieldInfo = propertyField.GetType().GetField("m_SerializedProperty",
                    BindingFlags.NonPublic | BindingFlags.Instance);

                if (fieldInfo == null)
                {
                    Debug.LogWarning("This shouldn't happen...");
                    return;
                }

                var property = (SerializedProperty)fieldInfo.GetValue(propertyField);

                if (property != null)
                    element.TrackSerializedObjectValue(property.serializedObject, _ => callback());
                else
                    Debug.LogWarning("Couldn't find the property.");
            });
        }
    }
}