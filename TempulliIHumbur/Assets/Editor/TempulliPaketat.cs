using System.Linq;
using UnityEditor;
using UnityEditor.PackageManager;
using UnityEditor.PackageManager.Requests;
using UnityEngine;

// Instalon paketat falas të Unity-t që përdor Tempulli: ProBuilder, VFX Graph (Shader Graph vjen me URP).
// Polybrush 1.2.1 NUK përputhet me Unity 6.6 (gabime kompilimi), prandaj hiqet nëse gjendet.
// Menuja: Tempulli > Instalo paketat. Herën e parë ekzekutohet vetë.
[InitializeOnLoad]
public static class TempulliPaketat
{
    static readonly string[] Wanted = { "com.unity.probuilder", "com.unity.visualeffectgraph", "com.unity.animation.rigging", "com.unity.cinemachine" };
    static readonly string[] Unwanted = { "com.unity.polybrush" };
    static ListRequest list;
    static AddAndRemoveRequest add;

    static TempulliPaketat()
    {
        if (!SessionState.GetBool("TempulliPaketatKontrolluar2", false))
        {
            SessionState.SetBool("TempulliPaketatKontrolluar2", true);
            EditorApplication.delayCall += Check;
        }
    }

    [MenuItem("Tempulli/Instalo paketat (ProBuilder, VFX Graph)")]
    public static void Check()
    {
        list = Client.List(true, false);
        EditorApplication.update += OnList;
    }

    static void OnList()
    {
        if (!list.IsCompleted) return;
        EditorApplication.update -= OnList;
        if (list.Status != StatusCode.Success) { Debug.LogWarning("Tempulli: s'u lexua lista e paketave: " + list.Error?.message); return; }
        var have = list.Result.Select(p => p.name).ToHashSet();
        var missing = Wanted.Where(w => !have.Contains(w)).ToArray();
        foreach (var p in list.Result.Where(p => Wanted.Contains(p.name) || p.name == "com.unity.shadergraph"))
            Debug.Log("Tempulli: paketa " + p.name + " " + p.version + " është e instaluar.");
        var remove = Unwanted.Where(u => have.Contains(u)).ToArray();
        if (missing.Length == 0 && remove.Length == 0) return;
        Debug.Log("Tempulli: po instalohen " + string.Join(", ", missing) + (remove.Length > 0 ? " | hiqen " + string.Join(", ", remove) : ""));
        add = Client.AddAndRemove(missing, remove);
        EditorApplication.update += OnAdd;
    }

    static void OnAdd()
    {
        if (!add.IsCompleted) return;
        EditorApplication.update -= OnAdd;
        if (add.Status == StatusCode.Success)
            foreach (var p in add.Result) Debug.Log("Tempulli: u instalua " + p.name + " " + p.version);
        else Debug.LogError("Tempulli: instalimi dështoi: " + add.Error?.message);
    }
}
