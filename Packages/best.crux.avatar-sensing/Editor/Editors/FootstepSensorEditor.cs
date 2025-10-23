using Crux.AvatarSensing.Editor.Controls;
using Crux.AvatarSensing.Runtime;
using UnityEditor;
using UnityEditor.UIElements;
using UnityEngine.UIElements;

namespace Crux.AvatarSensing.Editor.Editors
{
    [CustomEditor(typeof(FootstepSensorDefinition))]
    public class FootstepSensorEditor : UnityEditor.Editor
    {
        public override VisualElement CreateInspectorGUI()
        {
            var target = (FootstepSensorDefinition)serializedObject.targetObject;
            
            var root = new VisualElement();
            var list = new FootstepSensorParameterList();
            
            root.Add(new PropertyField(serializedObject.FindProperty(nameof(FootstepSensorDefinition.data))));
            root.Add(list);

            list.Connect(target);

            return root;
        }
    }
}