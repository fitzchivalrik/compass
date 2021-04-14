### Questions
Does the Minimap rotation thingy gets updated when minimap is disabled? -> Appears so
Does the Minimap gets updated at all when minimap is disabled? -> Appears so

### TODO


### Tex Mappings

- Navimap.tex
  
  - 00: Outer, edgy circle
  - 01: Inner circle circle
  - 02: Small Sun
  - 03: Yellow Circle
  - 04: Weather circle outer
  - 05: X
  - 06: Y
  - 07: North <0.4017857f, 0.8301887f> <0.4732143f, 0.9811321f>
  - 08: West <0.4732143f, 0.8301887f> <0.5446429f, 0.9811321f>
  - 09: East <0.5446429f, 0.8301887f> <0.5892857f, 0.9811321f>
  - 10: South <0.5892857f, 0.8301887f> <0.6339286f, 0.9811321f>
  - 11: Settings cog (link/unlink)
  - 12: Dark shadow circle, small
  - 13: Minus
  - 14: Plus
  - 15: Square Shadow
  - 16: ViewTriangle
  - 17: Small circle with outer border
  - 18: Orange Arrow (Arrow for quest out of viewport)
  - 19: Green Arrow
  - 20: Blue Arrow (Arrow for fate out of viewport)
  - 21: Glowing Under Thingy (Makes Icons glow)
  - 22: Red Arrow
  - 23: White Arrow
     
      
- 060955.tex // Arrow Down on QuestMarker
- 060954.tex // Arrow UP on QuestMarker
- 071025.tex // Quest Complete Marker
- 060443.tex // Player Marker
- 060457.tex // Area Transition Bullet Thingy
- 060495.tex // Small Area Circle
- 060496.tex // Big Area Circle 
  - Fate AddRGB 0 0 100 MultiplyRGB 50 100 100 (on ImgNode)
  - Orange(Quest) AddRGB 200 30 0 MultiplyRGB 62 62 24 (on ImgNode)
- 060497.tex // circle
- 060498.tex // Another circle
- 060093.tex // Fate Exclamation Mark
- 060501.tex // Fate Marker
- 060502.tex // Fate Marker Boss Battle
- till
- 060508.tex // Fate markers
- 060546.tex // Arrow Down on Circle Area
- 060542.tex // Arrow UP on Circle Area
- 060414.tex // Dungeon Symbol
- 060430 // Small Aetherythe Symbol
- 071021 // Quest Marker
- 071003 // MSQ Ongoing Marker
- 071005 // MSQ Complete Marker
- 071023 // Quest Ongoing Marker
- 071025 // Quest Complete Marker
- 071063 // BookQuest Ongoing Marker
- 071065 // BookQuest Complete Marker
- 071083 // LeveQuest Ongoing Marker
- 071085 // LeveQuest Complete Marker
- 071143 // BlueQuest Ongoing Marker
- 071145 // BlueQuest Complete Marker
  

### Notes

### AtkUnitBase

