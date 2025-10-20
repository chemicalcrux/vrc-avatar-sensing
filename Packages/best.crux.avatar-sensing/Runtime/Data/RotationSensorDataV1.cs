using System;
using Crux.Core.Runtime.Upgrades;
using UnityEngine;

namespace Crux.AvatarSensing.Runtime.Data
{
    [Serializable]
    [UpgradableVersion(1)]
    internal class RotationSensorDataV1 : RotationSensorData
    {
        public bool standalone = true;
        public Transform targetTransform;
        public string parameterName;

        public override RotationSensorData Upgrade()
        {
            return this;
        }
    }
}