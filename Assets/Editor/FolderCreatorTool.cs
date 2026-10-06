using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;

/// <summary>
/// Unity Editor penceresi: Assets klasörü altında hiyerarşik klasör oluşturma aracı.
/// Menü: Tools > Folder Creator
/// Konum: Assets/Editor/FolderCreatorTool.cs
/// </summary>
public class FolderCreatorTool : EditorWindow
{
    // =========================================================
    //  Veri Modeli
    // =========================================================
    private class FolderNode
    {
        public string           name;
        public string           guid;       // Unity asset GUID değil, iç kimlik
        public bool             isOpen;
        public List<FolderNode> children = new List<FolderNode>();

        public FolderNode(string name)
        {
            this.name = name;
            this.guid = Guid.NewGuid().ToString("N");
        }
    }

    // =========================================================
    //  Sabitler
    // =========================================================
    private const string ASSETS_ROOT  = "Assets";
    private const int    ROW_H        = 20;   // her satır yüksekliği (px)
    private const int    INDENT_W     = 16;   // her derinlik seviyesi girintisi (px)
    private const int    ICON_W       = 16;
    private const int    BTN_W        = 22;
    private const int    LEFT_PAD     = 4;

    private static readonly char[]   INVALID_CHARS = Path.GetInvalidFileNameChars();
    private static readonly string[] RESERVED = {
        "CON","PRN","AUX","NUL",
        "COM1","COM2","COM3","COM4","COM5","COM6","COM7","COM8","COM9",
        "LPT1","LPT2","LPT3","LPT4","LPT5","LPT6","LPT7","LPT8","LPT9"
    };

    // =========================================================
    //  Durum
    // =========================================================
    private FolderNode  _root;
    private Vector2     _scroll;
    private string      _search        = "";

    // Aktif input: hem kök hem de herhangi bir node için
    // parentGuid == null  → input kapalı
    // parentGuid == ""    → kök (Assets) altına ekleniyor
    // parentGuid == guid  → o node altına ekleniyor
    private string      _inputParentGuid = null;
    private string      _inputBuf        = "";
    private string      _inputErr        = "";
    private bool        _wantFocusInput;

    // Rename
    private string      _renameGuid      = null;
    private string      _renameBuf       = "";
    private string      _renameErr       = "";
    private bool        _wantFocusRename;

    // Silme onayı
    private string      _deleteGuid;
    private string      _deleteName;
    private int         _deleteChildCount;
    private bool        _showDeleteModal;

    // Status bar
    private string      _statusMsg  = "";
    private MessageType _statusType = MessageType.None;
    private double      _statusExp;

    // Styles — Layout geçişinde yeniden oluşturulur
    private GUIStyle    _styleFoldout;
    private GUIStyle    _styleLabel;
    private GUIStyle    _styleAddLink;
    private GUIStyle    _styleInput;
    private GUIStyle    _styleError;
    private bool        _stylesReady;

    // =========================================================
    //  Menü
    // =========================================================
    [MenuItem("Tools/Folder Creator")]
    public static void Open()
    {
        var w = GetWindow<FolderCreatorTool>("Folder Creator");
        w.minSize = new Vector2(300, 350);
    }

    // =========================================================
    //  Yaşam döngüsü
    // =========================================================
    private void OnEnable()
    {
        _stylesReady = false;
        Reload();
    }

    // Diskten klasör ağacını okur
    private void Reload()
    {
        _root         = new FolderNode(ASSETS_ROOT) { isOpen = true };
        _inputParentGuid = null;
        _renameGuid      = null;
        _showDeleteModal = false;
        ScanDir(_root, ASSETS_ROOT);
        Repaint();
    }

    private void ScanDir(FolderNode node, string diskPath)
    {
        if (!Directory.Exists(diskPath)) return;
        string[] dirs = Directory.GetDirectories(diskPath);
        Array.Sort(dirs, StringComparer.OrdinalIgnoreCase);
        foreach (string dir in dirs)
        {
            var child = new FolderNode(Path.GetFileName(dir));
            node.children.Add(child);
            ScanDir(child, dir);
        }
    }

