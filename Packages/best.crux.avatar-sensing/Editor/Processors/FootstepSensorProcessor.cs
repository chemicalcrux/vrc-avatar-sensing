using System;
using System.Collections.Generic;
using System.Linq;
using com.vrcfury.api;
using Crux.AvatarSensing.Runtime;
using Crux.AvatarSensing.Runtime.Data;
using Crux.ProceduralController.Editor;
using UnityEditor.Animations;
using UnityEngine;
using VRC.Dynamics;
using VRC.SDK3.Avatars.Components;
using VRC.SDK3.Avatars.ScriptableObjects;
using VRC.SDK3.Dynamics.Constraint.Components;
using VRC.SDK3.Dynamics.Contact.Components;
using VRC.SDK3.Dynamics.PhysBone.Components;
using VRC.SDKBase;

#pragma warning disable CS8524 // The switch expression does not handle some values of its input type (it is not exhaustive) involving an unnamed enum value.

namespace Crux.AvatarSensing.Editor.Processors
{
    public static class FootstepSensorProcessor
    {
        private class ProcessorState
        {
            public VRCPhysBoneCollider physboneFloor;
            public GameObject contactFloorPivot;
        }

        private const string HeightAdjustParameter = "Control/Floor Collider Height";

        public static bool Process(Context context, FootstepSensorDefinition definition)
        {
            if (!definition.data.TryUpgradeTo(out FootstepSensorDataV1 data))
                return false;

            var controller = new AnimatorController();
            var paramz = ScriptableObject.CreateInstance<VRCExpressionParameters>();
            var hierarchy = new MenuHierarchy();
            var state = new ProcessorState();

            List<VRCExpressionParameters.Parameter> paramzList = new List<VRCExpressionParameters.Parameter>();

            InitializeState(context, data, state);
            CreateSensors(context, data, state);
            AddParameters(controller, data, paramzList);

            paramz.parameters = paramzList.ToArray();

            foreach (var target in data.targets)
                controller.AddLayer(CreateEventLayer(data, target));

            switch (data.mode)
            {
                case FootstepSensorDataV1.Mode.Physbones:
                {
                    if (data.createFloorCollider && data.floorColliderHeightAdjustment)
                        controller.AddLayer(CreateHeightAdjustLayer(context, data, state));
                    break;
                }
                case FootstepSensorDataV1.Mode.Contacts:
                {
                    if (data.contactFloorHeightAdjustment)
                        controller.AddLayer(CreateHeightAdjustLayer(context, data, state));
                    break;
                }
            }

            controller.AddLayer(CreateValueLayer(data));

            context.receiver.AddController(controller);
            context.receiver.AddParameters(paramz);

            if (data.createMenu)
            {
                hierarchy.leaves.Add(new VRCExpressionsMenu.Control
                {
                    name = "Active",
                    parameter = new VRCExpressionsMenu.Control.Parameter
                    {
                        name = data.enableParameter
                    },
                    type = VRCExpressionsMenu.Control.ControlType.Toggle
                });

                bool heightAdjust = data.mode switch
                {
                    FootstepSensorDataV1.Mode.Physbones => data.createFloorCollider &&
                                                           data.floorColliderHeightAdjustment,
                    FootstepSensorDataV1.Mode.Contacts => data.contactFloorHeightAdjustment
                };

                if (heightAdjust)
                {
                    hierarchy.leaves.Add(new VRCExpressionsMenu.Control
                    {
                        name = "Floor Height",
                        type = VRCExpressionsMenu.Control.ControlType.RadialPuppet,
                        subParameters = new[]
                        {
                            new VRCExpressionsMenu.Control.Parameter
                            {
                                name = HeightAdjustParameter
                            }
                        }
                    });
                }

                List<VRCExpressionsMenu> menus = new();
                hierarchy.Resolve(paramz, "", menus);

                foreach (var menu in menus)
                    context.receiver.AddMenu(menu, data.menuPrefix);
            }

            return true;
        }

