using System.Collections.Generic;
using System.Linq;
using System.Text;
using Crux.AvatarSensing.Editor.ExtensionMethods;
using Crux.AvatarSensing.Runtime;
using Crux.AvatarSensing.Runtime.Data;
using UnityEngine.UIElements;

namespace Crux.AvatarSensing.Editor.Controls
{
    public class FootstepSensorParameterList : VisualElement
    {
        private FootstepSensorDefinition definition;
        private VisualElement holder;
        
        public FootstepSensorParameterList()
        {
            holder = new VisualElement();
            Add(holder);
        }

        public void Connect(FootstepSensorDefinition definition)
        {
            this.definition = definition;
            this.TrackPropertyByName("data", Refresh);
            Refresh();
        }

        void Refresh()
        {
            if (!definition.data.TryUpgradeTo(out FootstepSensorDataV1 data))
                return;

            holder.Clear();

            var text = new Label();
            holder.Add(text);
            StringBuilder builder = new StringBuilder();

            List<string> results = new();
            
            foreach (var target in data.targets)
            {
                results.AddRange(data.events.Select(evt => evt.GetParameter(data, target)));
                results.AddRange(data.states.Select(state => state.GetParameter(data, target)));
                results.AddRange(data.values.Select(value => value.GetParameter(data, target)));
            }

            results.Sort();

            builder.Append("Parameter count: " + results.Count);
            builder.Append("\n\n");
            
            foreach (var result in results)
            {
                builder.Append(result);
                builder.Append("\n");
            }

            text.text = builder.ToString();
        }
    }
}