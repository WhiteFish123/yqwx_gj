using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;

//一次性构建爱丽丝敌人的资源：精灵导入设置、动画片段、AnimatorController、Prefab
public static class AliceEnemyBuilder
{
    private const string ArtRoot = "Assets/Animations/Enemy/animations/Alice";
    private const string ClipDir = "Assets/Animations/Enemy/Alice";
    private const string ControllerPath = "Assets/Animations/AnimatorControllers/Enemy_Alice.controller";
    private const string SlimePrefabPath = "Assets/Prefab/Enemy/Enemy_Slime.prefab";
    private const string AlicePrefabPath = "Assets/Prefab/Enemy/Enemy_Alice.prefab";
    private const float Ppu = 16f;

    private static readonly Dictionary<string, Vector2> Pivots = new Dictionary<string, Vector2>
    {
        { "Front often", new Vector2(0.4615f, 0.4148f) },
        { "Back often", new Vector2(0.5100f, 0.2807f) },
        { "Left right often", new Vector2(0.5702f, 0.2500f) },
        { "Front charge", new Vector2(0.4839f, 0.4148f) },
        { "Backcharge", new Vector2(0.4250f, 0.5897f) },
        { "Left right charge", new Vector2(0.2955f, 0.2500f) },
        { "Crash", new Vector2(0.5000f, 0.5000f) },
    };

    private static readonly Dictionary<string, string[]> Folders = new Dictionary<string, string[]>
    {
        { "Front often", new[] { "1.png", "2.png", "3.png", "4.png" } },
        { "Back often", new[] { "a.png", "b.png", "c.png", "d.png", "e.png", "f.png", "g.png", "h.png" } },
        { "Left right often", new[] { "1.png", "2.png", "3.png", "4.png", "5.png" } },
        { "Front charge", new[] { "a.png", "b.png", "c.png", "d.png", "e.png", "f.png", "g.png", "h.png", "i.png", "j.png", "k.png", "l.png", "m.png", "n.png", "o.png", "p.png" } },
        { "Backcharge", new[] { "a.png", "b.png", "c.png", "d.png", "e.png", "f.png", "g.png", "h.png", "i.png", "j.png", "k.png", "l.png", "m.png", "n.png", "o.png", "p.png" } },
        { "Left right charge", new[] { "图层 1.png", "a.png", "b.png", "c.png", "d.png", "e.png", "f.png", "g.png", "h.png", "i.png", "j.png", "k.png", "l.png", "m.png", "n.png", "o.png", "p.png" } },
        { "Crash", new[] { "1.png", "2.png", "3.png", "4.png", "5.png", "6.png", "7.png", "8.png", "9.png", "10.png", "11.png" } },
    };

    public static string Build()
    {
        var log = new StringBuilder();
        ConfigureSprites(log);
        var clips = BuildClips(log);
        var controller = BuildController(clips, log);
        BuildPrefab(controller, log);
        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
        return log.ToString();
    }

    // ---------- 1. 导入设置 ----------
    private static void ConfigureSprites(StringBuilder log)
    {
        foreach (var folder in Folders.Keys)
        {
            foreach (var file in Folders[folder])
            {
                string path = ArtRoot + "/" + folder + "/" + file;
                var importer = AssetImporter.GetAtPath(path) as TextureImporter;
                if (importer == null)
                {
                    log.Append("MISSING ").Append(path).Append('\n');
                    continue;
                }

                importer.textureType = TextureImporterType.Sprite;
                importer.spriteImportMode = SpriteImportMode.Single;
                importer.spritePixelsPerUnit = Ppu;
                importer.filterMode = FilterMode.Point;
                importer.mipmapEnabled = false;
                importer.alphaIsTransparency = true;
                importer.maxTextureSize = 2048;
                importer.spritePivot = Pivots[folder];
                importer.SaveAndReimport();
            }
        }

        log.Append("sprites configured: ").Append(Folders.Values.Sum(f => f.Length)).Append('\n');
    }

    // ---------- 2. 动画片段 ----------
    private static Dictionary<string, AnimationClip> BuildClips(StringBuilder log)
    {
        if (!AssetDatabase.IsValidFolder(ClipDir))
            AssetDatabase.CreateFolder("Assets/Animations/Enemy", "Alice");

        var clips = new Dictionary<string, AnimationClip>();

        AnimationClip Add(string clipName, string folder, float fps, bool loop)
        {
            string path = ClipDir + "/" + clipName + ".anim";
            AssetDatabase.DeleteAsset(path);

            var clip = new AnimationClip { frameRate = fps };
            var settings = AnimationUtility.GetAnimationClipSettings(clip);
            settings.loopTime = loop;
            AnimationUtility.SetAnimationClipSettings(clip, settings);

            string[] files = Folders[folder];
            var keys = new ObjectReferenceKeyframe[files.Length];
            for (int i = 0; i < files.Length; i++)
            {
                string spritePath = ArtRoot + "/" + folder + "/" + files[i];
                var sprite = AssetDatabase.LoadAssetAtPath<Sprite>(spritePath);
                if (sprite == null)
                    log.Append("NO SPRITE ").Append(spritePath).Append('\n');

                keys[i] = new ObjectReferenceKeyframe { time = i / fps, value = sprite };
            }

            var binding = new EditorCurveBinding
            {
                path = string.Empty,
                type = typeof(SpriteRenderer),
                propertyName = "m_Sprite"
            };
            AnimationUtility.SetObjectReferenceCurve(clip, binding, keys);

            AssetDatabase.CreateAsset(clip, path);
            clips[clipName] = clip;
            log.Append("clip ").Append(clipName).Append(" frames=").Append(files.Length)
                .Append(" len=").Append((files.Length / fps).ToString("F2")).Append('\n');
            return clip;
        }

        Add("Alice_Idle_Side", "Left right often", 5f, true);
        Add("Alice_Idle_Front", "Front often", 5f, true);
        Add("Alice_Idle_Back", "Back often", 5f, true);
        Add("Alice_Charge_Side", "Left right charge", 20f, false);
        Add("Alice_Charge_Front", "Front charge", 20f, false);
        Add("Alice_Charge_Back", "Backcharge", 20f, false);
        Add("Alice_Crash", "Crash", 12f, false);

        return clips;
    }

