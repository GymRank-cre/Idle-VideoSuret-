using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

// Vue libre (11e) : bouton carré orange « Vue » (loupe) sous « Réputation », fiche de machine en bas, flèche qui désigne une dalle.
public static class FreeViewBuilder
{
    public static void Build()
    {
        GameObject canvas = GameObject.Find("Canvas");
        Transform safe = canvas != null ? canvas.transform.Find("HudSafeArea") : null;
        CameraFollow cameraFollow = Object.FindFirstObjectByType<CameraFollow>();
        PlayerController player = Object.FindFirstObjectByType<PlayerController>();
        if (safe == null || cameraFollow == null || player == null) return;

        foreach (string old in new[] { "ViewButton", "ViewInfo", "ViewHost" })
        {
            Transform t = safe.Find(old);
            if (t != null) Object.DestroyImmediate(t.gameObject);
        }
        GameObject oldPointer = GameObject.Find("ViewPointer");
        if (oldPointer != null) Object.DestroyImmediate(oldPointer);

        Sprite rounded = UiSprites.Rounded();
        Sprite circle = AssetDatabase.GetBuiltinExtraResource<Sprite>("UI/Skin/Knob.psd");

        // bouton carré orange à loupe
        GameObject btnGo = new GameObject("ViewButton", typeof(RectTransform), typeof(Button));
        btnGo.transform.SetParent(safe, false);
        RectTransform button = btnGo.GetComponent<RectTransform>();
        button.anchorMin = button.anchorMax = button.pivot = new Vector2(1f, 1f);
        button.anchoredPosition = new Vector2(-40f, -1100f);
        button.sizeDelta = new Vector2(150f, 150f);
        RectTransform baseRt = UpgradeBuilder.Box("Base", button, rounded, new Color32(0xC2, 0x53, 0x0A, 0xFF), new Vector2(0f, -14f), new Vector2(150f, 150f), false);
        baseRt.GetComponent<Image>().pixelsPerUnitMultiplier = 0.9f;
        RectTransform face = UpgradeBuilder.Box("Face", button, rounded, UiTheme.Orange, Vector2.zero, new Vector2(150f, 150f), true);
        face.GetComponent<Image>().pixelsPerUnitMultiplier = 0.9f;
        btnGo.GetComponent<Button>().targetGraphic = face.GetComponent<Image>();
        Object.DestroyImmediate(face.GetComponent<Button>());
        // loupe : anneau blanc, verre, manche incliné
        UpgradeBuilder.Box("Ring", face, circle, Color.white, new Vector2(-8f, 10f), new Vector2(70f, 70f), false);
        UpgradeBuilder.Box("Glass", face, circle, new Color32(0xBF, 0xD9, 0xFF, 0xFF), new Vector2(-8f, 10f), new Vector2(50f, 50f), false);
        UpgradeBuilder.Rot(UpgradeBuilder.Box("Handle", face, rounded, Color.white, new Vector2(26f, -26f), new Vector2(14f, 40f), false), 45f);
        btnGo.AddComponent<PopIn>();

        // fiche de machine / de dalle
        GameObject infoGo = new GameObject("ViewInfo", typeof(RectTransform), typeof(Image));
        infoGo.transform.SetParent(safe, false);
        RectTransform info = infoGo.GetComponent<RectTransform>();
        info.anchorMin = info.anchorMax = info.pivot = new Vector2(0.5f, 0f);
        info.anchoredPosition = new Vector2(0f, 330f);
        info.sizeDelta = new Vector2(760f, 150f);
        Image infoImg = infoGo.GetComponent<Image>();
        infoImg.sprite = rounded;
        infoImg.type = Image.Type.Sliced;
        infoImg.pixelsPerUnitMultiplier = 0.8f;
        infoImg.color = UiTheme.Slate;
        infoImg.raycastTarget = false;
        Text title = UpgradeBuilder.Label("Title", info, "", 54, Color.white, new Vector2(0f, 32f), new Vector2(720f, 70f));
        Text line = UpgradeBuilder.Label("Line", info, "", 40, UiTheme.Money, new Vector2(0f, -32f), new Vector2(720f, 60f));

        // flèche qui désigne une dalle
        var pointer = new GameObject("ViewPointer", typeof(SpriteRenderer), typeof(FaceCamera));
        var sr = pointer.GetComponent<SpriteRenderer>();
        sr.sprite = UiSprites.Arrow();
        sr.flipY = true;
        sr.color = UiTheme.Yellow;
        sr.sortingOrder = 20;
        pointer.transform.localScale = Vector3.one * 1.4f;

        VirtualJoystick joystick = Object.FindFirstObjectByType<VirtualJoystick>(FindObjectsInactive.Include);
        GameObject testRoot = null;
        foreach (GameObject g in Object.FindObjectsByType<GameObject>(FindObjectsInactive.Include, FindObjectsSortMode.None))
            if (g.name == "TestMachine" && g.transform.parent == null) testRoot = g;

        GameObject host = new GameObject("ViewHost", typeof(RectTransform), typeof(FreeViewController));
        host.transform.SetParent(safe, false);
        var so = new SerializedObject(host.GetComponent<FreeViewController>());
        so.FindProperty("cameraFollow").objectReferenceValue = cameraFollow;
        so.FindProperty("player").objectReferenceValue = player;
        so.FindProperty("joystickRoot").objectReferenceValue = joystick != null ? joystick.gameObject : null;
        so.FindProperty("toggleButton").objectReferenceValue = btnGo.GetComponent<Button>();
        so.FindProperty("toggleFace").objectReferenceValue = face.GetComponent<Image>();
        so.FindProperty("revealRoot").objectReferenceValue = testRoot;
        so.FindProperty("infoPanel").objectReferenceValue = infoGo;
        so.FindProperty("infoTitle").objectReferenceValue = title;
        so.FindProperty("infoLine").objectReferenceValue = line;
        so.FindProperty("pointer").objectReferenceValue = pointer.transform;
        so.FindProperty("machineUpgrades").objectReferenceValue = Object.FindFirstObjectByType<MachineUpgradeState>();
        so.FindProperty("buttonObject").objectReferenceValue = btnGo;
        so.ApplyModifiedPropertiesWithoutUndo();
    }
}
