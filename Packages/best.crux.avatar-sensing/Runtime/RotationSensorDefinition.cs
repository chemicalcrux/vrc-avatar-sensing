using Crux.AvatarSensing.Runtime.Data;
using Crux.Core.Runtime.Attributes;
using Crux.ProceduralController.Runtime.Models;
using UnityEngine;
using VRC.SDKBase;

namespace Crux.AvatarSensing.Runtime
{
    [HideIcon]
    public class RotationSensorDefinition : ComponentModel, IEditorOnly
    {
        [SerializeField, SerializeReference] internal RotationSensorData data = new RotationSensorDataV1();

        private void Reset()
        {
            data = new RotationSensorDataV1();
        }
    }
}