    // =========================================================
    //  Ana GUI
    // =========================================================
    private void OnGUI()
    {
        EnsureStyles();

        DrawToolbar();

        // Status süresi dolduysa temizle
        if (_statusExp > 0 && EditorApplication.timeSinceStartup > _statusExp)
        {
            _statusMsg = ""; _statusExp = 0; Repaint();
        }

        // Ağaç + scrollview
        _scroll = EditorGUILayout.BeginScrollView(_scroll);
        DrawRootHeader();
        DrawChildrenOf(_root, depth: 0);
        DrawAddRow(parentGuid: "", depth: 0);   // "" = kök seviyesi butonu / inputu
        EditorGUILayout.Space(8);
        EditorGUILayout.EndScrollView();

        DrawStatusBar();

        // Silme modalı en üstte çizilir
        if (_showDeleteModal) DrawDeleteModal();
    }

    // =========================================================
    //  Toolbar
    // =========================================================
    private void DrawToolbar()
    {
        EditorGUILayout.BeginHorizontal(EditorStyles.toolbar);

        GUILayout.Label("Folder Creator", EditorStyles.boldLabel, GUILayout.Width(105));
        GUILayout.FlexibleSpace();

        EditorGUI.BeginChangeCheck();
        string s = EditorGUILayout.TextField(_search, EditorStyles.toolbarSearchField, GUILayout.Width(150));
        if (EditorGUI.EndChangeCheck()) { _search = s; Repaint(); }

        if (GUILayout.Button("Yenile", EditorStyles.toolbarButton, GUILayout.Width(48)))
        {
            Reload();
            Status("Ağaç yenilendi.", MessageType.Info);
        }
        if (GUILayout.Button("Aç", EditorStyles.toolbarButton, GUILayout.Width(28)))
            SetOpenAll(_root, true);
        if (GUILayout.Button("Kapat", EditorStyles.toolbarButton, GUILayout.Width(44)))
        { SetOpenAll(_root, false); _root.isOpen = true; }

        EditorGUILayout.EndHorizontal();
    }

    // =========================================================
    //  Kök Başlık Satırı (Assets)
    // =========================================================
    private void DrawRootHeader()
    {
        EditorGUILayout.BeginHorizontal(GUILayout.Height(ROW_H));
        GUILayout.Space(LEFT_PAD);
        GUILayout.Label(EditorGUIUtility.IconContent("Folder Icon"),
                        GUILayout.Width(ICON_W), GUILayout.Height(ICON_W));
        GUILayout.Label("Assets", EditorStyles.boldLabel);
        GUILayout.FlexibleSpace();
        GUILayout.Label(TotalCount(_root) + " klasör", EditorStyles.miniLabel);
        EditorGUILayout.EndHorizontal();
    }

    // =========================================================
    //  Çocuk Listesi
    // =========================================================
    private void DrawChildrenOf(FolderNode parent, int depth)
    {
        foreach (var child in parent.children)
        {
            if (!Matches(child, _search)) continue;

            // Rename modu
            if (_renameGuid == child.guid)
            {
                DrawRenameRow(child, depth);
                // Rename modunda da çocukları göster
                if (child.isOpen)
                {
                    DrawChildrenOf(child, depth + 1);
                    DrawAddRow(child.guid, depth + 1);
                }
                continue;
            }

            DrawNodeRow(child, depth);

            // Input satırı: bu node'un hemen altında, çocuklardan ÖNCE
            if (_inputParentGuid == child.guid)
                DrawInputRow(depth + 1);

            if (child.isOpen)
            {
                DrawChildrenOf(child, depth + 1);
                DrawAddRow(child.guid, depth + 1);
            }
        }
    }

