using System;
using System.Collections.Generic;
using System.Linq;
using Crux.AvatarSensing.Runtime;
using Crux.AvatarSensing.Runtime.Data;
using Crux.ProceduralController.Editor;
using UnityEditor.Animations;
using UnityEngine;
using VRC.Dynamics;
using VRC.SDK3.Avatars.Components;
using VRC.SDK3.Avatars.ScriptableObjects;
using VRC.SDK3.Dynamics.Constraint.Components;
using VRC.SDK3.Dynamics.PhysBone.Components;
using VRC.SDKBase;

namespace Crux.AvatarSensing.Editor
{
    public static class FootstepSensorProcessor
    {
        public static bool Process(Context context, FootstepSensorDefinition definition)
        {
            if (!definition.data.TryUpgradeTo(out FootstepSensorDataV1 data))
                return false;

            var controller = new AnimatorController();
            var paramz = ScriptableObject.CreateInstance<VRCExpressionParameters>();
            var hierarchy = new MenuHierarchy();

            CreateSensors(context, data);
            AddParameters(controller, data);

            paramz.parameters = new[]
            {
                new VRCExpressionParameters.Parameter
                {
                    name = "Control/Active",
                    defaultValue = 1f,
                    networkSynced = true,
                    saved = false,
                    valueType = VRCExpressionParameters.ValueType.Bool
                }
            };

            foreach (var target in data.targets)
                controller.AddLayer(CreateEventLayer(context, data, target));

            controller.AddLayer(CreateValueLayer(context, data));

            context.receiver.AddController(controller);
            context.receiver.AddParameters(paramz);

            hierarchy.leaves.Add(new VRCExpressionsMenu.Control
            {
                name = "Active",
                parameter = new VRCExpressionsMenu.Control.Parameter
                {
                    name = "Control/Active"
                },
                type = VRCExpressionsMenu.Control.ControlType.Toggle
            });
            
            List<VRCExpressionsMenu> menus = new();
            hierarchy.Resolve(paramz, data.menuPrefix, menus);

            foreach (var menu in menus)
                context.receiver.AddMenu(menu, "");

            return true;
        }

        private static void CreateSensors(Context context, FootstepSensorDataV1 data)
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

                physbone.parameter = target.GetPhysboneParameter(data);

                physbone.version = VRCPhysBoneBase.Version.Version_1_1;

                physbone.pull = 1f;
                physbone.spring = 0f;
                physbone.allowGrabbing = VRCPhysBoneBase.AdvancedBool.False;

                physbone.radius = 0.03f;

                physbone.colliders.Add(data.floorCollider);

