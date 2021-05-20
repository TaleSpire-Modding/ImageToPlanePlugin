using System;
using System.IO;
using System.Net.Sockets;
using System.Windows.Forms;
using UnityEngine;
using BepInEx;
using BepInEx.Configuration;
using NetworkPlugin;
using Newtonsoft.Json;

namespace ImageToPlane
{

    [BepInPlugin(Guid, "ImageToPlane", Version)]
    [BepInDependency(NetworkUtilPlugin.Guid)]
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

            // Load NUP
            NetworkUtilPlugin.AddClientCallback(Guid,ServerCallback);
            NetworkUtilPlugin.AddServerCallback(Guid,ClientCallback);

            // Get TempAuthorId
            _playerId = NetworkUtilPlugin.GetAuthorId();
        }
        
        /// <summary>
        /// Looping method run by plugin
        /// </summary>
        void Update()
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
                    var messageContent = JsonConvert.SerializeObject(fileContent,Formatting.None, _jsonSetting);
                    var message = new NetworkMessage
                    {
                        PackageId = Guid,
                        Version = Version,
                        SerializedMessage = messageContent,
                        TempAuthorId = _playerId
                    };
                    SendMessage(message);
                }
                else if (Input.GetKey(ClearImage.Value.MainKey) && _rendered)
                {
                    Cleanup();
                    var message = new NetworkMessage
                    {
                        PackageId = Guid,
                        Version = Version,
                        SerializedMessage = "Clear",
                        TempAuthorId = _playerId
                    };
                    SendMessage(message);
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

        /// <summary>
        /// Sends a network message determined if client or host.
        /// </summary>
        /// <param name="message">message being sent</param>
        private static void SendMessage(NetworkMessage message)
        {
            if (NetworkUtilPlugin.IsHost())
            {
                NetworkUtilPlugin.ServerSendMessage(message);
            }
            else if (NetworkUtilPlugin.IsClient())
            {
                NetworkUtilPlugin.ClientSendMessage(message);
            }
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

        /// <summary>
        /// Code executed when the server receives a message
        /// </summary>
        /// <param name="socket">the socket</param>
        /// <param name="message">the message</param>
        public void ServerCallback(Socket socket, NetworkMessage message)
        {
            if (message.SerializedMessage == "Clear" && _rendered) Cleanup();
            else
            {
                MakeMap(JsonConvert.DeserializeObject<byte[]>(message.SerializedMessage, _jsonSetting));
            }
            
            // re-broadcast to everyone else
            SendMessage(message);
        }

        /// <summary>
        /// Code executed when the client receives a message
        /// </summary>
        /// <param name="socket">the socket</param>
        /// <param name="message">the message</param>
        public void ClientCallback(Socket socket, NetworkMessage message)
        {
            if (message.TempAuthorId == _playerId) return;
            if (message.SerializedMessage == "Clear" && _rendered) Cleanup();
            else {
                MakeMap(JsonConvert.DeserializeObject<byte[]>(message.SerializedMessage, _jsonSetting));
            }
        }
    }
}