    // =========================================================
    //  Normal Node Satırı
    // =========================================================
    private void DrawNodeRow(FolderNode node, int depth)
    {
        bool hasChildren = node.children.Count > 0;
        float xOffset    = LEFT_PAD + depth * INDENT_W;

        EditorGUILayout.BeginHorizontal(GUILayout.Height(ROW_H));

        // Girinti boşluğu
        GUILayout.Space(xOffset);

        // ▶ / ▼ toggle — sabit genişlikte, Foldout DEĞİL (layout sorunlarını önler)
        string arrow = hasChildren ? (node.isOpen ? "▾" : "▸") : " ";
        if (GUILayout.Button(arrow, EditorStyles.label, GUILayout.Width(12), GUILayout.Height(ROW_H)))
        {
            if (hasChildren) { node.isOpen = !node.isOpen; Repaint(); }
        }

        // Klasör ikonu
        var iconKey = (node.isOpen && hasChildren) ? "FolderOpened Icon" : "Folder Icon";
        GUILayout.Label(EditorGUIUtility.IconContent(iconKey),
                        GUILayout.Width(ICON_W), GUILayout.Height(ICON_W));
        GUILayout.Space(2);

        // İsim
        GUILayout.Label(node.name, _styleLabel, GUILayout.ExpandWidth(true));

        // Sağ taraf butonlar
        if (GUILayout.Button("+", EditorStyles.miniButton, GUILayout.Width(BTN_W)))
        {
            BeginAdd(node.guid);
            node.isOpen = true;
        }
        if (GUILayout.Button("✎", EditorStyles.miniButton, GUILayout.Width(BTN_W)))
            BeginRename(node);
        if (GUILayout.Button("✕", EditorStyles.miniButton, GUILayout.Width(BTN_W)))
            AskDelete(node);

        EditorGUILayout.EndHorizontal();
    }

    // =========================================================
    //  "+ klasör ekle" Butonu  VEYA  Input Satırı (kök dahil)
    // =========================================================
    private void DrawAddRow(string parentGuid, int depth)
    {
        float xOffset = LEFT_PAD + depth * INDENT_W + 14; // 14 = ok + ikon hizası

        // Eğer bu parent için input açıksa input satırı çiz
        if (_inputParentGuid == parentGuid)
        {
            DrawInputRow(depth);
            return;
        }

        // Aksi halde küçük "+ klasör ekle" bağlantısı
        EditorGUILayout.BeginHorizontal(GUILayout.Height(18));
        GUILayout.Space(xOffset);
        if (GUILayout.Button("+ klasör ekle", _styleAddLink))
            BeginAdd(parentGuid);
        GUILayout.FlexibleSpace();
        EditorGUILayout.EndHorizontal();
    }

    // =========================================================
    //  Input Satırı (klasör adı girişi)
    // =========================================================
    private void DrawInputRow(int depth)
    {
        float xOffset = LEFT_PAD + depth * INDENT_W + 14;

        EditorGUILayout.BeginHorizontal(GUILayout.Height(ROW_H));
        GUILayout.Space(xOffset);

        GUILayout.Label(EditorGUIUtility.IconContent("Folder Icon"),
                        GUILayout.Width(ICON_W), GUILayout.Height(ICON_W));

        GUI.SetNextControlName("NewFolderInput");
        _inputBuf = EditorGUILayout.TextField(_inputBuf, _styleInput, GUILayout.ExpandWidth(true));

        if (_wantFocusInput)
        {
            EditorGUI.FocusTextInControl("NewFolderInput");
            _wantFocusInput = false;
        }

        if (GUILayout.Button("✓", EditorStyles.miniButton, GUILayout.Width(BTN_W)))
            CommitAdd();
        if (GUILayout.Button("✕", EditorStyles.miniButton, GUILayout.Width(BTN_W)))
            CancelAdd();

        EditorGUILayout.EndHorizontal();

        // Hata mesajı
        if (!string.IsNullOrEmpty(_inputErr))
        {
            EditorGUILayout.BeginHorizontal();
            GUILayout.Space(xOffset + ICON_W + 2);
            GUILayout.Label(_inputErr, _styleError);
            EditorGUILayout.EndHorizontal();
        }

        // Klavye
        HandleInputKeys(CommitAdd, CancelAdd);
    }

