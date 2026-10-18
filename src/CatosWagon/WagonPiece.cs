using System;
using System.IO;
using System.Linq;
using System.Reflection;
using Jotunn.Configs;
using Jotunn.Entities;
using Jotunn.Managers;
using UnityEngine;

namespace CatosWagon
{
    internal static class WagonPiece
    {
        private const string BundleResourceSuffix = ".Assets.Wagon.catoswagon_market_wagons";
        private const string OpenVisualName = "HouseholdMarketWagon_Open_Visual";
        private const string ClosedVisualName = "HouseholdMarketWagon_Closed_Visual";

        private static bool _registered;
        private static AssetBundle _visualBundle;
        private static Sprite _wagonIcon;
        private static MethodInfo _loadImage;

        public static void Register()
        {
            if (_registered) return;
            PrefabManager.OnVanillaPrefabsAvailable -= Register;

            string buildSourceName = FindBuildPiecePrefabName();
            if (buildSourceName == null || !LoadBundle()) return;

            Sprite wagonIcon = LoadWagonIcon();
            if (wagonIcon == null) return;

            bool openRegistered = RegisterWagon(
                buildSourceName,
                "piece_catoswagon_open",
                OpenVisualName,
                "Catos Wagon (Open)",
                "A rustic market wagon with its canopy open.",
                wagonIcon);
            bool closedRegistered = RegisterWagon(
                buildSourceName,
                "piece_catoswagon_closed",
                ClosedVisualName,
                "Catos Wagon (Closed)",
                "A rustic market wagon with its canopy closed.",
                wagonIcon);

            _registered = openRegistered && closedRegistered;
            if (_registered)
                Plugin.Log.LogInfo("Catos Wagon pieces registered");
        }

        private static bool RegisterWagon(
            string buildSourceName,
            string pieceName,
            string visualName,
            string displayName,
            string description,
            Sprite icon)
        {
            GameObject visualSource = LoadVisualPrefab(visualName);
            if (visualSource == null)
            {
                Plugin.Log.LogError($"Catos Wagon: visual prefab '{visualName}' was not found in the embedded bundle.");
                return false;
            }

            var wagonPiece = new CustomPiece(pieceName, buildSourceName, new PieceConfig
            {
                PieceTable = "Hammer",
                Category = "Catos Wagon",
                Name = displayName,
                Description = description,
                Icon = icon,
                Requirements = new[]
                {
                    new RequirementConfig { Item = "Wood", Amount = 40 },
                    new RequirementConfig { Item = "FineWood", Amount = 20 },
                    new RequirementConfig { Item = "DeerHide", Amount = 8 }
                }
            });

            AttachVisual(wagonPiece.PiecePrefab, visualSource);
            Piece piece = wagonPiece.PiecePrefab.GetComponent<Piece>();
            if (piece != null) piece.m_canBeRemoved = true;

            PieceManager.Instance.AddPiece(wagonPiece);
            Plugin.Log.LogInfo($"Registered {pieceName} using visual {visualName}");
            return true;
        }

        private static string FindBuildPiecePrefabName()
        {
            string[] candidates = { "piece_wood_floor", "wood_floor", "piece_stone_floor", "stone_floor" };
            string buildPieceName = candidates.FirstOrDefault(
                candidate => PrefabManager.Cache.GetPrefab<GameObject>(candidate) != null);

            if (buildPieceName == null)
            {
                Plugin.Log.LogError($"Catos Wagon: no neutral vanilla build-piece found: {string.Join(", ", candidates)}");
            }

            return buildPieceName;
        }