        private static void InitializeState(Context context, FootstepSensorDataV1 data, ProcessorState state)
        {
            if (data.createFloorCollider)
            {
                GameObject colliderHolder = new GameObject("Footstep Floor Collider");
                colliderHolder.transform.SetParent(context.avatarRoot.transform, false);
                state.physboneFloor = colliderHolder.AddComponent<VRCPhysBoneCollider>();
                state.physboneFloor.shapeType = VRCPhysBoneColliderBase.ShapeType.Plane;

                colliderHolder.transform.localPosition = Vector3.up * data.floorColliderHeight;
            }
            else
            {
                state.physboneFloor = data.floorCollider;
            }
        }

        private static void CreateSensors(Context context, FootstepSensorDataV1 data, ProcessorState state)
        {
            if (data.mode == FootstepSensorDataV1.Mode.Physbones)
                CreatePhysboneSensors(context, data, state);
            else if (data.mode == FootstepSensorDataV1.Mode.Contacts)
                CreateContactSensors(context, data, state);
        }

        private static void CreatePhysboneSensors(Context context, FootstepSensorDataV1 data, ProcessorState state)
        {
            foreach (var target in data.targets)
            {
                var holder = new GameObject("Footstep Holder - " + target.identifier);
                holder.transform.SetParent(context.targetObject.transform, false);

                var positionConstraint = holder.AddComponent<VRCPositionConstraint>();

                positionConstraint.Sources.Add(new VRCConstraintSource
                {
                    SourceTransform = target.transform,
                    Weight = 1f
                });

                positionConstraint.ZeroConstraint();

                var physbone = holder.transform.gameObject.AddComponent<VRCPhysBone>();

                physbone.parameter = target.GetBaseParameter(data);

                physbone.version = VRCPhysBoneBase.Version.Version_1_1;
                physbone.integrationType = VRCPhysBoneBase.IntegrationType.Advanced;

                physbone.pull = 1f;
                physbone.spring = 0f;
                physbone.stiffness = 0.3f;

                physbone.allowCollision = VRCPhysBoneBase.AdvancedBool.False;
                physbone.allowGrabbing = VRCPhysBoneBase.AdvancedBool.False;

                physbone.radius = 0.03f;

                physbone.colliders.Add(state.physboneFloor);

                physbone.immobileType = VRCPhysBoneBase.ImmobileType.AllMotion;
                physbone.immobile = 1f;

                physbone.endpointPosition = new Vector3(0.001f, -0.3f, 0.001f);
                physbone.isAnimated = true;
            }
        }

        private static void CreateContactSensors(Context context, FootstepSensorDataV1 data, ProcessorState state)
        {
            var floorRoot = new GameObject("Footstep Floor Root");
            floorRoot.transform.SetParent(context.targetObject.transform, false);

            var floorConstraint = floorRoot.AddComponent<VRCPositionConstraint>();

            floorConstraint.Sources.Add(new VRCConstraintSource
                {
                    SourceTransform = context.avatarRoot.transform,
                    Weight = 1f
                }
            );

            floorConstraint.ZeroConstraint();

            var floorPivot = new GameObject("Footstep Floor Pivot");
            floorPivot.transform.SetParent(floorRoot.transform, false);
            floorPivot.transform.localPosition = Vector3.up * data.contactFloorHeight;

            state.contactFloorPivot = floorPivot;

            foreach (var target in data.targets)
            {
                var senderHolder = new GameObject($"Footstep Sender - {target.GetIdentifier()}");

                senderHolder.transform.SetParent(context.targetObject.transform);

                var senderConstraint = senderHolder.AddComponent<VRCPositionConstraint>();

                senderConstraint.Sources.Add(new VRCConstraintSource
                {
                    SourceTransform = target.transform,
                    Weight = 1f
                });

                senderConstraint.ZeroConstraint();

                var sender = senderHolder.AddComponent<VRCContactSender>();

                string collisionTag = $"Footstep - {target.GetIdentifier()}";

                sender.collisionTags = new()
                {
                    collisionTag
                };

                sender.radius = 0.2f;
                sender.position = sender.radius / 2f * Vector3.up;

                var receiverHolder = new GameObject($"Footstep Receiver - {target.GetIdentifier()}");

                receiverHolder.transform.SetParent(floorPivot.transform, false);

                var receiverConstraint = receiverHolder.AddComponent<VRCPositionConstraint>();

                receiverConstraint.Sources.Add(new VRCConstraintSource
                {
                    SourceTransform = target.transform,
                    Weight = 1f
                });

                receiverConstraint.AffectsPositionY = false;

                receiverConstraint.ZeroConstraint();

                var receiver = receiverHolder.AddComponent<VRCContactReceiver>();

                receiver.collisionTags = new()
                {
                    collisionTag
                };

                receiver.parameter = target.GetBaseParameter(data);
                receiver.allowOthers = false;

                receiver.shapeType = ContactBase.ShapeType.Capsule;
                receiver.radius = 0.2f;
                receiver.height = 3f;
                receiver.position = Vector3.down * (receiver.height / 2f - receiver.radius);
                receiver.collisionValue = 1f;
                receiver.receiverType = ContactReceiver.ReceiverType.Proximity;
            }
        }