    // =========================================================
    //  Rename Satırı
    // =========================================================
    private void DrawRenameRow(FolderNode node, int depth)
    {
        float xOffset = LEFT_PAD + depth * INDENT_W + 14;

        EditorGUILayout.BeginHorizontal(GUILayout.Height(ROW_H));
        GUILayout.Space(xOffset);

        GUILayout.Label(EditorGUIUtility.IconContent("Folder Icon"),
                        GUILayout.Width(ICON_W), GUILayout.Height(ICON_W));

        GUI.SetNextControlName("RenameInput");
        _renameBuf = EditorGUILayout.TextField(_renameBuf, _styleInput, GUILayout.ExpandWidth(true));

        if (_wantFocusRename)
        {
            EditorGUI.FocusTextInControl("RenameInput");
            _wantFocusRename = false;
        }

        if (GUILayout.Button("✓", EditorStyles.miniButton, GUILayout.Width(BTN_W)))
            CommitRename(node);
        if (GUILayout.Button("✕", EditorStyles.miniButton, GUILayout.Width(BTN_W)))
            CancelRename();

        EditorGUILayout.EndHorizontal();

        if (!string.IsNullOrEmpty(_renameErr))
        {
            EditorGUILayout.BeginHorizontal();
            GUILayout.Space(xOffset + ICON_W + 2);
            GUILayout.Label(_renameErr, _styleError);
            EditorGUILayout.EndHorizontal();
        }

        HandleInputKeys(() => CommitRename(node), CancelRename);
    }

    // =========================================================
    //  Silme Onay Modalı
    // =========================================================
    private void DrawDeleteModal()
    {
        // Modal arka plan (yarı saydam)
        Rect full = new Rect(0, 0, position.width, position.height);
        EditorGUI.DrawRect(full, new Color(0, 0, 0, 0.4f));

        float mw = Mathf.Min(320, position.width - 40);
        float mh = 120;
        Rect box = new Rect(
            (position.width  - mw) / 2f,
            (position.height - mh) / 2f,
            mw, mh);

        GUILayout.BeginArea(box, EditorStyles.helpBox);
        EditorGUILayout.Space(4);

        string msg = _deleteChildCount > 0
            ? $"\"{_deleteName}\" ve içindeki {_deleteChildCount} alt klasör silinecek.\nEmin misiniz?"
            : $"\"{_deleteName}\" klasörü silinecek.\nEmin misiniz?";

        EditorGUILayout.HelpBox(msg, MessageType.Warning);
        EditorGUILayout.Space(4);

        EditorGUILayout.BeginHorizontal();
        if (GUILayout.Button("Evet, Sil", GUILayout.Height(26)))
            DoDelete();
        if (GUILayout.Button("İptal", GUILayout.Height(26)))
            CancelDelete();
        EditorGUILayout.EndHorizontal();

        GUILayout.EndArea();
    }

    // =========================================================
    //  Status Bar
    // =========================================================
    private void DrawStatusBar()
    {
        EditorGUILayout.BeginHorizontal(EditorStyles.toolbar);
        if (!string.IsNullOrEmpty(_statusMsg))
            GUILayout.Label(_statusMsg, EditorStyles.miniLabel);
        else
            GUILayout.Label("Hazır", EditorStyles.miniLabel);
        GUILayout.FlexibleSpace();
        EditorGUILayout.EndHorizontal();
    }

    // =========================================================
    //  Eylemler — Klasör Ekleme
    // =========================================================
    private void BeginAdd(string parentGuid)
    {
        _inputParentGuid = parentGuid;   // "" = kök, diğer = node guid
        _inputBuf        = "";
        _inputErr        = "";
        _renameGuid      = null;         // varsa rename'i kapat
        _wantFocusInput  = true;
        Repaint();
    }

    private void CancelAdd()
    {
        _inputParentGuid = null;
        _inputBuf        = "";
        _inputErr        = "";
        Repaint();
    }

    private void CommitAdd()
    {
        string name = _inputBuf.Trim();

        // Parent node'u bul (kök için _root, diğerleri için FindNode)
        FolderNode parent = string.IsNullOrEmpty(_inputParentGuid)
            ? _root
            : FindNode(_root, _inputParentGuid);

        if (parent == null) { CancelAdd(); return; }

        string err = Validate(name, parent.children);
        if (err != null) { _inputErr = err; Repaint(); return; }

        // Disk yolu
        string parentPath = NodePath(parent);
        string created    = AssetDatabase.CreateFolder(parentPath, name);

        if (string.IsNullOrEmpty(created))
        {
            _inputErr = $"Unity klasörü oluşturamadı: {parentPath}/{name}";
            Repaint();
            return;
        }

        AssetDatabase.Refresh();

        // Ağaca ekle (alfabetik sırayla)
        var newNode = new FolderNode(name);
        parent.children.Add(newNode);
        parent.children.Sort((a, b) => string.Compare(a.name, b.name, StringComparison.OrdinalIgnoreCase));
        parent.isOpen = true;

        _inputParentGuid = null;
        _inputBuf        = "";
        _inputErr        = "";

        Status($"Oluşturuldu: {parentPath}/{name}", MessageType.Info);
        Repaint();
    }

