using NGPlugin.Scripts;
using Sandbox.Game;
using SpaceEngineers.Game.Entities.Blocks;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace NexusSpawnScripts.Scripts
{
    public class ButtonPressedScript : ScriptAPI
    {

        public override void NexusButtonAction(MyButtonPanel buttonPanel, ulong SteamID, long PlayerID)
        {
            MyVisualScriptLogicProvider.ShowNotification("Button Pressed", 1000, "White", PlayerID);
            base.NexusButtonAction(buttonPanel, SteamID, PlayerID);
        }
    }
}
