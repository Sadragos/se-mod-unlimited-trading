using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text;
using System.Timers;
using Sandbox.Common;
using Sandbox.Common.ObjectBuilders;
using Sandbox.Common.ObjectBuilders.Definitions;
using Sandbox.Definitions;
using Sandbox.Game;
using Sandbox.Game.Entities;
using Sandbox.ModAPI;
using Sandbox.ModAPI.Ingame;
using VRage.Game;
using VRage.Game.Components;
using VRage.Game.ModAPI;
using VRage.ModAPI;
using UnlimitedTrading;

namespace UnlimitedTrading
{
    [MySessionComponentDescriptor(MyUpdateOrder.NoUpdate)]
    class NoMinPrice : MySessionComponentBase
    {
        public override void LoadData()
        {
            base.LoadData();
            var allDefs = MyDefinitionManager.Static.GetAllDefinitions();

            foreach (var componenet in allDefs.OfType<MyPhysicalItemDefinition>())
            {
                Logging.Instance.WriteLine("Update Price of " + componenet.DisplayNameText + " from " + componenet.MinimalPricePerUnit + " -> 1");
                componenet.MinimalPricePerUnit = 1;
            }

            //var compressed = MyDefinitionManager.Static.GetBlueprintClass("Compressed");

            //foreach (var componenet in allDefs.OfType<MyAssemblerDefinition>())
            //{
            //    if (!componenet.ToString().StartsWith("MyObjectBuilder_Assembler")) continue;
            //    Logging.Instance.WriteLine("Adding Compressed to Blueprints " + componenet.ToString());
            //    componenet.BlueprintClasses.Add(compressed);
            //    componenet.InputInventoryConstraint = null;
            //    componenet.OutputInventoryConstraint = null;
            //}
        }
    }
}
