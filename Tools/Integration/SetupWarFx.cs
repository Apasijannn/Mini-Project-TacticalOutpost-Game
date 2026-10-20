using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEditor;
using UnityEditor.SceneManagement;
using Outpost.Player;

public static class SetupWarFx
{
    const string Root="Assets/TacticalOutpost/Gameplay/Effects/WarFX";
    static readonly Dictionary<Material,Material> Materials=new Dictionary<Material,Material>();

    static void Folder(string path)
    {
        if(AssetDatabase.IsValidFolder(path))return;
        int split=path.LastIndexOf('/');Folder(path.Substring(0,split));
        AssetDatabase.CreateFolder(path.Substring(0,split),path.Substring(split+1));
    }

    public static Texture AlphaTexture(Texture source)
    {
        if(!(source is Texture2D tex)||tex.format!=TextureFormat.Alpha8)return source;
        Folder(Root+"/Textures");
        string path=Root+"/Textures/"+tex.name+" RGBA.png";
        var existing=AssetDatabase.LoadAssetAtPath<Texture2D>(path);if(existing)return existing;
        var rt=RenderTexture.GetTemporary(tex.width,tex.height,0);
        var old=RenderTexture.active;var copy=new Texture2D(tex.width,tex.height,TextureFormat.RGBA32,false);
        try
        {
            Graphics.Blit(tex,rt);RenderTexture.active=rt;
            copy.ReadPixels(new Rect(0,0,tex.width,tex.height),0,0);copy.Apply();
            var pixels=copy.GetPixels();
            for(int i=0;i<pixels.Length;i++)pixels[i]=new Color(1,1,1,pixels[i].a);
            copy.SetPixels(pixels);copy.Apply();
            System.IO.File.WriteAllBytes(path,copy.EncodeToPNG());AssetDatabase.ImportAsset(path);
            return AssetDatabase.LoadAssetAtPath<Texture2D>(path);
        }
        finally{RenderTexture.active=old;RenderTexture.ReleaseTemporary(rt);UnityEngine.Object.DestroyImmediate(copy);}
    }

    public static int FixAlphaTextures()
    {
        int count=0;
        foreach(var guid in AssetDatabase.FindAssets("t:Material",new[]{Root+"/Materials"}))
        {
            var material=AssetDatabase.LoadAssetAtPath<Material>(AssetDatabase.GUIDToAssetPath(guid));
            var old=material.GetTexture("_BaseMap");var converted=AlphaTexture(old);
            if(old!=converted){material.SetTexture("_BaseMap",converted);EditorUtility.SetDirty(material);count++;}
        }
        AssetDatabase.SaveAssets();return count;
    }