                physbone.endpointPosition = new Vector3(0, -0.3f, 0);
                physbone.isAnimated = true;
            }
        }

        private static void AddParameters(AnimatorController controller, FootstepSensorDataV1 data)
        {
            controller.AddParameter(data.enableParameter, AnimatorControllerParameterType.Bool);

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

                foreach (var stateData in data.events)
                {
                    controller.AddParameter(stateData.GetParameter(data, target), AnimatorControllerParameterType.Bool);
                }

                foreach (var valueData in data.values)
                {
                    controller.AddParameter(valueData.GetParameter(data, target), AnimatorControllerParameterType.Float);
                }
            }
        }

        private static AnimatorControllerLayer CreateEventLayer(Context context, FootstepSensorDataV1 data,
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

            var stateIdle = machine.AddState("Idle");
            var stateDud = machine.AddState("Dud");
            var stateFootDown = machine.AddState("FootDown");
            var stateFootDownWait = machine.AddState("FootDownWait");
            var stateFootUp = machine.AddState("FootUp");

            // Idle -> Dud
            {
                var transition = stateIdle.AddTransition(stateDud);

                transition.hasExitTime = false;
                transition.duration = 0f;

                transition.AddCondition(AnimatorConditionMode.IfNot, 0, data.enableParameter);
                transition.AddCondition(AnimatorConditionMode.Greater, 0.5f, target.GetInputParameter(data));
            }

            // Dud -> Idle
            {
                var transition = stateDud.AddTransition(stateIdle);

                transition.hasExitTime = false;
                transition.duration = 0f;

                transition.AddCondition(AnimatorConditionMode.Less, 0.5f, target.GetInputParameter(data));
            }

            // Idle -> FootDown
            {
                var transition = stateIdle.AddTransition(stateFootDown);

                transition.hasExitTime = false;
                transition.duration = 0f;

                transition.AddCondition(AnimatorConditionMode.If, 0, data.enableParameter);
                transition.AddCondition(AnimatorConditionMode.Greater, 0.5f, target.GetInputParameter(data));
            }

            // FootDown -> FootDownWait
            {
                var transition = stateFootDown.AddTransition(stateFootDownWait);

                transition.duration = 0f;

                if (data.eventHoldTime > 0)
                {
                    transition.hasExitTime = true;
                    transition.exitTime = 1;

                    stateFootDown.speed = 1f / data.eventHoldTime;
                }
                else
                {
                    transition.hasExitTime = false;

                    transition.AddCondition(AnimatorConditionMode.Greater, 0, "Constant/One");
                }
            }

            // FootDownWait -> FootUp
            {
                var transition = stateFootDownWait.AddTransition(stateFootUp);

                transition.hasExitTime = false;
                transition.duration = 0f;

                transition.AddCondition(AnimatorConditionMode.Less, 0.5f, target.GetInputParameter(data));
            }

            // FootDown -> FootUp
            {
                var transition = stateFootDown.AddTransition(stateFootUp);

                transition.hasExitTime = false;
                transition.duration = 0f;

                transition.AddCondition(AnimatorConditionMode.Less, 0.5f, target.GetInputParameter(data));
            }

            // FootDown -> FootDownWait
            {
                var transition = stateFootUp.AddTransition(stateIdle);

                transition.duration = 0f;

                if (data.eventHoldTime > 0)
                {
                    transition.hasExitTime = true;
                    transition.exitTime = 1;

                    stateFootDown.speed = 1f / data.eventHoldTime;
                }
                else
                {
                    transition.hasExitTime = false;

                    transition.AddCondition(AnimatorConditionMode.Greater, 0, "Constant/One");
                }
            }

            // FootUp -> FootDown
            {
                var transition = stateFootUp.AddTransition(stateFootDown);

                transition.hasExitTime = false;
                transition.duration = 0f;

                transition.AddCondition(AnimatorConditionMode.Greater, 0.5f, target.GetInputParameter(data));
            }

            VRCAvatarParameterDriver driverFootDown = null;
            VRCAvatarParameterDriver driverFootDownWait = null;
            VRCAvatarParameterDriver driverFootUp = null;
            VRCAvatarParameterDriver driverIdle = null;

            foreach (var evt in data.events.Where(evt =>
                         evt.eventKind == FootstepSensorDataV1.FootstepEvent.FootDown))
            {
                if (!driverFootDown)
                    driverFootDown = stateFootDown.AddStateMachineBehaviour<VRCAvatarParameterDriver>();
                if (!driverFootDownWait)
                    driverFootDownWait = stateFootDownWait.AddStateMachineBehaviour<VRCAvatarParameterDriver>();
                if (!driverFootUp)
                    driverFootUp = stateFootUp.AddStateMachineBehaviour<VRCAvatarParameterDriver>();

                driverFootDown.parameters.Add(new VRC_AvatarParameterDriver.Parameter
                {
                    name = evt.GetParameter(data, target),
                    type = VRC_AvatarParameterDriver.ChangeType.Set,
                    value = 1f
                });

                driverFootDownWait.parameters.Add(new VRC_AvatarParameterDriver.Parameter
                {
                    name = evt.GetParameter(data, target),
                    type = VRC_AvatarParameterDriver.ChangeType.Set,
                    value = 0f
                });

                driverFootUp.parameters.Add(new VRC_AvatarParameterDriver.Parameter
                {
                    name = evt.GetParameter(data, target),
                    type = VRC_AvatarParameterDriver.ChangeType.Set,
                    value = 0f
                });
            }

            foreach (var evt in data.events.Where(evt => evt.eventKind == FootstepSensorDataV1.FootstepEvent.FootUp))
            {
                if (!driverFootUp)
                    driverFootUp = stateFootUp.AddStateMachineBehaviour<VRCAvatarParameterDriver>();
                if (!driverIdle)
                    driverIdle = stateIdle.AddStateMachineBehaviour<VRCAvatarParameterDriver>();
                if (!driverFootDown)
                    driverFootDown = stateFootDown.AddStateMachineBehaviour<VRCAvatarParameterDriver>();

                driverFootUp.parameters.Add(new VRC_AvatarParameterDriver.Parameter
                {
                    name = evt.GetParameter(data, target),
                    type = VRC_AvatarParameterDriver.ChangeType.Set,
                    value = 1f
                });

                driverIdle.parameters.Add(new VRC_AvatarParameterDriver.Parameter
                {
                    name = evt.GetParameter(data, target),
                    type = VRC_AvatarParameterDriver.ChangeType.Set,
                    value = 0f
                });

                driverFootDown.parameters.Add(new VRC_AvatarParameterDriver.Parameter
                {
                    name = evt.GetParameter(data, target),
                    type = VRC_AvatarParameterDriver.ChangeType.Set,
                    value = 0f
                });
            }

            foreach (var state in data.states.Where(state =>
                         state.stateKind == FootstepSensorDataV1.FootstepState.FootDownHold))
            {
                if (!driverFootDown)
                    driverFootDown = stateFootDown.AddStateMachineBehaviour<VRCAvatarParameterDriver>();
                if (!driverFootUp)
                    driverFootUp = stateFootUp.AddStateMachineBehaviour<VRCAvatarParameterDriver>();

                driverFootDown.parameters.Add(new VRC_AvatarParameterDriver.Parameter
                {
                    name = state.GetParameter(data, target),
                    type = VRC_AvatarParameterDriver.ChangeType.Set,
                    value = 1f
                });

                driverFootUp.parameters.Add(new VRC_AvatarParameterDriver.Parameter
                {
                    name = state.GetParameter(data, target),
                    type = VRC_AvatarParameterDriver.ChangeType.Set,
                    value = 0f
                });
            }

            foreach (var state in data.states.Where(state =>
                         state.stateKind == FootstepSensorDataV1.FootstepState.FootUpHold))
            {
                if (!driverFootUp)
                    driverFootUp = stateFootUp.AddStateMachineBehaviour<VRCAvatarParameterDriver>();
                if (!driverFootDown)
                    driverFootDown = stateFootDown.AddStateMachineBehaviour<VRCAvatarParameterDriver>();

                driverFootUp.parameters.Add(new VRC_AvatarParameterDriver.Parameter
                {
                    name = state.GetParameter(data, target),
                    type = VRC_AvatarParameterDriver.ChangeType.Set,
                    value = 1f
                });

                driverFootDown.parameters.Add(new VRC_AvatarParameterDriver.Parameter
                {
                    name = state.GetParameter(data, target),
                    type = VRC_AvatarParameterDriver.ChangeType.Set,
                    value = 0f
                });
            }

            return layer;
        }

        private static AnimatorControllerLayer CreateValueLayer(Context context, FootstepSensorDataV1 data)
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
    }
}