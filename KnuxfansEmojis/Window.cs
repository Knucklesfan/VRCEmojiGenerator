#if UNITY_EDITOR
using UnityEngine;
using UnityEditor;
using System.Collections.Generic;
using System.IO;
using UnityEditor.Animations;
using VRC.SDK3.Avatars.ScriptableObjects;
using VRC.SDK3.Avatars.Components;

public class Emoji
{
    public Texture2D texture;
    public string name;
    public Emoji()
    {
        name = "";
        texture = null;
    }
}

public class MyWindow : EditorWindow
{
    string filename = "MyEmojiPack";
    bool groupEnabled;
    int width = 256;
    int height = 256;
    int size = 0;
    Vector2 scrollPos;
    List<Emoji> emojis = new List<Emoji>();
    Shader shader;
    VRCAvatarDescriptor avatarDescriptor;

    [MenuItem("Window/Emoji Generator")]
    static void Init()
    {
        MyWindow window = (MyWindow)EditorWindow.GetWindow(typeof(MyWindow));
        window.Show();
    }

    void OnGUI()
    {
        GUILayout.Label("Base Settings", EditorStyles.boldLabel);
        filename = EditorGUILayout.TextField("Filename", filename);

        groupEnabled = EditorGUILayout.BeginToggleGroup("Texture size settings", groupEnabled);
        width = EditorGUILayout.IntField("Width of Texture:", width);
        height = EditorGUILayout.IntField("Height of Texture:", height);
        GUILayout.Label("Maximum emojis at this resolution: " + 16777216 / (width * height), EditorStyles.whiteLabel);
        EditorGUILayout.EndToggleGroup();

        size = EditorGUILayout.IntField("Number of Emojis", size);
        GUILayout.Label("Emojis", EditorStyles.boldLabel);
        scrollPos = EditorGUILayout.BeginScrollView(scrollPos, GUILayout.Height(500));
        for (int i = 0; i < size; i++)
        {
            if (size > emojis.Count)
            {
                emojis.Add(new Emoji());
            }
            emojis[i].name = EditorGUILayout.TextField("Emoji #" + (i + 1) + " Name:", emojis[i].name);
            GUILayout.BeginHorizontal();
            if (GUILayout.Button("Open Texture"))
            {
                string path = EditorUtility.OpenFilePanel("Pick a folder", "", "png,jpg");
                emojis[i] = genEmoji(path, emojis[i].name, i);
                Repaint();

            }
            GUI.enabled = false;
            EditorGUILayout.ObjectField("", emojis[i].texture, typeof(Texture2D), false);
            GUI.enabled = true;
            GUILayout.EndHorizontal();
        }


        EditorGUILayout.EndScrollView();


        if (GUILayout.Button("Grab from entire folder"))
        {
            string path = EditorUtility.OpenFolderPanel("Pick a folder", "", "");
            if (path != null)
            {
                string[] files = Directory.GetFiles(path);
                emojis = new List<Emoji>();
                for (int i = 0; i < files.Length; i++)
                {
                    if (files[i].EndsWith("png") || files[i].EndsWith("jpg"))
                    {
                        emojis.Add(genEmoji(files[i], "", i));
                    }
                }
                size = emojis.Count;
            }
        }

        shader = EditorGUILayout.ObjectField("Emoji Particle Shader: ", shader, typeof(Shader), false) as Shader;
        GUILayout.Label("I recommend picking either VRChat Standard Lite if for quest or an unlit shader on PC. Must have an emission channel if not unlit.", EditorStyles.whiteLabel);
        avatarDescriptor = EditorGUILayout.ObjectField("VRC Avatar Descriptor:", avatarDescriptor, typeof(VRCAvatarDescriptor), true) as VRCAvatarDescriptor;
        GUILayout.Label("If left unspecified, then check in the Assets/Emojis folder for the generated menus, animations, material, textures and the animation controller.", EditorStyles.whiteLabel);


        if (GUILayout.Button("Generate Animations, Animation Controllers, Atlas, and Menus"))
        {

            //putting this at the VERY START to cover some bases

            AnimatorController layers;
            if(avatarDescriptor != null && avatarDescriptor.baseAnimationLayers[4].animatorController != null)
            {
                layers = avatarDescriptor.baseAnimationLayers[4].animatorController as AnimatorController;
            }
            else
            {
                layers = AnimatorController.CreateAnimatorControllerAtPath("Assets/Emojis/AnimationController/anim.controller");
            }

            var layer = new UnityEditor.Animations.AnimatorControllerLayer
            {
                name = "Emojis",
                defaultWeight = 1f,
                stateMachine = new UnityEditor.Animations.AnimatorStateMachine() // Make sure to create a StateMachine as well, as a default one is not created
            };

            layers.AddLayer(layer);


            //next, begin generating texture atlas


            Texture2D texture = new Texture2D(4096, 4096, TextureFormat.ARGB32, false);
            for (int emo = 0; emo < size; emo++)
            {
                Texture2D tex = emojis[emo].texture;
                for (int i = 0; i < tex.width; i++)
                {
                    for (int j = 0; j < tex.height; j++)
                    {
                        texture.SetPixel(i + (emo * tex.width), j + (emo / (4096 / tex.height) * tex.height), tex.GetPixel(i, j));
                    }
                }
            }

            texture.Compress(false);
            AssetDatabase.CreateAsset(texture, "Assets/Emojis/Texture.asset");
            Material mat = new Material(shader);
            mat.SetTexture("_MainTex", texture);
            mat.SetFloat("_EnableEmission", 1);
            mat.SetFloat("_Glossiness", 1);
            mat.SetFloat("_Metallic", 1);

            mat.SetTexture("_EmissionMap", texture);
            mat.SetColor("_EmissionColor", Color.white);
            AssetDatabase.CreateAsset(mat, "Assets/Emojis/EmojiMaterial.mat");

            AnimationClip based = new AnimationClip();
            based.SetCurve("emoji", typeof(GameObject), "m_IsActive", AnimationCurve.Constant(0, 5, 0));
            AssetDatabase.CreateAsset(based, "Assets/Emojis/emojiRebirthDefault.anim");

            List<AnimationClip> animationClips = new List<AnimationClip>();
            for (int i = 0; i < size; i++)
            {
                AnimationClip clip = new AnimationClip();
                float maxw = (float)(4096 / width);
                AnimationCurve toggle = AnimationCurve.Constant(0, 5, 1);
                float x = (int)(i % maxw) / maxw;
                float y = (int)(i / maxw) / maxw;
                Debug.Log(x + " " + y);
                AnimationCurve posx = AnimationCurve.Constant(0, 5, x);
                AnimationCurve posy = AnimationCurve.Constant(0, 5, y);
                AnimationCurve res = AnimationCurve.Constant(0, 5, 0.0625f);

                clip.SetCurve("emoji", typeof(GameObject), "m_IsActive", toggle);
                clip.SetCurve("emoji", typeof(ParticleSystemRenderer), "material._MainTex_ST.x", res);
                clip.SetCurve("emoji", typeof(ParticleSystemRenderer), "material._MainTex_ST.y", res);
                clip.SetCurve("emoji", typeof(ParticleSystemRenderer), "material._MainTex_ST.z", posx);
                clip.SetCurve("emoji", typeof(ParticleSystemRenderer), "material._MainTex_ST.w", posy);
                animationClips.Add(clip);
                AssetDatabase.CreateAsset(clip, "Assets/Emojis/Animations/emojiRebirth" + i + ".anim");
            }
            layers.AddParameter("emoji", AnimatorControllerParameterType.Int);

            var rootStateMachine = layers.layers[layers.layers.Length - 1].stateMachine;

            AnimatorState wait = rootStateMachine.AddState("Wait For Emoji");
            wait.motion = based;
            rootStateMachine.defaultState = wait;
            for (int i = 0; i < size; i++)
            {
                AnimatorState temp = rootStateMachine.AddState("Emoji #" + i);
                temp.motion = animationClips[i];
                wait.AddTransition(temp, false).AddCondition(AnimatorConditionMode.Equals,(float)i+1,"emoji");
                temp.AddExitTransition().hasExitTime = true;

            }


            int nummenus = (size) / 8;
            int currentemoji = 0;
            VRCExpressionsMenu.Control.Parameter emojiparam = new VRCExpressionsMenu.Control.Parameter();
            emojiparam.name = "emoji";
            List<VRCExpressionsMenu> menus = new List<VRCExpressionsMenu>();
            VRCExpressionsMenu menuofmenus = new VRCExpressionsMenu(); ;
            for (int men = 0; men < nummenus && currentemoji < size; ++men) {
                menuofmenus = new VRCExpressionsMenu();
                for (int i = 0; i < 8 && currentemoji < size; i++)
                {
                    VRCExpressionsMenu menu1 = new VRCExpressionsMenu();
                    for (int j = 0; j < 8 && currentemoji < size; j++)
                    {
                        VRCExpressionsMenu.Control control = new VRCExpressionsMenu.Control();
                        control.name = emojis[currentemoji].name;
                        control.type = VRCExpressionsMenu.Control.ControlType.Button;
                        control.icon = emojis[currentemoji].texture;
                        control.parameter = emojiparam;
                        control.value = currentemoji + 1;
                        menu1.controls.Add(control);
                        currentemoji++;
                    }
                    AssetDatabase.CreateAsset(menu1, "Assets/Emojis/Menus/Menu" + i + ".asset");
                    VRCExpressionsMenu.Control ctrl = new VRCExpressionsMenu.Control();
                    ctrl.name = "Menu #" + men;
                    ctrl.type = VRCExpressionsMenu.Control.ControlType.SubMenu;
                    ctrl.icon = emojis[currentemoji-1].texture;
                    ctrl.subMenu = menu1;
                    menuofmenus.controls.Add(ctrl);
                    men++;
                }
                AssetDatabase.CreateAsset(menuofmenus, "Assets/Emojis/Menus/BigMenu" + men + ".asset");
            }
            //var control = new ExpressionsMenu.Control();
            //menu.controls.Add()
            VRCExpressionParameters parameters = new VRCExpressionParameters();
            parameters.parameters = new VRCExpressionParameters.Parameter[1];
            parameters.parameters[0] = new VRCExpressionParameters.Parameter();
            parameters.parameters[0].defaultValue = 0;
            parameters.parameters[0].name = "emoji";
            parameters.parameters[0].valueType = VRCExpressionParameters.ValueType.Int;
            parameters.parameters[0].saved = false;


            //getting everything together since now we have all we need to complete the infinity stones


            // i mean um
            // the avatar

            if(avatarDescriptor != null)
            {
                if(avatarDescriptor.baseAnimationLayers[4].animatorController == null)
                {
                    avatarDescriptor.baseAnimationLayers[4].animatorController = layers;
                }
                avatarDescriptor.expressionsMenu = menuofmenus;
                avatarDescriptor.expressionParameters = parameters;
                
            }
            

            }
    }

