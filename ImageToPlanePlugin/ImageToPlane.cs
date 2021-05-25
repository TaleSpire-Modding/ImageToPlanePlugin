using System;
using System.Text;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Windows.Forms;
using UnityEngine;
using BepInEx;
using BepInEx.Configuration;
using PhotonUtil;
using Newtonsoft.Json;
using UnityEngine.Networking;
using System.Collections;
using System.Net.Mime;
using SRF;

namespace ImageToPlane
{

    [BepInPlugin(Guid, "ImageToPlane", Version)]
    [BepInDependency(PhotonUtilPlugin.Guid)]
    public class ImageToPlane: BaseUnityPlugin
    {
        // constants
        private const string Guid = "org.hollofox.plugins.imageToPlane";
        private const string Version = "2.0.0.0";
        
        // Cube based settings
        private GameObject _cube;
        private readonly JsonSerializerSettings _jsonSetting = new JsonSerializerSettings { ReferenceLoopHandling = ReferenceLoopHandling.Ignore, TypeNameHandling = TypeNameHandling.None };
        private bool _rendered = false;

        // Id of player for NUP
        private Guid _playerId;

        // Configs
        private ConfigEntry<KeyboardShortcut> LoadImage { get; set; }
        private ConfigEntry<KeyboardShortcut> ClearImage { get; set; }
        private ConfigEntry<int> PixelsPerTile { get; set; }

        PhotonMessage latest;

        // Track Messages
        private Dictionary<PhotonPlayer, List<PhotonMessage>> Messages =
            new Dictionary<PhotonPlayer, List<PhotonMessage>>();


        /// <summary>
        /// Awake plugin
        /// </summary>
        void Awake()
        {
            Logger.LogInfo("In Awake for ImageToPlane");

            Debug.Log("ImageToPlane Plug-in loaded");
            LoadImage = Config.Bind("Hotkeys", "Load Image Shortcut", new KeyboardShortcut(KeyCode.F1));
            ClearImage = Config.Bind("Hotkeys", "Clear Image Shortcut", new KeyboardShortcut(KeyCode.F2));
            PixelsPerTile = Config.Bind("Scale", "Scale Size", 40);

            // Load PUP
            ModdingTales.ModdingUtils.Initialize(this, Logger);
            PhotonUtilPlugin.AddMod(Guid);
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
                        SystemMessage.AskForTextInput("Board URL", "Enter the URL to your map (PNG or JPG Image Only)", "OK", delegate (string mediaUrl)
                        {
                            if (mediaUrl.Length > 256)
                            {
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
                        }, delegate
                        {
                        }, "Open Board Locally Instead", delegate
                        {
                            var dialog = new OpenFileDialog
                            {
                                Filter = "Image Files|*.bmp;*.jpg;*.jpeg;*.png;",
                                InitialDirectory = "C:",
                                Title = "Select an Image"
                            };
                            string path = null;
                            if (dialog.ShowDialog() == DialogResult.OK) path = dialog.FileName;
                            if (string.IsNullOrWhiteSpace(path)) return;

                            // make map
                            var fileContent = File.ReadAllBytes(path);
                            MakeMap(fileContent);
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
                        foreach (var message in from m in messages.Values from message in m where message != null && !message.Viewed select message)
                        {
                            StartCoroutine(DownloadImage(message.SerializedMessage));
                        }
                    } 
                    else if (load)
                    {
                        MakeMap(BufferTexture);
                        load = false;
                    }
                }
                catch (Exception ex)
                {
                    Debug.Log("Crash in Image To Plane Plugin");
                    Debug.Log(ex.Message);
                    Debug.Log(ex.StackTrace);
                    Debug.Log(ex.InnerException);
                    Debug.Log(ex.Source);
                }
            }
        }
        
        private static void ClearMessage()
        {
            PhotonUtilPlugin.ClearNonPersistent(Guid);
        }

        private static bool load = false;
        private static Texture2D BufferTexture = null;

        IEnumerator DownloadImage(string MediaUrl)
        {
            using (UnityWebRequest request = UnityWebRequestTexture.GetTexture(MediaUrl))
            {
                yield return request.SendWebRequest();
                if (request.isNetworkError || request.isHttpError)
                    Debug.Log(request.error);
                else
                {
                    Debug.Log("Downloaded!");
                    BufferTexture = ((DownloadHandlerTexture)request.downloadHandler).texture;
                    load = true;
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

        /// <summary>
        /// Displays an image
        /// </summary>
        /// <param name="texture">Texture generated from previous makemap</param>
        private void MakeMap(Texture2D texture)
        {
            if (_cube == null) _cube = GameObject.CreatePrimitive(PrimitiveType.Cube);
            var rend = _cube.GetComponent<Renderer>();
            _cube.transform.localScale = new Vector3(((float)texture.width) / PixelsPerTile.Value + 0.01f,
                0.01f, ((float)texture.height) / PixelsPerTile.Value + 0.01f);
            rend.material.mainTexture = texture;
            rend.material.SetTexture("main", texture);
            _rendered = true;
            load = false;
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
