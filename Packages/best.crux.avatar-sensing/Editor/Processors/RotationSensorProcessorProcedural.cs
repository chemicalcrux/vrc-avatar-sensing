using Crux.AvatarSensing.Runtime;
using Crux.AvatarSensing.Runtime.Data;
using Crux.ProceduralController.Editor;
using Crux.ProceduralController.Editor.Processors;
using JetBrains.Annotations;

namespace Crux.AvatarSensing.Editor.Processors
{
    [UsedImplicitly]
    public class RotationSensorProcessorProcedural : Processor<RotationSensorDefinition>
    {
        public override void Process(Context context)
        {
            if (!model.data.TryUpgradeTo(out RotationSensorDataV1 data))
                return;

            if (data.standalone)
                return;

            RotationSensorProcessor.Process(context, model);
        }
    }
}