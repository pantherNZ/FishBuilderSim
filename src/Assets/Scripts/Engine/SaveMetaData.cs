using System;
using Newtonsoft.Json;
using Runtime.Game;

namespace Save
{
    public partial class SaveMetaData : JSONSave
    {
        [JsonProperty] public string gameName;
        [JsonProperty] public DateTime lastPlayed;
        [JsonProperty] public int depths;
        [JsonProperty] public int saveIdx;

        public SaveMetaData(string path) : base(path)
        {
        }

        public override void Save()
        {
            lastPlayed = DateTime.Now;

            var localPlayer = GlobalConstantsHandler.RuntimeConstants.localPlayer;
            if (localPlayer != null)
            {
                //depth = localPlayer
            }

            base.Save();
        }
    }
}
