using System;
using System.Collections.Generic;
using Crux.Core.Runtime;
using Crux.Core.Runtime.Attributes;
using Crux.Core.Runtime.Upgrades;
using UnityEditor;
using UnityEngine;
using VRC.SDK3.Dynamics.PhysBone.Components;

namespace Crux.AvatarSensing.Runtime.Data
{
    [Serializable]
    [UpgradableVersion(1)]
    internal class FootstepSensorDataV1 : FootstepSensorData
    {
        [Serializable]
        internal class FootstepTarget
        {
            public string identifier;
            public Transform transform;

            public string GetIdentifier()
            {
                if (string.IsNullOrEmpty(identifier))
                {
                    if (transform != null)
                    {
                        return transform.name;
                    }
                }
                else
                {
                    return identifier;
                }

                return "MISSING";
            }

            public string GetPhysboneParameter(FootstepSensorDataV1 data)
            {
                string result = "Input/Physbone/";

                result += GetIdentifier();
                
                return result;
            }

            public string GetInputParameter(FootstepSensorDataV1 data)
            {
                return GetPhysboneParameter(data) + "_Angle";
            }
        }

        internal enum FootstepEvent
        {
            FootDown,
            FootUp
        }

        [Serializable]
        internal class FootstepEventEntry
        {
            public FootstepEvent eventKind;
            public string parameterName;

            public string GetParameter(FootstepSensorDataV1 data, FootstepTarget target)
            {
                string result = data.outputPrefix;

                result += target.GetIdentifier();

                if (string.IsNullOrEmpty(parameterName))
                    result += "-" + eventKind;
                else
                    result += "-" + parameterName;

                return result;
            }
        }

        internal enum FootstepState
        {
            FootDownHold,
            FootUpHold,
        }

        [Serializable]
        internal class FootstepStateEntry
        {
            public FootstepState stateKind;
            public string parameterName;

            public string GetParameter(FootstepSensorDataV1 data, FootstepTarget target)
            {
                string result = data.outputPrefix;

                result += target.GetIdentifier();

                if (string.IsNullOrEmpty(parameterName))
                    result += "-" + stateKind;
                else
                    result += "-" + parameterName;

                return result;
            }
        }

        internal enum FootstepValue
        {
            GroundProximity
        }

        [Serializable]
        internal class FootstepValueEntry
        {
            public FootstepValue valueKind;
            public string parameterName;

            public string GetParameter(FootstepSensorDataV1 data, FootstepTarget target)
            {
                string result = data.outputPrefix;

                result += target.GetIdentifier();

                if (string.IsNullOrEmpty(parameterName))
                    result += "-" + valueKind;
                else
                    result += "-" + parameterName;

                return result;
            }
        }

        public bool standalone;

        [Space] public bool createMenu = true;
        
        [BeginRevealArea(nameof(createMenu), true)]
        public string menuPrefix = "Config/Footsteps/";
        [EndRevealArea]
        
        [Space]
        public string enableParameter = "Control/Active";

        public string outputPrefix = "Shared/Footsteps/";

        public VRCPhysBoneCollider floorCollider;

        public float eventHoldTime;

        public float margin = 0.05f;

        public DecoratedList<FootstepTarget> targets = new();
        public DecoratedList<FootstepEventEntry> events = new();
        public DecoratedList<FootstepStateEntry> states = new();
        public DecoratedList<FootstepValueEntry> values = new();

        public override FootstepSensorData Upgrade()
        {
            return this;
        }
    }
}