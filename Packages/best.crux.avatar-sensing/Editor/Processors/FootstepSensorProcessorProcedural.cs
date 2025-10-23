using Crux.AvatarSensing.Runtime;
using Crux.AvatarSensing.Runtime.Data;
using Crux.ProceduralController.Editor;
using Crux.ProceduralController.Editor.Processors;
using JetBrains.Annotations;

namespace Crux.AvatarSensing.Editor.Processors
{
    [UsedImplicitly]
    public class FootstepSensorProcessorProcedural : Processor<FootstepSensorDefinition>
    {
        public override void Process(Context context)
        {
            if (!model.data.TryUpgradeTo(out FootstepSensorDataV1 data))
                return;

            if (data.standalone)
                return;

            FootstepSensorProcessor.Process(context, model);
        }
    }
}