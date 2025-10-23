using Crux.AvatarSensing.Runtime;
using Crux.AvatarSensing.Runtime.Data;
using Crux.Core.Editor;
using Crux.ProceduralController.Editor;
using UnityEngine;
using VRC.Dynamics;
using VRC.SDK3.Dynamics.Constraint.Components;
using VRC.SDK3.Dynamics.PhysBone.Components;

namespace Crux.AvatarSensing.Editor.Processors
{
    public static class RotationSensorProcessor
    {
        public static bool Process(Context context, RotationSensorDefinition definition)
        {
            if (!definition.data.TryUpgradeTo(out RotationSensorDataV1 data))
                return false;
            
            var holder = CreateHolder(context.targetObject, context.avatarRoot, definition);
            
            var rotator = new GameObject(definition.name + " Rotator");
            rotator.transform.SetParent(holder.transform);
            rotator.transform.localPosition = Vector3.zero;
            rotator.transform.localRotation = Quaternion.identity;
            rotator.transform.localScale = Vector3.one;

            var sensor = new GameObject(definition.name + " Sensor");
            sensor.transform.SetParent(rotator.transform);
            sensor.transform.localPosition = Vector3.zero;
            sensor.transform.localRotation = Quaternion.identity;
            sensor.transform.localScale = Vector3.one;

            var physbone = sensor.AddComponent<VRCPhysBone>();
            physbone.version = VRCPhysBoneBase.Version.Version_1_1;

            physbone.immobile = 1;
            physbone.immobileType = VRCPhysBoneBase.ImmobileType.World;
            physbone.isAnimated = true;

            physbone.endpointPosition = Vector3.one;
            physbone.parameter = data.parameterName;

            Transform target = data.targetTransform == null ? definition.transform : data.targetTransform;

            var constraint = rotator.AddComponent<VRCRotationConstraint>();

            constraint.SolveInLocalSpace = true;
            constraint.Sources.Add(new VRCConstraintSource()
            {
                SourceTransform = target,
                Weight = 1f
            });

            constraint.ZeroConstraint();
            
            return true;
        }

        private static GameObject CreateHolder(GameObject parent, GameObject root, RotationSensorDefinition definition)
        {
            var holder = new GameObject(definition.name + " Sensor Holder");
            holder.transform.SetParent(parent.transform);
            holder.transform.localPosition = Vector3.zero;
            holder.transform.localRotation = Quaternion.identity;
            holder.transform.localScale = Vector3.one;

            var origin =
                AssetReference.ParseAndLoad<GameObject>("50456884b6dc644298cde2d62b600af9,4817090454563823606");

            var rotationConstraint = holder.AddComponent<VRCRotationConstraint>();
            rotationConstraint.Sources.Add(new VRCConstraintSource
            {
                SourceTransform = origin.transform,
                Weight = 1f
            });
            
            rotationConstraint.ZeroConstraint();
            
            var positionConstraint = holder.AddComponent<VRCPositionConstraint>();
            positionConstraint.Sources.Add(new VRCConstraintSource
            {
                SourceTransform = root.transform,
                Weight = 1f
            });
            
            positionConstraint.ZeroConstraint();

            return holder;
        }
    }
}