        private static void AddParameters(AnimatorController controller, FootstepSensorDataV1 data,
            List<VRCExpressionParameters.Parameter> paramzList)
        {
            paramzList.Add(new VRCExpressionParameters.Parameter
            {
                name = "Control/Active",
                defaultValue = 1f,
                networkSynced = true,
                saved = false,
                valueType = VRCExpressionParameters.ValueType.Bool
            });

            controller.AddParameter(data.enableParameter, AnimatorControllerParameterType.Bool);

            switch (data.mode)
            {
                case FootstepSensorDataV1.Mode.Physbones:
                {
                    if (data.createFloorCollider && data.floorColliderHeightAdjustment)
                    {
                        paramzList.Add(new VRCExpressionParameters.Parameter
                        {
                            name = HeightAdjustParameter,
                            defaultValue = 0.5f,
                            networkSynced = true,
                            saved = true,
                            valueType = VRCExpressionParameters.ValueType.Float
                        });

                        controller.AddParameter(HeightAdjustParameter, AnimatorControllerParameterType.Float);
                    }

                    break;
                }
                case FootstepSensorDataV1.Mode.Contacts:
                {
                    if (data.contactFloorHeightAdjustment)
                    {
                        paramzList.Add(new VRCExpressionParameters.Parameter
                        {
                            name = HeightAdjustParameter,
                            defaultValue = 0.5f,
                            networkSynced = true,
                            saved = true,
                            valueType = VRCExpressionParameters.ValueType.Float
                        });

                        controller.AddParameter(HeightAdjustParameter, AnimatorControllerParameterType.Float);
                    }

                    break;
                }
            }

            controller.AddParameter("Constant/One", AnimatorControllerParameterType.Float);

            var paramz = controller.parameters;
            paramz[^1].defaultFloat = 1f;
            controller.parameters = paramz;

            foreach (var target in data.targets)
            {
                controller.AddParameter(target.GetInputParameter(data), AnimatorControllerParameterType.Float);

                foreach (var eventData in data.events)
                {
                    controller.AddParameter(eventData.GetParameter(data, target), AnimatorControllerParameterType.Bool);
                }

                foreach (var stateData in data.states)
                {
                    controller.AddParameter(stateData.GetParameter(data, target), AnimatorControllerParameterType.Bool);
                }

                foreach (var valueData in data.values)
                {
                    controller.AddParameter(valueData.GetParameter(data, target),
                        AnimatorControllerParameterType.Float);
                }
            }

            if (data.createFloorCollider && data.floorColliderHeightAdjustment)
            {
                controller.AddParameter("Control/Collider Height", AnimatorControllerParameterType.Float);
            }
        }

