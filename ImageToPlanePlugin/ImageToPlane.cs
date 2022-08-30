using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Windows.Forms;
using UnityEngine;
using BepInEx;
using BepInEx.Configuration;
using PhotonUtil;
using UnityEngine.Networking;
using System.Collections;
using ModdingTales;
using PluginUtilities;
using SRF;
using UnityEngine.Video;

namespace ImageToPlane
{

    [BepInPlugin(Guid, "HolloFoxes' ImageToPlane Plugin", Version)]
    [BepInDependency(PhotonUtilPlugin.Guid)]
    [BepInDependency(SetInjectionFlag.Guid)]
    public class ImageToPlane : BaseUnityPlugin
    {
        // constants
        private const string Guid = "org.hollofox.plugins.imageToPlane";
        private const string Version = "2.3.0.0";

        // Cube based settings
        private GameObject _cube;

        private bool _rendered;

        // Id of player for NUP
        private Guid _playerId;

        // Configs
        private static ConfigEntry<ModdingUtils.LogLevel> LogLevelConfig { get; set; }
        private ConfigEntry<KeyboardShortcut> LoadImage { get; set; }
        private ConfigEntry<KeyboardShortcut> ClearImage { get; set; }
        private ConfigEntry<KeyboardShortcut> MoveImage { get; set; }
        private ConfigEntry<int> PixelsPerTile { get; set; }
        
        private ConfigEntry<float> TilesWide { get; set; }
        private ConfigEntry<float> TilesLong { get; set; }

        PhotonMessage latest;

        // Track Messages
        private Dictionary<PhotonPlayer, List<PhotonMessage>> Messages =
            new Dictionary<PhotonPlayer, List<PhotonMessage>>();

        private static ModdingUtils.LogLevel LogLevel => LogLevelConfig.Value == ModdingUtils.LogLevel.Inherited ? ModdingUtils.LogLevelConfig.Value : LogLevelConfig.Value;

        /// <summary>
        /// Awake plugin
        /// </summary>
        void Awake()
        {
            DoConfig(Config);
            
            // Load PUP
            ModdingUtils.Initialize(this, Logger);
            PhotonUtilPlugin.AddMod(Guid);

            if (LogLevel > ModdingUtils.LogLevel.None)
                Logger.LogInfo("ImageToPlane Plug-in loaded");
        }

        private void DoConfig(ConfigFile config)
        {
            // Descriptions for configs
            var logLevelDescription = new ConfigDescription("logging level, inherited determined by setinjectionflag", null, new ConfigurationManagerAttributes { IsAdvanced = true });
            var loadImageDescription = new ConfigDescription("keybind to spawn plane", null, new ConfigurationManagerAttributes());
            var clearImageDescription = new ConfigDescription("keybind to remove plane", null, new ConfigurationManagerAttributes());
            var moveImageDescription = new ConfigDescription("keybind to move plane", null, new ConfigurationManagerAttributes());
            var pixelsPerTileDescription = new ConfigDescription("pixel resolution per tile", null, new ConfigurationManagerAttributes { CallbackAction = AdjustMapSize });
            var videoSizeDescription = new ConfigDescription("Tile Dimension for Video", null, new ConfigurationManagerAttributes { CallbackAction = AdjustVideoSize});

            // Actual Configs
            LogLevelConfig = config.Bind("Logging", "Log Level", ModdingUtils.LogLevel.Inherited, logLevelDescription);

            if (LogLevel > ModdingUtils.LogLevel.None)
                Logger.LogInfo("In Awake for ImageToPlane");

            // KeyBinds
            LoadImage = config.Bind("Hotkeys", "Load Image Shortcut", new KeyboardShortcut(KeyCode.F1),loadImageDescription);
            ClearImage = config.Bind("Hotkeys", "Clear Image Shortcut", new KeyboardShortcut(KeyCode.F2),clearImageDescription);
            MoveImage = config.Bind("Hotkeys", "Move Image Shortcut", new KeyboardShortcut(KeyCode.F3),moveImageDescription);
            
            // Plane Resolution
            PixelsPerTile = config.Bind("Scale", "Scale Size", 40,pixelsPerTileDescription);
            TilesWide = config.Bind("Video", "Tiles Wide", 19.20f * 5, videoSizeDescription);
            TilesLong = config.Bind("Video", "Tiles High", 10.80f * 5, videoSizeDescription);

            if (LogLevel >= ModdingUtils.LogLevel.Low)
                Logger.LogInfo("Config Bound");
        }

