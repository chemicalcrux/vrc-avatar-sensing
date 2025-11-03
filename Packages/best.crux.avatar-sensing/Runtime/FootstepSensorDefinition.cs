using Crux.AvatarSensing.Runtime.Data;
using Crux.Core.Runtime.Upgrades;
using Crux.ProceduralController.Runtime.Models;
using UnityEditor;
using UnityEngine;
using VRC.SDKBase;

namespace Crux.AvatarSensing.Runtime
{
    [UpgradableLatestVersion(1)]
    public class FootstepSensorDefinition : ComponentModel, IEditorOnly
    {
        [SerializeField, SerializeReference] internal FootstepSensorData data = new FootstepSensorDataV1();

        private void Reset()
        {
            data = new FootstepSensorDataV1();
        }

#if UNITY_EDITOR
        private void OnDrawGizmosSelected()
        {
            if (!data.TryUpgradeTo(out FootstepSensorDataV1 latest))
                return;

            if (latest.mode == FootstepSensorDataV1.Mode.Physbones)
                DrawPhysboneGizmos(latest);
            else if (latest.mode == FootstepSensorDataV1.Mode.Contacts)
                DrawContactGizmos(latest);
        }

        private void DrawPhysboneGizmos(FootstepSensorDataV1 latest)
        {
            if (latest.createFloorCollider)
            {
                Gizmos.color = new Color(0.5f, 0.5f, 0.5f, 0.4f);

                Gizmos.DrawCube(transform.root.position + Vector3.up * latest.floorColliderHeight,
                    new Vector3(1, 0, 1));

                if (latest.floorColliderHeightAdjustment)
                {
                    Gizmos.color = new Color(0.5f, 0.5f, 0.5f, 0.2f);

                    Gizmos.DrawCube(
                        transform.root.position +
                        Vector3.up * (latest.floorColliderHeight + latest.floorColliderAdjustRange),
                        new Vector3(1, 0, 1));
                    Gizmos.DrawCube(
                        transform.root.position +
                        Vector3.up * (latest.floorColliderHeight - latest.floorColliderAdjustRange),
                        new Vector3(1, 0, 1));
                }
            }

            Gizmos.color = Color.white;

            foreach (var target in latest.targets)
            {
                Handles.Label(target.transform.position, target.GetIdentifier(), style: new GUIStyle()
                {
                    normal = new GUIStyleState()
                    {
                        textColor = Color.white
                    }
                });
                Gizmos.DrawLine(target.transform.position, target.transform.position + Vector3.down * 0.3f);
            }
        }

        private void DrawContactGizmos(FootstepSensorDataV1 latest)
        {
            Gizmos.color = new Color(0.5f, 0.5f, 0.5f, 0.4f);

            Gizmos.DrawCube(transform.root.position + Vector3.up * latest.contactFloorHeight, new Vector3(1, 0, 1));

            if (latest.contactFloorHeightAdjustment)
            {
                Gizmos.color = new Color(0.5f, 0.5f, 0.5f, 0.2f);

                Gizmos.DrawCube(
                    transform.root.position +
                    Vector3.up * (latest.contactFloorHeight + latest.contactFloorHeightRange),
                    new Vector3(1, 0, 1));
                Gizmos.DrawCube(
                    transform.root.position +
                    Vector3.up * (latest.contactFloorHeight - latest.contactFloorHeightRange),
                    new Vector3(1, 0, 1));
            }

            Gizmos.color = Color.white;

            foreach (var target in latest.targets)
            {
                Handles.Label(target.transform.position, target.GetIdentifier(), style: new GUIStyle()
                {
                    normal = new GUIStyleState()
                    {
                        textColor = Color.white
                    }
                });
            }
        }
#endif
    }
}