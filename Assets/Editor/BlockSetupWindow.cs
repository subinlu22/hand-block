using System.Collections.Generic;
using UnityEditor;
using UnityEditorInternal;
using UnityEngine;

// Tools > Block Setup
// 가져온 블럭 모델(blocks.obj)을 쓸 수 있는 블럭들로 바꿔준다.
// 1) 블럭마다 피벗을 블럭 가운데로 옮기고
// 2) 기준 블럭(지금 쓰는 블럭)의 태그, 레이어, Rigidbody, 스크립트 등을 똑같이 붙이고
// 3) Box Collider 를 블럭 크기에 맞게 붙이고
// 4) (선택) 나무 Material 을 복사해서 원래 색으로 칠해준다.
public class BlockSetupWindow : EditorWindow
{
    GameObject modelRoot;
    GameObject referenceBlock;
    Material woodTemplate;
    bool hideOriginal = true;

    [MenuItem("Tools/Block Setup")]
    static void Open() => GetWindow<BlockSetupWindow>("Block Setup");

    void OnGUI()
    {
        EditorGUILayout.HelpBox(
            "1. Hierarchy 에 꺼내놓은 blocks 모델을 '모델'에 넣으세요.\n" +
            "2. 지금 잘 동작하는 블럭 하나를 '기준 블럭'에 넣으세요.\n" +
            "3. (선택) 만들어둔 나무 Material 을 넣으면 원래 색으로 나무를 칠해줍니다.",
            MessageType.Info);

        modelRoot = (GameObject)EditorGUILayout.ObjectField("모델 (Hierarchy)", modelRoot, typeof(GameObject), true);
        referenceBlock = (GameObject)EditorGUILayout.ObjectField("기준 블럭", referenceBlock, typeof(GameObject), true);
        woodTemplate = (Material)EditorGUILayout.ObjectField("나무 Material (선택)", woodTemplate, typeof(Material), false);
        hideOriginal = EditorGUILayout.Toggle("원본 모델 숨기기", hideOriginal);

        GUI.enabled = modelRoot != null && referenceBlock != null;
        if (GUILayout.Button("블럭 만들기", GUILayout.Height(30))) Build();
        GUI.enabled = true;
    }

    void Build()
    {
        const string dir = "Assets/BlockMeshes";
        if (!AssetDatabase.IsValidFolder(dir)) AssetDatabase.CreateFolder("Assets", "BlockMeshes");

        var parent = new GameObject("Blocks");
        Undo.RegisterCreatedObjectUndo(parent, "Block Setup");
        parent.transform.position = modelRoot.transform.position;

        var colorMats = new Dictionary<Material, Material>();
        var refCollider = referenceBlock.GetComponent<Collider>();
        int count = 0;

        foreach (var mf in modelRoot.GetComponentsInChildren<MeshFilter>(true))
        {
            if (mf.sharedMesh == null) continue;
            var src = mf.sharedMesh;
            var mesh = Instantiate(src);
            mesh.name = mf.name;

            Vector3 c = mesh.bounds.center;
            var verts = mesh.vertices;
            for (int i = 0; i < verts.Length; i++) verts[i] -= c;
            mesh.vertices = verts;
            mesh.RecalculateBounds();
            if (mesh.tangents == null || mesh.tangents.Length == 0) mesh.RecalculateTangents();

            AssetDatabase.CreateAsset(mesh, AssetDatabase.GenerateUniqueAssetPath($"{dir}/{mf.name}.asset"));

            var t = mf.transform;
            var go = new GameObject(mf.name);
            Undo.RegisterCreatedObjectUndo(go, "Block Setup");
            go.transform.SetParent(parent.transform, true);
            go.transform.position = t.TransformPoint(c);
            go.transform.rotation = t.rotation;
            go.transform.localScale = t.lossyScale;
            go.tag = referenceBlock.tag;
            go.layer = referenceBlock.layer;

            go.AddComponent<MeshFilter>().sharedMesh = mesh;
            var mr = go.AddComponent<MeshRenderer>();
            var srcMr = mf.GetComponent<MeshRenderer>();
            var mats = srcMr != null ? srcMr.sharedMaterials : new Material[0];
            if (woodTemplate != null)
            {
                for (int i = 0; i < mats.Length; i++) mats[i] = GetWoodColor(mats[i], colorMats, dir);
            }
            mr.sharedMaterials = mats;

            // 기준 블럭의 컴포넌트 복사 (Transform, 메쉬, 콜라이더 제외)
            foreach (var comp in referenceBlock.GetComponents<Component>())
            {
                if (comp == null || comp is Transform || comp is MeshFilter || comp is Renderer || comp is Collider) continue;
                if (ComponentUtility.CopyComponent(comp)) ComponentUtility.PasteComponentAsNew(go);
            }

            var box = go.AddComponent<BoxCollider>(); // 메쉬 크기에 자동으로 맞춰짐
            if (refCollider != null) box.sharedMaterial = refCollider.sharedMaterial;
            count++;
        }

        if (hideOriginal)
        {
            Undo.RecordObject(modelRoot, "Block Setup");
            modelRoot.SetActive(false);
        }

        AssetDatabase.SaveAssets();
        Selection.activeGameObject = parent;
        Debug.Log($"[Block Setup] 블럭 {count}개를 만들었어요. (Blocks 오브젝트 아래)");
    }

    Material GetWoodColor(Material original, Dictionary<Material, Material> cache, string dir)
    {
        if (original == null) return woodTemplate;
        if (cache.TryGetValue(original, out var m)) return m;

        Color col = original.HasProperty("_BaseColor") ? original.GetColor("_BaseColor")
                  : original.HasProperty("_Color") ? original.GetColor("_Color") : Color.white;
        m = new Material(woodTemplate) { name = "Wood_" + original.name };
        if (m.HasProperty("_BaseColor")) m.SetColor("_BaseColor", col);
        if (m.HasProperty("_Color")) m.SetColor("_Color", col);
        AssetDatabase.CreateAsset(m, AssetDatabase.GenerateUniqueAssetPath($"{dir}/{m.name}.mat"));
        cache[original] = m;
        return m;
    }
}
