using System.IO;
using System.Windows.Forms;
using UnityEngine;
using BepInEx;
using BepInEx.Configuration;

namespace ImageToPlane
{
    [BepInPlugin("org.hollofox.plugins.imageToPlane", "ImageToPlane", "0.1.0.0")]
    public class ImageToPlane: BaseUnityPlugin
    {
        private GameObject cube;

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
        }
        
        void Update()
        {
            try
            {
                if (Input.GetKey(KeyCode.F1))
                {
                    OpenFileDialog dialog = new OpenFileDialog();
                    dialog.Filter = "Image Files|*.bmp;*.jpg;*.jpeg;*.png;";
                    //Next is the starting directory for the dialog and the title for the dialog box are set.  
                    dialog.InitialDirectory = "C:";
                    dialog.Title = "Select an Image";
                    string path = null;
                    if (dialog.ShowDialog() == DialogResult.OK) path = dialog.FileName;
                    if (!string.IsNullOrWhiteSpace(path))
                    {
                        var fileContent = File.ReadAllBytes(path);
                        Texture2D texture = new Texture2D(0, 0);
                        texture.LoadImage(fileContent);

                        if (cube == null) cube = GameObject.CreatePrimitive(PrimitiveType.Cube);
                        Renderer rend = cube.GetComponent<Renderer>();

                        cube.transform.localScale = new Vector3(((float)texture.width) / PixelsPerTile.Value + 0.01f, 0.01f, ((float)texture.height) / PixelsPerTile.Value + 0.01f);

                        rend.material.mainTexture = texture;
                        rend.material.SetTexture("main", texture);
                    }
                }
                else if (Input.GetKey(KeyCode.F2))
                {
                    var t = cube;
                    cube = null;
                    if (t != null) Destroy(t);
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