        private static Sprite LoadWagonIcon()
        {
            if (_wagonIcon != null) return _wagonIcon;

            Assembly assembly = typeof(WagonPiece).Assembly;
            string resource = assembly.GetManifestResourceNames()
                .FirstOrDefault(name => name.EndsWith(".Assets.icon.png", StringComparison.OrdinalIgnoreCase));
            if (resource == null)
            {
                Plugin.Log.LogError("Catos Wagon: embedded wagon icon was not found.");
                return null;
            }

            using (Stream stream = assembly.GetManifestResourceStream(resource))
            using (var buffer = new MemoryStream())
            {
                if (stream == null)
                {
                    Plugin.Log.LogError($"Catos Wagon: embedded icon stream '{resource}' was empty.");
                    return null;
                }

                stream.CopyTo(buffer);
                byte[] bytes = buffer.ToArray();
                var texture = new Texture2D(2, 2, TextureFormat.RGBA32, false);
                if (!TryLoadImage(texture, bytes))
                {
                    UnityEngine.Object.Destroy(texture);
                    Plugin.Log.LogError("Catos Wagon: embedded icon could not be decoded.");
                    return null;
                }

                texture.name = "CatosWagonIconTexture";
                _wagonIcon = Sprite.Create(
                    texture,
                    new Rect(0f, 0f, texture.width, texture.height),
                    new Vector2(0.5f, 0.5f),
                    100f);
                _wagonIcon.name = "CatosWagonIcon";
                return _wagonIcon;
            }
        }

        private static bool TryLoadImage(Texture2D texture, byte[] bytes)
        {
            if (_loadImage == null)
            {
                _loadImage = typeof(Texture2D).GetMethod("LoadImage", new[] { typeof(byte[]) });
                if (_loadImage == null)
                {
                    Type imageConversion = Type.GetType("UnityEngine.ImageConversion, UnityEngine.ImageConversionModule");
                    if (imageConversion != null)
                        _loadImage = imageConversion.GetMethod("LoadImage", new[] { typeof(Texture2D), typeof(byte[]) });
                }
            }

            if (_loadImage == null)
            {
                Plugin.Log.LogError("Catos Wagon: Unity image conversion support was not found.");
                return false;
            }

            object result = _loadImage.GetParameters().Length == 1
                ? _loadImage.Invoke(texture, new object[] { bytes })
                : _loadImage.Invoke(null, new object[] { texture, bytes });
            return result is bool loaded && loaded;
        }

        private static void AttachVisual(GameObject functionalPrefab, GameObject source)
        {
            GameObject visual = UnityEngine.Object.Instantiate(source);
            visual.name = source.name;
            visual.transform.SetParent(functionalPrefab.transform, false);
            visual.transform.localPosition = Vector3.zero;
            visual.transform.localRotation = Quaternion.identity;
            visual.transform.localScale = Vector3.one;

            foreach (Renderer renderer in functionalPrefab.GetComponentsInChildren<Renderer>(true))
            {
                if (!renderer.transform.IsChildOf(visual.transform)) renderer.enabled = false;
            }
        }

        private static GameObject LoadVisualPrefab(string visualName)
        {
            return _visualBundle == null ? null : _visualBundle.LoadAsset<GameObject>(visualName);
        }

        private static bool LoadBundle()
        {
            if (_visualBundle != null) return true;

            Assembly assembly = typeof(WagonPiece).Assembly;
            string resource = assembly.GetManifestResourceNames()
                .FirstOrDefault(name => name.EndsWith(BundleResourceSuffix, StringComparison.OrdinalIgnoreCase));
            if (resource == null)
            {
                Plugin.Log.LogError($"Catos Wagon: embedded bundle ending with '{BundleResourceSuffix}' was not found.");
                return false;
            }

            using (Stream stream = assembly.GetManifestResourceStream(resource))
            using (var buffer = new MemoryStream())
            {
                if (stream == null)
                {
                    Plugin.Log.LogError($"Catos Wagon: embedded bundle stream '{resource}' was empty.");
                    return false;
                }

                stream.CopyTo(buffer);
                _visualBundle = AssetBundle.LoadFromMemory(buffer.ToArray());
            }

            if (_visualBundle == null)
            {
                Plugin.Log.LogError("Catos Wagon: AssetBundle.LoadFromMemory returned null.");
                return false;
            }

            return true;
        }
    }
}