        private bool OnBoard()
        {
            return (CameraController.HasInstance &&
                    BoardSessionManager.HasInstance &&
                    BoardSessionManager.HasBoardAndIsInNominalState &&
                    !BoardSessionManager.IsLoading);
        }

        private readonly TimeSpan _fetchTimeSpan = TimeSpan.FromSeconds(1);
        private DateTime _lastChecked = DateTime.Now;

        private List<Vector4> movement = new List<Vector4>();
        private bool isMoving;

        /// <summary>
        /// Looping method run by plugin
        /// </summary>
        void Update()
        {
            if (OnBoard())
            {
                try
                {

                    if (Input.GetKey(LoadImage.Value.MainKey))
                    {
                        SystemMessage.AskForTextInput("Media URI", "Enter the URI to your media",
                            "OK", delegate(string mediaUrl)
                            {
                                if (mediaUrl.Length > 256)
                                {
                                    if (LogLevel > ModdingUtils.LogLevel.None)
                                        Logger.LogWarning($"Media URL is too long: {mediaUrl}");
                                }
                                else
                                {
                                    PhotonUtilPlugin.AddMessage(new PhotonMessage
                                    {
                                        PackageId = Guid,
                                        SerializedMessage = mediaUrl,
                                        Version = Version,
                                    });
                                }
                            }, delegate { }, "Open Board Locally Instead", delegate
                            {
                                var dialog = new OpenFileDialog
                                {
                                    Filter = "Image Files|*.bmp;*.jpg;*.jpeg;*.png;|Video Files (*.*)|*.mp4;*.mov;*.webm;*.wmv;|All Files (*.*)|*.*",
                                    InitialDirectory = "C:",
                                    Title = "Select Media"
                                };
                                string path = null;
                                if (dialog.ShowDialog() == DialogResult.OK) path = dialog.FileName;
                                if (LogLevel == ModdingUtils.LogLevel.All)
                                    Logger.LogDebug(path);

                                if (string.IsNullOrWhiteSpace(path)) return;
                                
                                // Make Map
                                if (path.EndsWith(".mp4") || path.EndsWith(".mov;") || path.EndsWith(".webm;") || path.EndsWith(".wmv;") )
                                    MakeMap(path);
                                else
                                    MakeMap(File.ReadAllBytes(path));
                            });
                    }
                    else if (Input.GetKey(ClearImage.Value.MainKey) && _rendered)
                    {
                        Cleanup();
                        ClearMessage();
                        var message = new PhotonMessage
                        {
                            PackageId = Guid,
                            Version = Version,
                            SerializedMessage = "Clear"
                        };
                        PhotonUtilPlugin.AddMessage(message);
                    }
                    else if (DateTime.Now - _lastChecked > _fetchTimeSpan)
                    {
                        _lastChecked = DateTime.Now;
                        var messages = PhotonUtilPlugin.GetNewMessages(Guid);
                        foreach (var message in from m in messages.Values
                            from message in m
                            where message != null && !message.Viewed
                            select message)
                        {
                            StartCoroutine(DownloadImage(message.SerializedMessage));
                        }
                    }
                    else if (_load)
                    {
                        MakeMap(_bufferTexture);
                        _load = false;
                    }

                    if (Input.GetKey(MoveImage.Value.MainKey))
                    {
                        SystemMessage.AskForTextInput("Tween Plane", "Enter the movement e.g. [x,y,z,t] => [1,1,1,1]",
                            "OK", delegate (string tween)
                            {
                                var cull = tween.Replace("[", "").Replace("]", "").Split(',');
                                for (int i = 0; i < cull.Length; i++) cull[i] = cull[i].Replace(",", "");
                                var x = float.Parse(cull[0]);
                                var y = float.Parse(cull[1]);
                                var z = float.Parse(cull[2]);
                                var t = 0f;
                                if (cull.Length == 4) t = float.Parse(cull[3]);
                                movement.Add(new Vector4(x,y,z,t));
                            }, delegate { }, "Cancel");
                    }

                }
                catch (Exception ex)
                {
                    Logger.LogError("Crash in Image To Plane Plugin");
                    Logger.LogError(ex.Message);
                    Logger.LogError(ex.StackTrace);
                    Logger.LogError(ex.InnerException);
                    Logger.LogError(ex.Source);
                }
            }

            if (isMoving || _cube == null || movement.Count == 0) return;
            var move = new Vector3(movement[0].x, movement[0].y, movement[0].z);
            var time = movement[0].w;
            StartCoroutine(moveObject(_cube,move,time));
            movement.RemoveAt(0);
        }

