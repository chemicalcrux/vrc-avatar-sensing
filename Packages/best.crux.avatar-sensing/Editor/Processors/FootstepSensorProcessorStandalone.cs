using Crux.AvatarSensing.Runtime;
using Crux.AvatarSensing.Runtime.Data;
using Crux.ProceduralController.Editor;
using Crux.ProceduralController.Editor.Receivers;
using UnityEngine;
using VRC.SDKBase.Editor.BuildPipeline;

namespace Crux.AvatarSensing.Editor.Processors
{
    public class FootstepSensorProcessorStandalone : IVRCSDKPreprocessAvatarCallback
    {
        public int callbackOrder => -10010;

        public bool OnPreprocessAvatar(GameObject avatarGameObject)
        {
            foreach (var target in avatarGameObject.GetComponentsInChildren<FootstepSensorDefinition>())
            {
                if (!Process(avatarGameObject, target))
                    return false;
            }

            return true;
        }

        private bool Process(GameObject root, FootstepSensorDefinition definition)
        {
            if (!definition.data.TryUpgradeTo(out FootstepSensorDataV1 data))
                return false;

            if (!data.standalone)
                return true;

            var context = new Context
            {
                avatarRoot = root,
                targetObject = definition.gameObject,
                receiver = new FuryFullControllerReceiver(definition.gameObject)
            };

            foreach (var target in data.targets)
            {
                foreach (var evt in data.events)
                {
                    context.receiver.AddGlobalParameter(evt.GetParameter(data, target));
                }
                foreach (var state in data.states)
                {
                    context.receiver.AddGlobalParameter(state.GetParameter(data, target));
                }
                foreach (var value in data.values)
                {
                    context.receiver.AddGlobalParameter(value.GetParameter(data, target));
                }
            }

            return FootstepSensorProcessor.Process(context, definition);
        }
    }
}