    // =========================================================
    //  Eylemler — Rename
    // =========================================================
    private void BeginRename(FolderNode node)
    {
        _renameGuid      = node.guid;
        _renameBuf       = node.name;
        _renameErr       = "";
        _inputParentGuid = null;
        _wantFocusRename = true;
        Repaint();
    }

    private void CancelRename()
    {
        _renameGuid = null;
        _renameErr  = "";
        Repaint();
    }

    private void CommitRename(FolderNode node)
    {
        string newName = _renameBuf.Trim();
        var parent     = FindParent(_root, node.guid);
        if (parent == null) { CancelRename(); return; }

        // Kardeşleri al, kendini çıkar
        var siblings = new List<FolderNode>(parent.children);
        siblings.RemoveAll(c => c.guid == node.guid);

        string err = Validate(newName, siblings);
        if (err != null) { _renameErr = err; Repaint(); return; }

        string oldPath = NodePath(node);
        string result  = AssetDatabase.RenameAsset(oldPath, newName);
        if (!string.IsNullOrEmpty(result))
        {
            _renameErr = "Unity hatası: " + result;
            Repaint();
            return;
        }

        node.name   = newName;
        _renameGuid = null;
        _renameErr  = "";

        // Sırayı güncelle
        parent.children.Sort((a, b) => string.Compare(a.name, b.name, StringComparison.OrdinalIgnoreCase));

        AssetDatabase.Refresh();
        Status($"Yeniden adlandırıldı → {newName}", MessageType.Info);
        Repaint();
    }

    // =========================================================
    //  Eylemler — Silme
    // =========================================================
    private void AskDelete(FolderNode node)
    {
        _deleteGuid       = node.guid;
        _deleteName       = node.name;
        _deleteChildCount = TotalCount(node);
        _showDeleteModal  = true;
        Repaint();
    }

    private void DoDelete()
    {
        var node   = FindNode(_root, _deleteGuid);
        var parent = FindParent(_root, _deleteGuid);
        if (node == null || parent == null) { CancelDelete(); return; }

        string path = NodePath(node);
        bool   ok   = AssetDatabase.DeleteAsset(path);

        if (!ok)
        {
            Status($"Silinemedi: {path}", MessageType.Error);
            CancelDelete();
            return;
        }

        parent.children.RemoveAll(c => c.guid == _deleteGuid);
        AssetDatabase.Refresh();
        Status($"Silindi: {node.name}", MessageType.Warning);
        CancelDelete();
    }

    private void CancelDelete()
    {
        _showDeleteModal = false;
        _deleteGuid      = null;
        Repaint();
    }

    // =========================================================
    //  Yardımcı: Disk Yolu
    //  _root için "Assets", diğerleri için "Assets/A/B/C"
    // =========================================================
    private string NodePath(FolderNode node)
    {
        if (node == _root) return ASSETS_ROOT;
        return BuildPath(_root, node.guid, ASSETS_ROOT);
    }

    private string BuildPath(FolderNode cur, string targetGuid, string acc)
    {
        // Mevcut node hedefse yolu döndür
        if (cur.guid == targetGuid) return acc;

        foreach (var child in cur.children)
        {
            // child için yeni yol: acc/child.name
            string childAcc = acc + "/" + child.name;
            string result   = BuildPath(child, targetGuid, childAcc);
            if (result != null) return result;
        }
        return null;
    }

    // =========================================================
    //  Yardımcı: Ağaç Tarama
    // =========================================================
    private FolderNode FindNode(FolderNode node, string guid)
    {
        if (node.guid == guid) return node;
        foreach (var child in node.children)
        {
            var found = FindNode(child, guid);
            if (found != null) return found;
        }
        return null;
    }