        internal IEnumerator moveObject(GameObject o, Vector3 move, float totalMovementTime)
        {
            if (LogLevel >= ModdingUtils.LogLevel.Medium)
                Logger.LogInfo("ITP Moving cube");
            isMoving = true;
            var origin = o.transform.localPosition;
            var destination = origin + move;
            if (LogLevel >= ModdingUtils.LogLevel.High)
                Logger.LogInfo($"dest:[{destination.x},{destination.y},{destination.z}]");
            var currentMovementTime = 0f;//The amount of time that has passed
            while (Vector3.Distance(o.transform.localPosition, destination) > 0)
            {
                if (LogLevel == ModdingUtils.LogLevel.All)
                    Logger.LogInfo("ITP Moving cube Loop");
                currentMovementTime += Time.deltaTime;
                o.transform.localPosition = Vector3.Lerp(origin, destination, currentMovementTime / totalMovementTime);
                yield return null;
            }
            o.transform.localPosition = destination;
            isMoving = false;
        }

        private static void ClearMessage()
        {
            PhotonUtilPlugin.ClearNonPersistent(Guid);
        }

        private static bool _load;
        private static Texture2D _bufferTexture;

        /// <summary>
        /// Downloads an image to a Texture
        /// </summary>
        /// <param name="mediaUrl">URL of the image</param>
        /// <returns></returns>
        IEnumerator DownloadImage(string mediaUrl)
        {
            if (mediaUrl.EndsWith(".mp4") || mediaUrl.EndsWith(".mov;") || mediaUrl.EndsWith(".webm;") || mediaUrl.EndsWith(".wmv;")) 
                MakeMap(mediaUrl);
            else
            {
                using (UnityWebRequest request = UnityWebRequestTexture.GetTexture(mediaUrl))
                {
                    yield return request.SendWebRequest();
                    if (request.isNetworkError || request.isHttpError)
                        Logger.LogError(request.error);
                    else
                    {
                        if (LogLevel == ModdingUtils.LogLevel.All)
                            Logger.LogInfo("Downloaded!");
                        _bufferTexture = ((DownloadHandlerTexture)request.downloadHandler).texture;
                        _load = true;
                    }
                }
            }
        }

        /// <summary>
        /// Displays an image
        /// </summary>
        /// <param name="fileContent">Byte array from a read file</param>
        private void MakeMap(byte[] fileContent)
        {
            var texture = new Texture2D(0, 0);
            texture.LoadImage(fileContent);
            MakeMap(texture);
        }

        private void AdjustVideoSize(object o)
        {
            if (_cube == null || !_cube.TryGetComponent(out VideoPlayer _)) return;
            _cube.transform.localScale = new Vector3(TilesWide.Value + 0.01f, 0.01f, TilesLong.Value + 0.01f);
        }

        private void AdjustMapSize(object o)
        {
            if (_cube == null || _cube.TryGetComponent(out VideoPlayer _)) return;
            var rend = _cube.GetComponent<Renderer>();
            var texture = rend.material.mainTexture;
            _cube.transform.localScale = new Vector3(((float)texture.width) / PixelsPerTile.Value + 0.01f,
                0.01f, ((float)texture.height) / PixelsPerTile.Value + 0.01f);
        }

        /// <summary>
        /// Displays an image
        /// </summary>
        /// <param name="uri">location of video</param>
        internal void MakeMap(string uri)
        {
            if (_cube == null) _cube = GameObject.CreatePrimitive(PrimitiveType.Cube);
            
            if (!_cube.TryGetComponent(out VideoPlayer p))
                p = _cube.AddComponent<VideoPlayer>();

            p.playOnAwake = true;
            p.source = VideoSource.Url;
            if (!uri.StartsWith("http"))
                uri = "file://" + uri;
            p.url = uri;
            p.isLooping = true;

            AdjustVideoSize(null);
            _rendered = true;
            _load = false;
        }

        /// <summary>
        /// Displays an image
        /// </summary>
        /// <param name="texture">Texture generated from previous makemap</param>
        private void MakeMap(Texture2D texture)
        {
            if (_cube == null) _cube = GameObject.CreatePrimitive(PrimitiveType.Cube);
            _cube.RemoveComponentIfExists<VideoPlayer>();

            var rend = _cube.GetComponent<Renderer>();
            rend.material.mainTexture = texture;
            rend.material.SetTexture("main", texture);
            AdjustMapSize(null);
            _rendered = true;
            _load = false;
        }

        /// <summary>
        /// Cleans up the image
        /// </summary>
        private void Cleanup()
        {
            var t = _cube;
            _cube = null;
            if (t != null) Destroy(t);
            _rendered = false;
        }
    }
}
