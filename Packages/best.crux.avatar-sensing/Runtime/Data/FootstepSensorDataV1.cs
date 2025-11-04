using System;
using Crux.Core.Runtime;
using Crux.Core.Runtime.Attributes;
using Crux.Core.Runtime.Upgrades;
using UnityEngine;
using VRC.SDK3.Dynamics.PhysBone.Components;
#pragma warning disable CS8524 // The switch expression does not handle some values of its input type (it is not exhaustive) involving an unnamed enum value.

namespace Crux.AvatarSensing.Runtime.Data
{
    [Serializable]
    [UpgradableVersion(1)]
    internal class FootstepSensorDataV1 : FootstepSensorData
    {
        internal enum Mode
        {
            Physbones,
            Contacts
        }
        
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

            public string GetBaseParameter(FootstepSensorDataV1 data)
            {
                string result = "Input/Sensor/";

                result += GetIdentifier();

                return result;
            }

            public string GetInputParameter(FootstepSensorDataV1 data)
            {
                return data.mode switch
                {
                    Mode.Physbones => GetBaseParameter(data) + "_Angle",
                    Mode.Contacts => GetBaseParameter(data)
                };
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

        [TooltipInline("Disable this if you are using this sensor as part of a Procedural Controller")]
        public bool standalone = true;

        public bool createMenu = true;

        [BeginRevealArea(nameof(createMenu), true)]
        public string menuPrefix = "Config/Footsteps/";

        [EndRevealArea] public string enableParameter = "Control/Active";

        public string outputPrefix = "Shared/Footsteps/";

        public Mode mode;
        
        [BeginEnumRevealArea(nameof(mode), typeof(Mode), BeginEnumRevealAreaAttribute.EnumFlagKind.Off, Mode.Physbones)]
        [TooltipInline("You can use your own floor collider, or you have the sensor generate one for you.")]
        public bool createFloorCollider;

        [BeginRevealArea(nameof(createFloorCollider), false)]
        public VRCPhysBoneCollider floorCollider;

        [TooltipInline("How far above the origin of your avatar the collider will be.")]
        [EndRevealArea]
        [BeginRevealArea(nameof(createFloorCollider), true)]
        public float floorColliderHeight;

        [TooltipInline("Adds a control to your menu to move the floor up and down.")]
        public bool floorColliderHeightAdjustment;

        [BeginRevealArea(nameof(floorColliderHeightAdjustment), true)] [Range(0, 1)]
        public float floorColliderAdjustRange = 0.1f;

        [EndRevealArea]
        [EndRevealArea]
        [EndRevealArea]
        [BeginEnumRevealArea(nameof(mode), typeof(Mode), BeginEnumRevealAreaAttribute.EnumFlagKind.Off, Mode.Contacts)]

        [TooltipInline("The contact receivers will be placed at this height.")]
        public float contactFloorHeight;

        [TooltipInline("Adds a control to your menu to move the floor up and down.")]
        public bool contactFloorHeightAdjustment;

        public float contactFloorHeightRange;
        
        [EndRevealArea]
        [TooltipInline(
            "How long the foot-up and foot-down events will stay true. If set to zero, they will stay true for exactly one frame")]
        public float eventHoldTime;

        [TooltipInline(
            "How much the physbone angle or contact overlap has to go above or below the threshold before going from 'foot down' to 'foot up' or vice-versa. " +
            "This helps to prevent 'flapping', where the sensor is wiggling back and forth across the threshold.")]
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