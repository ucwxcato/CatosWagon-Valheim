using BepInEx;
using BepInEx.Logging;
using Jotunn;
using Jotunn.Managers;

namespace CatosWagon
{
    [BepInPlugin(GUID, NAME, VERSION)]
    [BepInDependency(Main.ModGuid)]
    public sealed class Plugin : BaseUnityPlugin
    {
        public const string GUID = "com.catosaurluna.catoswagon";
        public const string NAME = "CatosWagon";
        public const string VERSION = "1.0.0";

        internal static ManualLogSource Log;

        private void Awake()
        {
            Log = Logger;
            PrefabManager.OnVanillaPrefabsAvailable += WagonPiece.Register;
            Log.LogInfo($"{NAME} {VERSION} loaded; waiting for vanilla prefabs");
        }

        private void OnDestroy()
        {
            PrefabManager.OnVanillaPrefabsAvailable -= WagonPiece.Register;
        }
    }
}
