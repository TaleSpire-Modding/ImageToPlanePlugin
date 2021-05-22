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

namespace ImageToPlane
{

    [BepInPlugin(Guid, "ImageToPlane", Version)]
    [BepInDependency(PhotonUtilPlugin.Guid)]
    public class ImageToPlane: BaseUnityPlugin
    {
        // constants
        private const string Guid = "org.hollofox.plugins.imageToPlane";
        private const string Version = "1.1.0.0";
        
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
            PhotonUtilPlugin.AddMod(Guid);
        }

        private bool OnBoard()
        {
            return (CameraController.HasInstance &&
                    BoardSessionManager.HasInstance &&
                    BoardSessionManager.HasBoardAndIsInNominalState &&
                    !BoardSessionManager.IsLoading);
        }

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
                        // Get Image
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

                        // Push to Server / Clients
                        ClearMessage();
                        var intarray = fileContent.Select(t => (int) t).ToList();
                        var messageContent = JsonConvert.SerializeObject(intarray);

                        var message = new PhotonMessage
                        {
                            PackageId = Guid,
                            Version = Version,
                            SerializedMessage = messageContent,
                        };

                        SendMessage(message);
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
                        SendMessage(message);
                    }
                    
                    // Now we check for incoming messages
                    var messages = PhotonUtilPlugin.GetMessages(Guid);

                    var newM = false;

                    foreach (var m in messages.SelectMany(player => player.Value))
                    {
                        if (latest == null)
                        {
                            latest = m;
                            newM = true;
                        }
                        else if (latest.Created < m.Created)
                        {
                            latest = m;
                            newM = true;
                        }
                    }

                    if (latest != null && newM)
                    {
                        Debug.Log(latest.SerializedMessage);

                        if (latest.Author != PhotonUtilPlugin.GetAuthor())
                        {
                            if (latest.SerializedMessage == "Clear")
                            {
                                Cleanup();
                            }
                            else
                            {
                                var intArray = JsonConvert.DeserializeObject<List<int>>(latest.SerializedMessage);
                                byte[] SerializedMessage = intArray.Select(i => (byte) i).ToArray();
                                MakeMap(SerializedMessage);
                            }
                        }
                    }

                    Messages = messages;

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

        /// <summary>
        /// Sends a network message determined if client or host.
        /// </summary>
        /// <param name="message">message being sent</param>
        private static void SendMessage(PhotonMessage message)
        {
            Debug.Log(message.SerializedMessage);
            PhotonUtilPlugin.AddMessage(Guid,message);
        }

        /// <summary>
        /// Sends a network message determined if client or host.
        /// </summary>
        /// <param name="message">message being sent</param>
        private static void ClearMessage()
        {
            PhotonUtilPlugin.ClearNonPersistent(Guid);
        }

        /// <summary>
        /// Displays an image
        /// </summary>
        /// <param name="fileContent">Makes an image at origin</param>
        private void MakeMap(byte[] fileContent)
        {
            var texture = new Texture2D(0, 0);
            texture.LoadImage(fileContent);
            if (_cube == null) _cube = GameObject.CreatePrimitive(PrimitiveType.Cube);
            var rend = _cube.GetComponent<Renderer>();
            _cube.transform.localScale = new Vector3(((float)texture.width) / PixelsPerTile.Value + 0.01f,
                0.01f, ((float)texture.height) / PixelsPerTile.Value + 0.01f);
            rend.material.mainTexture = texture;
            rend.material.SetTexture("main", texture);
            _rendered = true;
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