    // ---------- 3. AnimatorController ----------
    private static AnimatorController BuildController(Dictionary<string, AnimationClip> clips, StringBuilder log)
    {
        AssetDatabase.DeleteAsset(ControllerPath);
        var controller = AnimatorController.CreateAnimatorControllerAtPath(ControllerPath);
        var stateMachine = controller.layers[0].stateMachine;

        for (int i = stateMachine.states.Length - 1; i >= 0; i--)
            stateMachine.RemoveState(stateMachine.states[i].state);

        AnimatorState first = null;
        foreach (var pair in clips)
        {
            var state = stateMachine.AddState(pair.Key);
            state.motion = pair.Value;
            if (pair.Key == "Alice_Idle_Side")
                first = state;
        }

        if (first != null)
            stateMachine.defaultState = first;

        log.Append("controller states=").Append(stateMachine.states.Length).Append('\n');
        return controller;
    }

    // ---------- 4. Prefab ----------
    private static void BuildPrefab(AnimatorController controller, StringBuilder log)
    {
        AssetDatabase.DeleteAsset(AlicePrefabPath);
        if (!AssetDatabase.CopyAsset(SlimePrefabPath, AlicePrefabPath))
        {
            log.Append("COPY FAILED\n");
            return;
        }

        AssetDatabase.Refresh();

        var root = PrefabUtility.LoadPrefabContents(AlicePrefabPath);
        try
        {
            root.name = "Enemy_Alice";

            var oldComponent = root.GetComponent<Enemy_Slime>();
            var oldSerialized = oldComponent != null ? new SerializedObject(oldComponent) : null;

            var alice = root.AddComponent<Enemy_Alice>();
            if (oldSerialized != null)
                CopySerialized(oldSerialized, new SerializedObject(alice));

            if (oldComponent != null)
                UnityEngine.Object.DestroyImmediate(oldComponent, true);

            var animRoot = root.transform.Find("Animation");
            if (animRoot == null)
            {
                log.Append("NO Animation child\n");
                return;
            }

            var animator = animRoot.GetComponent<Animator>();
            if (animator != null)
                animator.runtimeAnimatorController = controller;

            var spriteRenderer = animRoot.GetComponent<SpriteRenderer>();
            if (spriteRenderer != null)
                spriteRenderer.sprite = AssetDatabase.LoadAssetAtPath<Sprite>(ArtRoot + "/Left right often/1.png");

            var trail = animRoot.Find("TrailEffect");
            if (trail != null)
                UnityEngine.Object.DestroyImmediate(trail.gameObject, true);

            var collider = root.GetComponent<CapsuleCollider2D>();
            if (collider != null)
            {
                collider.direction = CapsuleDirection2D.Vertical;
                collider.size = new Vector2(0.7f, 0.9f);
                collider.offset = new Vector2(0f, 0.45f);
            }

            PrefabUtility.SaveAsPrefabAsset(root, AlicePrefabPath);
            log.Append("prefab written: ").Append(AlicePrefabPath).Append('\n');
        }
        finally
        {
            PrefabUtility.UnloadPrefabContents(root);
        }
    }

    //把旧组件里同名的序列化字段逐个抄到新组件（只处理简单类型）
    private static void CopySerialized(SerializedObject from, SerializedObject to)
    {
        var iterator = from.GetIterator();
        bool enterChildren = true;

        while (iterator.NextVisible(enterChildren))
        {
            enterChildren = false;

            if (iterator.name == "m_Script" || iterator.propertyPath.Contains("["))
                continue;

            var target = to.FindProperty(iterator.propertyPath);
            if (target == null || target.propertyType != iterator.propertyType)
                continue;

            switch (iterator.propertyType)
            {
                case SerializedPropertyType.Integer: target.intValue = iterator.intValue; break;
                case SerializedPropertyType.Boolean: target.boolValue = iterator.boolValue; break;
                case SerializedPropertyType.Float: target.floatValue = iterator.floatValue; break;
                case SerializedPropertyType.String: target.stringValue = iterator.stringValue; break;
                case SerializedPropertyType.Color: target.colorValue = iterator.colorValue; break;
                case SerializedPropertyType.ObjectReference: target.objectReferenceValue = iterator.objectReferenceValue; break;
                case SerializedPropertyType.LayerMask: target.intValue = iterator.intValue; break;
                case SerializedPropertyType.Enum: target.enumValueIndex = iterator.enumValueIndex; break;
                case SerializedPropertyType.Vector2: target.vector2Value = iterator.vector2Value; break;
                case SerializedPropertyType.Vector3: target.vector3Value = iterator.vector3Value; break;
                case SerializedPropertyType.Vector2Int: target.vector2IntValue = iterator.vector2IntValue; break;
                case SerializedPropertyType.Vector3Int: target.vector3IntValue = iterator.vector3IntValue; break;
            }
        }

        to.ApplyModifiedPropertiesWithoutUndo();
    }
}