    private FolderNode FindParent(FolderNode node, string childGuid)
    {
        foreach (var child in node.children)
        {
            if (child.guid == childGuid) return node;
            var found = FindParent(child, childGuid);
            if (found != null) return found;
        }
        return null;
    }

    private int TotalCount(FolderNode node)
    {
        int n = (node == _root) ? 0 : 1;
        foreach (var child in node.children) n += TotalCount(child);
        return n;
    }

    private void SetOpenAll(FolderNode node, bool val)
    {
        node.isOpen = val;
        foreach (var child in node.children) SetOpenAll(child, val);
        Repaint();
    }

    private bool Matches(FolderNode node, string q)
    {
        if (string.IsNullOrEmpty(q)) return true;
        if (node.name.IndexOf(q, StringComparison.OrdinalIgnoreCase) >= 0) return true;
        foreach (var child in node.children)
            if (Matches(child, q)) return true;
        return false;
    }

    // =========================================================
    //  Yardımcı: Validasyon
    // =========================================================
    private string Validate(string name, List<FolderNode> siblings)
    {
        if (string.IsNullOrEmpty(name))
            return "Klasör adı boş olamaz.";
        if (name != name.Trim())
            return "Başında veya sonunda boşluk olamaz.";
        foreach (char c in INVALID_CHARS)
            if (name.IndexOf(c) >= 0)
                return $"Geçersiz karakter içeriyor: '{c}'";
        if (name.StartsWith("."))
            return "Nokta ile başlayan isimler Unity'de sorun çıkarır.";
        if (name.Length > 64)
            return "İsim 64 karakterden uzun olamaz.";
        foreach (string r in RESERVED)
            if (string.Equals(name, r, StringComparison.OrdinalIgnoreCase))
                return $"'{name}' Windows rezerveli bir isimdir.";
        foreach (var sib in siblings)
            if (string.Equals(sib.name, name, StringComparison.OrdinalIgnoreCase))
                return "Bu isimde bir klasör zaten mevcut.";
        return null;
    }

    // =========================================================
    //  Yardımcı: Klavye (Enter / Escape)
    // =========================================================
    private void HandleInputKeys(Action onConfirm, Action onCancel)
    {
        Event ev = Event.current;
        if (ev.type != EventType.KeyDown) return;
        if (ev.keyCode == KeyCode.Return || ev.keyCode == KeyCode.KeypadEnter)
        { onConfirm(); ev.Use(); }
        else if (ev.keyCode == KeyCode.Escape)
        { onCancel(); ev.Use(); }
    }

    // =========================================================
    //  Yardımcı: Status Mesajı
    // =========================================================
    private void Status(string msg, MessageType type, float sec = 4f)
    {
        _statusMsg  = msg;
        _statusType = type;
        _statusExp  = EditorApplication.timeSinceStartup + sec;
        Repaint();
    }

    // =========================================================
    //  GUIStyle — Layout geçişinde yeniden oluştur
    // =========================================================
    private void EnsureStyles()
    {
        // EditorStyles layout geçişinde hazır olur; Layout event'te oluşturuyoruz
        if (_stylesReady && Event.current.type != EventType.Layout) return;
        if (Event.current.type != EventType.Layout) return;

        _styleFoldout = new GUIStyle(EditorStyles.foldout);

        _styleLabel = new GUIStyle(EditorStyles.label)
        {
            alignment = TextAnchor.MiddleLeft
        };

        _styleAddLink = new GUIStyle(EditorStyles.miniLabel)
        {
            normal  = { textColor = new Color(0.45f, 0.7f, 1f) },
            hover   = { textColor = new Color(0.6f,  0.85f, 1f) },
            active  = { textColor = Color.white },
            padding = new RectOffset(0, 0, 0, 0)
        };

        _styleInput = new GUIStyle(EditorStyles.textField)
        {
            alignment = TextAnchor.MiddleLeft,
            fontSize  = 12
        };

        _styleError = new GUIStyle(EditorStyles.miniLabel)
        {
            wordWrap = true,
            normal   = { textColor = new Color(1f, 0.35f, 0.35f) }
        };

        _stylesReady = true;
    }
}