        private static AnimatorControllerLayer CreateEventLayer(FootstepSensorDataV1 data,
            FootstepSensorDataV1.FootstepTarget target)
        {
            var machine = new AnimatorStateMachine
            {
                name = "Footstep Events"
            };

            var layer = new AnimatorControllerLayer
            {
                name = machine.name,
                defaultWeight = 1f,
                stateMachine = machine
            };

            var stateEntry1 = machine.AddState("Entry Delay 1");
            var stateEntry2 = machine.AddState("Entry Delay 2");
            var stateDisabled = machine.AddState("Disabled");
            var stateUp = machine.AddState("Up");
            var stateUpHold = machine.AddState("UpHold");
            var stateDown = machine.AddState("Down");
            var stateDownHold = machine.AddState("DownHold");

            // Entry 1 -> Entry 2
            {
                var transition = stateEntry1.AddTransition(stateEntry2);

                transition.name = "Wait One Frame";
                transition.hasExitTime = false;
                transition.duration = 0f;

                transition.AddCondition(AnimatorConditionMode.Greater, 0, "Constant/One");
            }
            // Entry 2 -> Disabled
            {
                var transition = stateEntry2.AddTransition(stateDisabled);

                transition.name = "Wait One Frame";
                transition.hasExitTime = false;
                transition.duration = 0f;

                transition.AddCondition(AnimatorConditionMode.Greater, 0, "Constant/One");
            }
            // Up -> Disabled
            {
                var transition = stateUp.AddTransition(stateDisabled);

                transition.name = "Deactivate";
                transition.hasExitTime = false;
                transition.duration = 0f;

                transition.AddCondition(AnimatorConditionMode.IfNot, 0, data.enableParameter);
            }

            // UpHold -> Disabled
            {
                var transition = stateUpHold.AddTransition(stateDisabled);

                transition.name = "Deactivate";
                transition.hasExitTime = false;
                transition.duration = 0f;

                transition.AddCondition(AnimatorConditionMode.IfNot, 0, data.enableParameter);
            }

            // Down -> Disabled
            {
                var transition = stateDown.AddTransition(stateDisabled);

                transition.name = "Deactivate";
                transition.hasExitTime = false;
                transition.duration = 0f;

                transition.AddCondition(AnimatorConditionMode.IfNot, 0, data.enableParameter);
            }

            // DownHold -> Disabled
            {
                var transition = stateDownHold.AddTransition(stateDisabled);

                transition.name = "Deactivate";
                transition.hasExitTime = false;
                transition.duration = 0f;

                transition.AddCondition(AnimatorConditionMode.IfNot, 0, data.enableParameter);
            }

            // Disabled -> UpHold
            {
                var transition = stateDisabled.AddTransition(stateUpHold);

                transition.name = "Activate Up";
                transition.hasExitTime = false;
                transition.duration = 0f;

                transition.AddCondition(AnimatorConditionMode.If, 0, data.enableParameter);
                transition.AddCondition(AnimatorConditionMode.Less, 0.5f, target.GetInputParameter(data));
            }

            // Disabled -> DownHold
            {
                var transition = stateDisabled.AddTransition(stateDownHold);

                transition.name = "Activate Down";
                transition.hasExitTime = false;
                transition.duration = 0f;

                transition.AddCondition(AnimatorConditionMode.If, 0, data.enableParameter);
                transition.AddCondition(AnimatorConditionMode.Greater, 0.5f, target.GetInputParameter(data));
            }

            // UpHold -> Down
            {
                var transition = stateUpHold.AddTransition(stateDown);

                transition.name = "Step";
                transition.hasExitTime = false;
                transition.duration = 0f;

                transition.AddCondition(AnimatorConditionMode.If, 0, data.enableParameter);
                transition.AddCondition(AnimatorConditionMode.Greater, 0.5f + data.margin,
                    target.GetInputParameter(data));
            }

            // Down -> DownHold
            {
                var transition = stateDown.AddTransition(stateDownHold);

                transition.name = "Hold";
                transition.duration = 0f;

                if (data.eventHoldTime > 0)
                {
                    transition.hasExitTime = true;
                    transition.exitTime = 1;

                    stateDown.speed = 1f / data.eventHoldTime;
                }
                else
                {
                    transition.hasExitTime = false;

                    transition.AddCondition(AnimatorConditionMode.Greater, 0, "Constant/One");
                }
            }

            // DownHold -> Up
            {
                var transition = stateDownHold.AddTransition(stateUp);

                transition.name = "Raise";
                transition.hasExitTime = false;
                transition.duration = 0f;

                transition.AddCondition(AnimatorConditionMode.Less, 0.5f - data.margin, target.GetInputParameter(data));
            }

            // Down -> Up
            {
                var transition = stateDown.AddTransition(stateUp);

                transition.name = "Raise Early";
                transition.hasExitTime = false;
                transition.duration = 0f;

                transition.AddCondition(AnimatorConditionMode.Less, 0.5f - data.margin, target.GetInputParameter(data));
            }

            // Up -> UpHold
            {
                var transition = stateUp.AddTransition(stateUpHold);

                transition.name = "Hold";
                transition.duration = 0f;

                if (data.eventHoldTime > 0)
                {
                    transition.hasExitTime = true;
                    transition.exitTime = 1;

                    stateDown.speed = 1f / data.eventHoldTime;
                }
                else
                {
                    transition.hasExitTime = false;

                    transition.AddCondition(AnimatorConditionMode.Greater, 0, "Constant/One");
                }
            }

            // Up -> Down
            {
                var transition = stateUp.AddTransition(stateDown);

                transition.name = "Step Early";
                transition.hasExitTime = false;
                transition.duration = 0f;

                transition.AddCondition(AnimatorConditionMode.Greater, 0.5f + data.margin,
                    target.GetInputParameter(data));
            }

            VRCAvatarParameterDriver driverDisabled =
                stateDisabled.AddStateMachineBehaviour<VRCAvatarParameterDriver>();
            VRCAvatarParameterDriver driverDown = stateDown.AddStateMachineBehaviour<VRCAvatarParameterDriver>();
            VRCAvatarParameterDriver driverDownHold =
                stateDownHold.AddStateMachineBehaviour<VRCAvatarParameterDriver>();
            VRCAvatarParameterDriver driverUp = stateUp.AddStateMachineBehaviour<VRCAvatarParameterDriver>();
            VRCAvatarParameterDriver driverUpHold = stateUpHold.AddStateMachineBehaviour<VRCAvatarParameterDriver>();

            foreach (var evt in data.events.Where(evt =>
                         evt.eventKind == FootstepSensorDataV1.FootstepEvent.FootDown))
            {
                string param = evt.GetParameter(data, target);

                UpdateDriver(driverDisabled, param, 0);
                UpdateDriver(driverDown, param, 1);
                UpdateDriver(driverDownHold, param, 0);
                UpdateDriver(driverUp, param, 0);
            }

            foreach (var evt in data.events.Where(evt => evt.eventKind == FootstepSensorDataV1.FootstepEvent.FootUp))
            {
                string param = evt.GetParameter(data, target);

                UpdateDriver(driverDisabled, param, 0);
                UpdateDriver(driverUp, param, 1);
                UpdateDriver(driverUpHold, param, 0);
                UpdateDriver(driverDown, param, 0);
            }

            foreach (var state in data.states.Where(state =>
                         state.stateKind == FootstepSensorDataV1.FootstepState.FootDownHold))
            {
                string param = state.GetParameter(data, target);

                UpdateDriver(driverDisabled, param, 0);
                UpdateDriver(driverDown, param, 1);
                UpdateDriver(driverDownHold, param, 1);
                UpdateDriver(driverUp, param, 0);
            }

            foreach (var state in data.states.Where(state =>
                         state.stateKind == FootstepSensorDataV1.FootstepState.FootUpHold))
            {
                string param = state.GetParameter(data, target);

                UpdateDriver(driverDisabled, param, 0);
                UpdateDriver(driverUp, param, 1);
                UpdateDriver(driverUpHold, param, 1);
                UpdateDriver(driverDown, param, 0);
            }

            return layer;
        }