dec(x:-22.27, y:56.34 distance: 15.56

ULDData hold lists of all resources and stuff used by nodes in this unitbase
Textures, PartLists, Objects are Arrays with respective Count Var.
-> Can maybe bypass as I feed of _NaviMaps resources?

Prev/Next is bottom to top in regard to UiDebug (Next is Up, Prev is down)
ULDData -> NodeList has all the nodes of the NonComponentNodes.
ComponentNodes have their own ULDData, including Texture info etc.
There are overlaps -> Maybe ULDResourceHandle determines 'ownership'?

AreaMap lowest level has 265 Children in the relevant component node
NaviMap has 206

#### AtkImageNode

- Multiple[Red|Blue|Green]  are percentage in range 0-100 (maybe)
// NOTE WrapMode 1 -> ignore UV and take Width/Size != size, 2-> Fit/Stretch, 0 -> ignore Width/Height, do UV only
// Seems like game uses 1 + correct width/size, matching UV

### Stuff

TargetManagerAddress - 0x120 => Camera XZY Coordinates?

C# 9 Delegate magic
```c#
public unsafe List<Action> LearnedBlueSpells {
  get {
    var func = (delegate*<IntPtr, uint, bool>)(MainModule.BaseAddress + 0x7D54E0);
    var arg = MainModule.BaseAddress + 0x1DB4D50;
    return BlueMageSpells.Values.Where(action => func(arg, action.UnlockLink)).ToList();
  }
}
```

I’ll just add one to the nameplate ui
Since it’s always visible

ChatLog->ULDData.LoadedState == 3
this is the flag that should tell when a ui addon is fully operational

So the OnSetup method for ChatLog is at e86a20
And as I said, if you want to run code only once on setup, good idea to take a look at the ChatLog.OnSetup method. OnSetup is vtbl[44] for anything derived from AtkUnitBase and for ChatLog it's at offset 0xE86A20

Basically, every ui addon (the pointer you get from GetUiModuleByName from Dalamud) inherits from AtkUnitBase  https://github.com/aers/FFXIVClientStructs/blob/main/FFXIV/Component/GUI/AtkUnitBase.cs

and those have a field ULDData.

You can probably just `Marshal.ReadByte( chatLogPtr + 0x28 /* Beginning ULD */ + 0x89 /* Loaded State Flag */ ) == 3`
and you can get the chatLogPtr from Dalamud with GetUiModuleByName("ChatLog", 1) when it's up.

Btw, Monodimentional compass is just a visual representation of what a dot product is; basic maths.
Tip: unrotate before doting if you ever get there..

I use MinMaxNormalize which is exactly the same as NormalizeToRange, but I didn't include the Kismet math header at the time so.. there's that.
The texture is X seamless, and North is in the middle. If South was in the middle, just simply add 0.5 to each result below.
```c++
// Normalize a value between 0 and 1 range.
float MinMaxNormalize(float value, float min, float max)
{
return (value - min) / (max - min);
}
```

I then calculate the -1 to 1 value using this code:

```C++
CurrentWorldDirection = FollowCamera->GetComponentRotation().Yaw;
if (CurrentWorldDirection >= 0.0f)
{
CurrentWorldDirection = MinMaxNormalize(CurrentWorldDirection, 0.0f, 360.0f);
}
else
{
CurrentWorldDirection = -MinMaxNormalize(CurrentWorldDirection, 0.0f, -360.0f);
}
```



```C#
// Get an arrow pointing to the marked object in the player's local frame of reference
Vector3 offset = playerTransform.InverseTransformPoint(markedObjectTransform.position);

// Get an angle that's 0 when the object is directly ahead, 
// and ranges -pi...pi for objects to the left/right.
float angle = Mathf.Atan2(offset.x, offset.z);

// Let's put these markers in a container parent object 
// whose center is where you want the middle of the compass. 
// Then each marker only needs to worry about its x position relative to this.
compassMarker.localPosition = Vector3.right * compassWidth * angle/(2f * Mathf.PI);
```


For poles at an infinite distance you can replace offset = playerTransform.InverseTransformDirection(Vector3.forward) for north, using Vector3.right .back .left for east, south, and west respectively.

`compass.uvRect = new Rect(player.localEulerAngles.y / 360f, 0, 1, 1)`

Camera - PlayerAddress
0x21500CE1D80-0x2153CECB050
= -0x3C1E92D0

Camera - FrameworkBase
0x21500CE1D80-0x2157F8A8F70
= -0x7EBC71F0

Camera - GUIManager
0x21500CE1D80-0x2011FF14
= 0x214E0BC1E6C

Camera - ScriptManager
0x21500CE1D80-0x2157F8ABBD8
= -0x7EBC9E58

Camera - MaybeControllerStruct
0x21500CE1D80-0x2150A360990
= -0x967EC10

Writes Camera Rotation:
ffxiv_dx11.exe+110BDE8:
7FF76BEABDE0 - 0F28 C1  - movaps xmm0,xmm1
7FF76BEABDE3 - E8 A8F957FF - call ffxiv_dx11.exe+68B790
7FF76BEABDE8 - F3 0F11 83 30010000  - movss [rbx+00000130],xmm0 <<
7FF76BEABDF0 - 48 83 C4 20 - add rsp,20
7FF76BEABDF4 - 5B - pop rbx

RAX=0000000000000000
RBX=0000021500CE1C50
RCX=0000021500CE1C50
RDX=000000000000012C
RSI=000002157F8A8F70
RDI=000002153CECB000
RSP=000000C9B2CFE9E0
RBP=0000000000000000
RIP=00007FF76BEABDF0
R8=0000000000000000
R9=00000000001E9B96
R10=0000000000000001
R11=0000021590B7E820
R12=0000000000000000
R13=0000021547DD7FC0
R14=0000021547DD7FC0
R15=00007FFE0A6F58B0


Address of signature = ffxiv_dx11.exe + 0x01D3CE60
"\xC0\xA7\x46\x6C\xF7\x7F", "xxxxxx"
"C0 A7 46 6C F7 7F"

Camera - ActionManager (ffxiv_dx11.exe + 0x01D3CE60)
0x21500CE1D80-0x7FF76CADCE60
= -0x7DE26BDFB0E0


anyway (for Native Toast Notifications)
1405C82B0 ClientUIUIModule_vf148 (5.45_Hotfix)
probably is CreateWideText
but have fun with the args
a3 is layer
11 is lobby text, 10 is front, everything else is back(?)
a2 is just the string
a4 and a5 are bools
a6 is put into a uint AtkValue but who knows what it is
anyway i would bp that function and see if its called for what you expect to be called for
uimodule has some other vfuncs to open other kinds of on-screen text
ooks like first bool is false, second bool is true
the unknown int has been 4017, 3379, 399, and 398 so far
I called with 4017 (LogMessage Key)
also called with 40,000 in case it was somehow related to duration, but that did nothing
you can give the function 0 and it's fine, so good to know
https://git.sr.ht/~jkcclemens/XivCommon/tree/master/item/XivCommon/Functions/Toast.cs