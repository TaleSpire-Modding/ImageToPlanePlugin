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
    [BepInDependency("org.hollofox.plugins.PhotonUtil", BepInDependency.DependencyFlags.HardDependency)]
    public class ImageToPlane: BaseUnityPlugin
    {
        const string Guid = "org.hollofox.plugins.imageToPlane";
        const string Version = "1.0.5.0";
        private GameObject cube;
        private ConcurrentQueue<PhotonMessage> Queue;
        private JsonSerializerSettings JsonSetting = new JsonSerializerSettings { ReferenceLoopHandling = ReferenceLoopHandling.Ignore };

        private ConfigEntry<KeyboardShortcut> LoadImage { get; set; }
        private ConfigEntry<KeyboardShortcut> ClearImage { get; set; }
        private ConfigEntry<int> PixelsPerTile { get; set; }
        // Awake is called once when both the game and the plug-in are loaded
        void Awake()
        {
            Logger.LogInfo("In Awake for ImageToPlane");

            UnityEngine.Debug.Log("ImageToPlane Plug-in loaded");
            LoadImage = Config.Bind("Hotkeys", "Load Image Shortcut", new KeyboardShortcut(KeyCode.F1));
            ClearImage = Config.Bind("Hotkeys", "Clear Image Shortcut", new KeyboardShortcut(KeyCode.F2));
            PixelsPerTile = Config.Bind("Scale", "Scale Size", 40);

            Debug.Log($"Load Attempt 1:{PhotonUtilPlugin.AddQueue(Guid)}");
            Queue = PhotonUtilPlugin.GetIncomingMessageQueue(Guid);
        }
        
        void Update()
        {
            try
            {
                if (Input.GetKey(KeyCode.F1))
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
                        Texture2D texture = new Texture2D(0, 0);
                        texture.LoadImage(fileContent);

                        if (cube == null) cube = GameObject.CreatePrimitive(PrimitiveType.Cube);
                        Renderer rend = cube.GetComponent<Renderer>();

                        cube.transform.localScale = new Vector3(((float) texture.width) / PixelsPerTile.Value + 0.01f,
                            0.01f, ((float) texture.height) / PixelsPerTile.Value + 0.01f);

                        rend.material.mainTexture = texture;
                        rend.material.SetTexture("main", texture);

                        var messageContent = JsonConvert.SerializeObject(texture,Formatting.None, JsonSetting);
                        var message = new PhotonMessage
                        {
                            PackageId = Guid,
                            Version = Version,
                            SerializedMessage = messageContent
                        };
                        PhotonUtilPlugin.SendMessage(message);
                    }
                }
                else if (Input.GetKey(KeyCode.F2))
                {
                    var t = cube;
                    cube = null;
                    if (t != null) Destroy(t);
                    var message = new PhotonMessage
                    {
                        PackageId = Guid,
                        Version = Version,
                        SerializedMessage = "Clear"
                    };
                    PhotonUtilPlugin.SendMessage(message);
                }
                else
                {
                    Queue.TryDequeue(out var message);
                    if (message == null) return;
                    if (message.SerializedMessage == "Clear")
                    {
                        var t = cube;
                        cube = null;
                        if (t != null) Destroy(t);
                    }
                    else
                    {
                        var texture = JsonConvert.DeserializeObject<Texture2D>(message.SerializedMessage);
                        if (cube == null) cube = GameObject.CreatePrimitive(PrimitiveType.Cube);
                        var rend = cube.GetComponent<Renderer>();
                        cube.transform.localScale = new Vector3(((float) texture.width) / PixelsPerTile.Value + 0.01f,
                            0.01f, ((float) texture.height) / PixelsPerTile.Value + 0.01f);
                        rend.material.mainTexture = texture;
                        rend.material.SetTexture("main", texture);
                    }
                }
            }
            catch (System.Exception ex)
            {
                UnityEngine.Debug.Log("Crash in Image To Plane Plugin");
                UnityEngine.Debug.Log(ex.Message);
                UnityEngine.Debug.Log(ex.StackTrace);
                UnityEngine.Debug.Log(ex.InnerException);
                UnityEngine.Debug.Log(ex.Source);
            }
        }
    }
}