        private static AnimatorControllerLayer CreateValueLayer(FootstepSensorDataV1 data)
        {
            var machine = new AnimatorStateMachine
            {
                name = "Footstep Values"
            };

            var layer = new AnimatorControllerLayer
            {
                name = machine.name,
                defaultWeight = 1f,
                stateMachine = machine
            };

            var state = machine.AddState("Blend State");

            var root = new BlendTree
            {
                name = "Root",
                blendType = BlendTreeType.Direct
            };

            state.motion = root;

            foreach (var target in data.targets)
            {
                var targetRoot = new BlendTree
                {
                    name = target.identifier + " Root",
                    blendType = BlendTreeType.Direct
                };

                foreach (var value in data.values)
                {
                    switch (value.valueKind)
                    {
                        case FootstepSensorDataV1.FootstepValue.GroundProximity:
                        {
                            var tree = new BlendTree
                            {
                                name = value.valueKind.ToString(),
                                blendType = BlendTreeType.Simple1D,
                                blendParameter = target.GetInputParameter(data),
                                useAutomaticThresholds = false
                            };

                            tree.AddChild(AnimatorMath.GetClip(value.GetParameter(data, target), 0));
                            tree.AddChild(AnimatorMath.GetClip(value.GetParameter(data, target), 1));

                            var children = tree.children;

                            children[0].threshold = 0;
                            children[1].threshold = 0.5f;

                            tree.children = children;

                            targetRoot.AddChild(tree);

                            break;
                        }
                        default:
                            throw new ArgumentOutOfRangeException();
                    }
                }

                if (targetRoot.children.Length > 0)
                {
                    AnimatorMath.SetOneParams(targetRoot);
                    root.AddChild(targetRoot);
                    Debug.Log(targetRoot.children[0].directBlendParameter);
                }
            }

            AnimatorMath.SetOneParams(root);
            return layer;
        }