    //I wrote this, it basically grabs from a given path
    private Emoji genEmoji(string path, string name, int place)
    {
        Emoji emoji = new Emoji();
        if (path.EndsWith("png") || path.EndsWith("jpg"))
        {
            var fileContent = File.ReadAllBytes(path);
            if(name == "" || name == null)
            {
                emoji.name = path.Substring(path.LastIndexOf("/") + 1, path.LastIndexOf(".") - (path.LastIndexOf("/") + 1));
            }
            else
            {
                emoji.name = name;
            }
            Texture2D texture = new Texture2D(256, 256);
            texture.LoadImage(fileContent);
            texture = ScaleTexture(texture, width, height);
            emoji.texture = texture;
            AssetDatabase.CreateAsset(emoji.texture, "Assets/Emojis/Temps/Emoji" + place + ".asset");

        }

        return emoji;

    }






    //this code was borrowed from Helical on this unity answers page, thanks for your help,
    //although now that I understand how this works I plan to rewrite this in the future
    //https://answers.unity.com/questions/150942/texture-scale.html
    private Texture2D ScaleTexture(Texture2D source, int targetWidth, int targetHeight)
    {
        Texture2D result = new Texture2D(targetWidth, targetHeight, source.format, false);
        float incX = (1.0f / (float)targetWidth);
        float incY = (1.0f / (float)targetHeight);
        for (int i = 0; i < result.height; ++i)
        {
            for (int j = 0; j < result.width; ++j)
            {
                Color newColor = source.GetPixelBilinear((float)j / (float)result.width, (float)i / (float)result.height);
                result.SetPixel(j, i, newColor);
            }
        }
        result.Apply();
        return result;
    }

}
#endif