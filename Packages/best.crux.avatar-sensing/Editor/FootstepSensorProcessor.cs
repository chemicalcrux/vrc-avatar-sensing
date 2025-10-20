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

                foreach (var stateData in data.states)
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

            var stateDisabled = machine.AddState("Disabled");
            var stateUp = machine.AddState("Up");
            var stateUpHold = machine.AddState("UpHold");
            var stateDown = machine.AddState("Down");
            var stateDownHold = machine.AddState("DownHold");

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
                transition.AddCondition(AnimatorConditionMode.Greater, 0.5f, target.GetInputParameter(data));
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

                transition.AddCondition(AnimatorConditionMode.Less, 0.5f, target.GetInputParameter(data));
            }

            // Down -> Up
            {
                var transition = stateDown.AddTransition(stateUp);

                transition.name = "Raise Early";
                transition.hasExitTime = false;
                transition.duration = 0f;

                transition.AddCondition(AnimatorConditionMode.Less, 0.5f, target.GetInputParameter(data));
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

                transition.AddCondition(AnimatorConditionMode.Greater, 0.5f, target.GetInputParameter(data));
            }

            VRCAvatarParameterDriver driverDisabled = null;
            VRCAvatarParameterDriver driverDown = null;
            VRCAvatarParameterDriver driverDownWait = null;
            VRCAvatarParameterDriver driverUp = null;
            VRCAvatarParameterDriver driverUpHold = null;

            foreach (var evt in data.events.Where(evt =>
                         evt.eventKind == FootstepSensorDataV1.FootstepEvent.FootDown))
            {
                if (!driverDisabled)
                    driverDisabled = stateDisabled.AddStateMachineBehaviour<VRCAvatarParameterDriver>();
                if (!driverDown)
                    driverDown = stateDown.AddStateMachineBehaviour<VRCAvatarParameterDriver>();
                if (!driverDownWait)
                    driverDownWait = stateDownHold.AddStateMachineBehaviour<VRCAvatarParameterDriver>();
                if (!driverUp)
                    driverUp = stateUp.AddStateMachineBehaviour<VRCAvatarParameterDriver>();

                driverDisabled.parameters.Add(new VRC_AvatarParameterDriver.Parameter
                {
                    name = evt.GetParameter(data, target),
                    type = VRC_AvatarParameterDriver.ChangeType.Set,
                    value = 0f
                });
                
                driverDown.parameters.Add(new VRC_AvatarParameterDriver.Parameter
                {
                    name = evt.GetParameter(data, target),
                    type = VRC_AvatarParameterDriver.ChangeType.Set,
                    value = 1f
                });

                driverDownWait.parameters.Add(new VRC_AvatarParameterDriver.Parameter
                {
                    name = evt.GetParameter(data, target),
                    type = VRC_AvatarParameterDriver.ChangeType.Set,
                    value = 0f
                });

                driverUp.parameters.Add(new VRC_AvatarParameterDriver.Parameter
                {
                    name = evt.GetParameter(data, target),
                    type = VRC_AvatarParameterDriver.ChangeType.Set,
                    value = 0f
                });
            }

            foreach (var evt in data.events.Where(evt => evt.eventKind == FootstepSensorDataV1.FootstepEvent.FootUp))
            {
                if (!driverDisabled)
                    driverDisabled = stateDisabled.AddStateMachineBehaviour<VRCAvatarParameterDriver>();
                if (!driverUp)
                    driverUp = stateUp.AddStateMachineBehaviour<VRCAvatarParameterDriver>();
                if (!driverUpHold)
                    driverUpHold = stateUpHold.AddStateMachineBehaviour<VRCAvatarParameterDriver>();
                if (!driverDown)
                    driverDown = stateDown.AddStateMachineBehaviour<VRCAvatarParameterDriver>();

                driverDisabled.parameters.Add(new VRC_AvatarParameterDriver.Parameter
                {
                    name = evt.GetParameter(data, target),
                    type = VRC_AvatarParameterDriver.ChangeType.Set,
                    value = 0f
                });

                driverUp.parameters.Add(new VRC_AvatarParameterDriver.Parameter
                {
                    name = evt.GetParameter(data, target),
                    type = VRC_AvatarParameterDriver.ChangeType.Set,
                    value = 1f
                });

                driverUpHold.parameters.Add(new VRC_AvatarParameterDriver.Parameter
                {
                    name = evt.GetParameter(data, target),
                    type = VRC_AvatarParameterDriver.ChangeType.Set,
                    value = 0f
                });

                driverDown.parameters.Add(new VRC_AvatarParameterDriver.Parameter
                {
                    name = evt.GetParameter(data, target),
                    type = VRC_AvatarParameterDriver.ChangeType.Set,
                    value = 0f
                });
            }

            foreach (var state in data.states.Where(state =>
                         state.stateKind == FootstepSensorDataV1.FootstepState.FootDownHold))
            {
                if (!driverDisabled)
                    driverDisabled = stateDisabled.AddStateMachineBehaviour<VRCAvatarParameterDriver>();
                if (!driverDown)
                    driverDown = stateDown.AddStateMachineBehaviour<VRCAvatarParameterDriver>();
                if (!driverUp)
                    driverUp = stateUp.AddStateMachineBehaviour<VRCAvatarParameterDriver>();

                driverDisabled.parameters.Add(new VRC_AvatarParameterDriver.Parameter
                {
                    name = state.GetParameter(data, target),
                    type = VRC_AvatarParameterDriver.ChangeType.Set,
                    value = 0f
                });

                driverDown.parameters.Add(new VRC_AvatarParameterDriver.Parameter
                {
                    name = state.GetParameter(data, target),
                    type = VRC_AvatarParameterDriver.ChangeType.Set,
                    value = 1f
                });

                driverUp.parameters.Add(new VRC_AvatarParameterDriver.Parameter
                {
                    name = state.GetParameter(data, target),
                    type = VRC_AvatarParameterDriver.ChangeType.Set,
                    value = 0f
                });
            }

            foreach (var state in data.states.Where(state =>
                         state.stateKind == FootstepSensorDataV1.FootstepState.FootUpHold))
            {
                if (!driverDisabled)
                    driverDisabled = stateDisabled.AddStateMachineBehaviour<VRCAvatarParameterDriver>();
                if (!driverUp)
                    driverUp = stateUp.AddStateMachineBehaviour<VRCAvatarParameterDriver>();
                if (!driverDown)
                    driverDown = stateDown.AddStateMachineBehaviour<VRCAvatarParameterDriver>();

                driverDisabled.parameters.Add(new VRC_AvatarParameterDriver.Parameter
                {
                    name = state.GetParameter(data, target),
                    type = VRC_AvatarParameterDriver.ChangeType.Set,
                    value = 0f
                });

                driverUp.parameters.Add(new VRC_AvatarParameterDriver.Parameter
                {
                    name = state.GetParameter(data, target),
                    type = VRC_AvatarParameterDriver.ChangeType.Set,
                    value = 1f
                });

                driverDown.parameters.Add(new VRC_AvatarParameterDriver.Parameter
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