    static GameObject Effect(string source,string name,bool muzzle,float scale)
    {
        var asset=AssetDatabase.LoadAssetAtPath<GameObject>("Assets/JMO Assets/WarFX/_Effects/"+source+".prefab");
        if(!asset)throw new InvalidOperationException("Missing War FX prefab: "+source);
        var go=UnityEngine.Object.Instantiate(asset);go.name=name;go.SetActive(false);
        try
        {
            go.transform.localScale=Vector3.one*scale;
            if(muzzle)
            {
                foreach(var behaviour in go.GetComponentsInChildren<MonoBehaviour>(true))UnityEngine.Object.DestroyImmediate(behaviour);
                foreach(var light in go.GetComponentsInChildren<Light>(true))UnityEngine.Object.DestroyImmediate(light);
            }
            foreach(var ps in go.GetComponentsInChildren<ParticleSystem>(true))
            {
                ps.Stop(true,ParticleSystemStopBehavior.StopEmittingAndClear);
                var main=ps.main;main.loop=false;main.playOnAwake=!muzzle;main.scalingMode=ParticleSystemScalingMode.Hierarchy;
                if(muzzle){var emission=ps.emission;emission.rateOverTime=0;emission.SetBursts(new[]{new ParticleSystem.Burst(0f,1)});main.duration=.06f;}
            }
            foreach(var renderer in go.GetComponentsInChildren<ParticleSystemRenderer>(true))
            {
                renderer.sharedMaterials=renderer.sharedMaterials.Select(sourceMat=>
                {
                    if(!sourceMat)return null;
                    if(Materials.TryGetValue(sourceMat,out var existing))return existing;
                    existing=AssetDatabase.LoadAssetAtPath<Material>(Root+"/Materials/"+sourceMat.name+" URP.mat");
                    if(existing){Materials.Add(sourceMat,existing);return existing;}
                    var material=new Material(Shader.Find("Universal Render Pipeline/Particles/Unlit"));
                    material.name=sourceMat.name+" URP";
                    material.SetTexture("_BaseMap",AlphaTexture(sourceMat.mainTexture));
                    material.SetColor("_BaseColor",sourceMat.HasProperty("_TintColor")?sourceMat.GetColor("_TintColor")*2:Color.white);
                    bool additive=sourceMat.shader.name.Contains("Additive");
                    material.SetFloat("_Surface",1);material.SetFloat("_Blend",additive?2:0);
                    material.SetFloat("_SrcBlend",(float)BlendMode.SrcAlpha);
                    material.SetFloat("_DstBlend",(float)(additive?BlendMode.One:BlendMode.OneMinusSrcAlpha));
                    material.SetFloat("_ZWrite",0);material.SetFloat("_Cull",0);
                    material.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
                    material.SetOverrideTag("RenderType","Transparent");material.renderQueue=3000;
                    AssetDatabase.CreateAsset(material,Root+"/Materials/"+material.name+".mat");
                    Materials.Add(sourceMat,material);return material;
                }).ToArray();
            }
            go.SetActive(true);
            return PrefabUtility.SaveAsPrefabAsset(go,Root+"/"+name+".prefab");
        }
        finally{UnityEngine.Object.DestroyImmediate(go);}
    }

    public static object Apply()
    {
        if(EditorApplication.isPlaying)throw new InvalidOperationException("Exit Play Mode first.");
        if(AssetDatabase.LoadAssetAtPath<GameObject>(Root+"/Enemy impact URP.prefab"))throw new InvalidOperationException("War FX setup already exists.");
        Folder(Root+"/Materials");
        var rifle=Effect("MuzzleFlashes/FPS/WFX_MF FPS RIFLE1","Rifle muzzle URP",true,.55f);
        var pistol=Effect("MuzzleFlashes/FPS/WFX_MF FPS RIFLE2","Pistol muzzle URP",true,.4f);
        var impact=Effect("Bullet Impacts/WFX_BImpact Concrete","Surface impact URP",false,.3f);
        var flesh=Effect("Bullet Impacts/WFX_BImpact SoftBody","Enemy impact URP",false,.25f);
        string path="Assets/TacticalOutpost/Gameplay/Prefabs/Adapted/Player_Industrial.prefab";
        var player=PrefabUtility.LoadPrefabContents(path);
        int count=0;
        try
        {
            foreach(var weapon in player.GetComponentsInChildren<FpsWeapon>(true))
            {
                if(!weapon.muzzle)throw new InvalidOperationException("Weapon has no muzzle: "+weapon.name);
                var go=(GameObject)PrefabUtility.InstantiatePrefab(weapon.kind==WeaponKind.Rifle?rifle:pistol,weapon.muzzle);
                go.transform.localPosition=Vector3.zero;go.transform.localRotation=Quaternion.identity;
                weapon.muzzleFlash=go.GetComponent<ParticleSystem>();
                weapon.impactPrefab=impact;weapon.enemyImpactPrefab=flesh;count++;
            }
            PrefabUtility.SaveAsPrefabAsset(player,path,out bool saved);
            if(!saved||count!=2)throw new InvalidOperationException("Player VFX save failed.");
        }
        finally{PrefabUtility.UnloadPrefabContents(player);}
        var scene=UnityEngine.SceneManagement.SceneManager.GetSceneByPath("Assets/Scenes/Gameplay/TacticalOutpost_Game.unity");
        EditorSceneManager.MarkSceneDirty(scene);EditorSceneManager.SaveScene(scene);AssetDatabase.SaveAssets();
        return new{weapons=count,materials=Materials.Count};
    }
}