        private static AnimatorControllerLayer CreateHeightAdjustLayer(Context context, FootstepSensorDataV1 data,
            ProcessorState processorState)
        {
            var machine = new AnimatorStateMachine
            {
                name = "Height Adjust"
            };

            var layer = new AnimatorControllerLayer
            {
                name = machine.name,
                defaultWeight = 1f,
                stateMachine = machine
            };

            var state = machine.AddState("Blend");

            var root = new BlendTree
            {
                name = "Root",
                blendParameter = HeightAdjustParameter,
                blendType = BlendTreeType.Simple1D
            };

            Transform target;
            float center;
            float lower;
            float upper;

            if (data.mode == FootstepSensorDataV1.Mode.Physbones)
            {
                target = processorState.physboneFloor.transform;
                center = data.floorColliderHeight;
                lower = center - data.floorColliderAdjustRange;
                upper = center + data.floorColliderAdjustRange;
            }
            else if (data.mode == FootstepSensorDataV1.Mode.Contacts)
            {
                target = processorState.contactFloorPivot.transform;
                center = data.contactFloorHeight;
                lower = center - data.contactFloorHeightRange;
                upper = center + data.contactFloorHeightRange;
            }
            else
            {
                throw new ArgumentOutOfRangeException("Invalid mode: " + data.mode);
            }

            string path = GetPath(context.avatarRoot.transform, target);
            Type type = typeof(Transform);
            string property = "m_LocalPosition.y";

            AnimationCurve lowerCurve =
                AnimationCurve.Constant(0, 1, lower);

            var lowerClip = new AnimationClip
            {
                name = "Height Adjust - Low"
            };

            lowerClip.SetCurve(path, type, property, lowerCurve);

            root.AddChild(lowerClip);

            AnimationCurve upperCurve =
                AnimationCurve.Constant(0, 1, upper);

            var upperClip = new AnimationClip
            {
                name = "Height Adjust - High"
            };

            upperClip.SetCurve(path, type, property, upperCurve);

            root.AddChild(upperClip);

            state.motion = root;

            return layer;
        }

        private static void UpdateDriver(VRCAvatarParameterDriver driver, string parameter,
            float value)
        {
            driver.parameters.Add(new VRC_AvatarParameterDriver.Parameter
            {
                name = parameter,
                type = VRC_AvatarParameterDriver.ChangeType.Set,
                value = value
            });
        }

        private static string GetPath(Transform root, Transform target)
        {
            string path = target.name;
            target = target.parent;

            while (target != root)
            {
                path = target.name + "/" + path;
                target = target.parent;
            }

            return path;
        }
    }
}