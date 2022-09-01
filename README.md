# Image To Plane Plugin

This is a plugin for TaleSpire using BepInEx.

## Install

Go to the releases folder and download the latest and extract to the contents of your TaleSpire game folder.

## Usage

After installing, you can press F1 to bring up a request to select an image either via url or dial.
Upon selecting an image, a plane the with selected image will be displayed.
- Pressing F1 will allow to select a new image.
- Pressing F2 will clear the map of the displayed image.
- Pressing F3 will prompt a vector to move the image over time.

The plane scales the image to currently render at 40px per tile.
You can change the bind keys in config editor.
Image limitation is 16384x16384 pixels in size.

Input formatting supports vector and CSV, following are examples of inputs:
```CSharp
[x,y,z,t]
[x,y,z]
x,y,z,t
x,y,z
```
If it is not supplied, transition occurs instantly.

## How to Compile / Modify

Open ```ImageToPlanePlugin.sln``` in Visual Studio.

You will need to add references to:

```
* BepInEx.dll  (Download from the BepInEx project.)
* Bouncyrock.TaleSpire.Runtime (found in Steam\steamapps\common\TaleSpire\TaleSpire_Data\Managed)
* UnityEngine.dll
* UnityEngine.CoreModule.dll
* UnityEngine.InputLegacyModule.dll 
* UnityEngine.UI
* Unity.TextMeshPro
* System.Windows.Forms
* PhotonUtil (found https://github.com/brajm008/PhotonUtilPlugin)
```

Build the project.

Browse to the newly created ```bin/Debug``` or ```bin/Release``` folders and copy the ```ImageToPlanePlugin.dll``` to ```Steam\steamapps\common\TaleSpire\BepInEx\plugins```

## Changelog
- 2.3.0: Added video support (`.mp4, .mov, .webm, .wmv`)
- 2.3.0: Updated config callback in preparation for configmanager
- 2.2.2: Fix config error
- 2.2.1: Logging update
- 2.2.0: Polymorph and winform patch
- 2.1.2: Doc update, Repo Moved
- 2.1.1: Doc update
- 2.1.0: Added ability to move plane using 4D vector e.g. `[0,10,0,5]` will move the plane x=0, y=10, z=0, over 5 seconds. (time is optional and defaults to 0)
- 2.0.0: Last Change Logged

## Shoutouts
Shoutout to my Patreons on https://www.patreon.com/HolloFox recognising your
mighty contribution to my caffeine addiction:
- John Fuller
- [Tales Tavern](https://talestavern.com/) - MadWizard
- Joaqim Planstedt
