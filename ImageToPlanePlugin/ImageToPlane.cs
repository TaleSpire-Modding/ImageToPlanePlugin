using System.Collections.Concurrent;
using System.IO;
using System.Windows.Forms;
using UnityEngine;
using BepInEx;
using BepInEx.Configuration;
using PhotonUtil;
using Newtonsoft.Json;

namespace ImageToPlane
{
    

    [BepInPlugin(Guid, "ImageToPlane", Version)]
    [BepInDependency("org.hollofox.plugins.PhotonUtil")]
    public class ImageToPlane: BaseUnityPlugin
    {
        private const string Guid = "org.hollofox.plugins.imageToPlane";
        private const string Version = "1.1.0.0";
        private GameObject _cube;
        private ConcurrentQueue<PhotonMessage> _queue;
        private readonly JsonSerializerSettings _jsonSetting = new JsonSerializerSettings { ReferenceLoopHandling = ReferenceLoopHandling.Ignore, TypeNameHandling = TypeNameHandling.None };
        private bool _rendered = false;

        private ConfigEntry<KeyboardShortcut> LoadImage { get; set; }
        private ConfigEntry<KeyboardShortcut> ClearImage { get; set; }
        private ConfigEntry<int> PixelsPerTile { get; set; }

        void Awake()
        {
            Logger.LogInfo("In Awake for ImageToPlane");

            Debug.Log("ImageToPlane Plug-in loaded");
            LoadImage = Config.Bind("Hotkeys", "Load Image Shortcut", new KeyboardShortcut(KeyCode.F1));
            ClearImage = Config.Bind("Hotkeys", "Clear Image Shortcut", new KeyboardShortcut(KeyCode.F2));
            PixelsPerTile = Config.Bind("Scale", "Scale Size", 40);

            // Load PUP
            PhotonUtilPlugin.AddQueue(Guid);
            _queue = PhotonUtilPlugin.GetIncomingMessageQueue(Guid);
        }
        
        void Update()
        {
            try
            {
                if (Input.GetKey(LoadImage.Value.MainKey))
                {
                    var dialog = new OpenFileDialog
                    {
                        Filter = "Image Files|*.bmp;*.jpg;*.jpeg;*.png;",
                        InitialDirectory = "C:",
                        Title = "Select an Image"
                    };
                    string path = null;
                    if (dialog.ShowDialog() == DialogResult.OK) path = dialog.FileName;
                    if (!string.IsNullOrWhiteSpace(path))
                    {
                        var fileContent = File.ReadAllBytes(path);
                        var texture = new Texture2D(0, 0);
                        texture.LoadImage(fileContent);

                        if (_cube == null) _cube = GameObject.CreatePrimitive(PrimitiveType.Cube);
                        var rend = _cube.GetComponent<Renderer>();

                        _cube.transform.localScale = new Vector3(((float) texture.width) / PixelsPerTile.Value + 0.01f,
                            0.01f, ((float) texture.height) / PixelsPerTile.Value + 0.01f);

                        rend.material.mainTexture = texture;
                        rend.material.SetTexture("main", texture);

                        var messageContent = JsonConvert.SerializeObject(fileContent,Formatting.None, _jsonSetting);
                        var message = new PhotonMessage
                        {
                            PackageId = Guid,
                            Version = Version,
                            SerializedMessage = messageContent
                        };
                        PhotonUtilPlugin.SendMessage(message);
                        _rendered = true;
                    }
                }
                else if (Input.GetKey(ClearImage.Value.MainKey) && _rendered)
                {
                    var t = _cube;
                    _cube = null;
                    if (t != null) Destroy(t);
                    var message = new PhotonMessage
                    {
                        PackageId = Guid,
                        Version = Version,
                        SerializedMessage = "Clear"
                    };
                    PhotonUtilPlugin.SendMessage(message);
                    _rendered = false;
                }
                else
                {
                    _queue.TryDequeue(out var message);
                    if (message == null) return;
                    if (message.SerializedMessage == "Clear" && _rendered)
                    {
                        var t = _cube;
                        _cube = null;
                        if (t != null) Destroy(t);
                        _rendered = false;
                    }
                    else
                    {
                        var texture = new Texture2D(0, 0);
                        var fileContent = JsonConvert.DeserializeObject<byte[]>(message.SerializedMessage,_jsonSetting);
                        texture.LoadImage(fileContent);

                        if (_cube == null) _cube = GameObject.CreatePrimitive(PrimitiveType.Cube);
                        var rend = _cube.GetComponent<Renderer>();
                        _cube.transform.localScale = new Vector3(((float) texture.width) / PixelsPerTile.Value + 0.01f,
                            0.01f, ((float) texture.height) / PixelsPerTile.Value + 0.01f);
                        rend.material.mainTexture = texture;
                        rend.material.SetTexture("main", texture);
                        _rendered = true;
                    }
                }
            }
            catch (System.Exception ex)
            {
                Debug.Log("Crash in Image To Plane Plugin");
                Debug.Log(ex.Message);
                Debug.Log(ex.StackTrace);
                Debug.Log(ex.InnerException);
                Debug.Log(ex.Source);
            }
        }
    }
}
