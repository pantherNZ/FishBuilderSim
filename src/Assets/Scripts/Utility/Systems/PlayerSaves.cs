using System.Collections.Generic;
using Newtonsoft.Json;

namespace Save
{
    public class PlayerSave : JSONSave
    {
        [JsonProperty] public int mutationPoints;
        [JsonProperty] public List<int> availablePartSchemaHashes = new();
        [JsonProperty] public List<int> equippedPartSchemaHashes = new();

        public PlayerSave(string path) : base(path)
        {
        }

    }

    public class GameStateSave : JSONSave
    {
        [JsonProperty] public bool isGameOver;
        [JsonProperty] public bool isChoosingStartingPart;
        [JsonProperty] public int selectedStartingPartSchemaHash;
        [JsonProperty] public int worldMapSeed;
        [JsonProperty] public int playerX;
        [JsonProperty] public int playerY;
        [JsonProperty] public bool hasCurrentEncounter;
        [JsonProperty] public int currentEncounterX;
        [JsonProperty] public int currentEncounterY;
        [JsonProperty] public int playerHealth;
        [JsonProperty] public int playerSize;
        [JsonProperty] public List<int> pendingRewardPartSchemaHashes = new();
        [JsonProperty] public List<MapNodeSave> nodes = new();

        public GameStateSave(string path) : base(path)
        {
        }

    }

    public class MapNodeSave
    {
        [JsonProperty] public int x;
        [JsonProperty] public int y;
        [JsonProperty] public bool isVisited;
        [JsonProperty] public bool isAccessible;
        [JsonProperty] public bool encounterCompleted;
        [JsonProperty] public bool playerWon;
    }
}
