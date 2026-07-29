using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Numerics;
using Dalamud.Interface.Utility.Raii;
using Dalamud.Bindings.ImGui;
using RezPls.Managers;

namespace RezPls.GUI;

public class Interface : IDisposable
{
    public const string PluginName = "RezPls";

    private readonly string _configHeader;
    private readonly RezPls _plugin;

    private          string          _statusFilter = string.Empty;
    private readonly HashSet<string> _seenNames;

    public bool Visible;

    public bool TestMode = false;

    private static void ChangeAndSave<T>(T value, T currentValue, Action<T> setter) where T : IEquatable<T>
    {
        if (value.Equals(currentValue))
            return;

        setter(value);
        RezPls.Config.Save();
    }

    public Interface(RezPls plugin)
    {
        _plugin       = plugin;
        _configHeader = RezPls.Version.Length > 0 ? $"{PluginName} v{RezPls.Version}###{PluginName}" : PluginName;
        _seenNames    = new HashSet<string>(_plugin.StatusSet.DisabledStatusSet.Count + _plugin.StatusSet.EnabledStatusSet.Count);

        Dalamud.PluginInterface.UiBuilder.Draw         += Draw;
        Dalamud.PluginInterface.UiBuilder.OpenConfigUi += Enable;
        Dalamud.PluginInterface.UiBuilder.OpenMainUi   += Enable;
    }

    private static void DrawCheckbox(string name, string tooltip, bool value, Action<bool> setter)
    {
        var tmp = value;
        if (ImGui.Checkbox(name, ref tmp))
            ChangeAndSave(tmp, value, setter);

        if (ImGui.IsItemHovered())
            ImGui.SetTooltip(tooltip);
    }

    private void DrawEnabledCheckbox()
        => DrawCheckbox("啟用插件", "啟用或停用 RezPls。", RezPls.Config.Enabled, e =>
        {
            RezPls.Config.Enabled = e;
            if (e)
                _plugin.Enable();
            else
                _plugin.Disable();
        });

    private void DrawHideSymbolsOnSelfCheckbox()
        => DrawCheckbox("隱藏自己的場景標示", "隱藏顯示在自己角色位置上的圖示及文字。",
            RezPls.Config.HideSymbolsOnSelf,    e => RezPls.Config.HideSymbolsOnSelf = e);

    private void DrawShowCastProgressCheckbox()
        => DrawCheckbox("顯示詠唱進度", "顯示復活技能目前的詠唱進度。僅適用於包含填滿區域的方框樣式。",
            RezPls.Config.ShowCastProgress,   e => RezPls.Config.ShowCastProgress = e);

    private void DrawEnabledRaiseCheckbox()
        => DrawCheckbox("啟用復活標示",
            "標示正在被復活或已有復活效果的玩家。", RezPls.Config.EnabledRaise, e => RezPls.Config.EnabledRaise = e);

    private void DrawShowGroupCheckbox()
        => DrawCheckbox("在小隊列表中標示",
            "依照狀態與顏色設定，在小隊列表中標示玩家。",
            RezPls.Config.ShowGroupFrame,
            e => RezPls.Config.ShowGroupFrame = e);

    private void DrawShowAllianceCheckbox()
        => DrawCheckbox("在團隊列表中標示",
            "依照狀態與顏色設定，在團隊列表中標示玩家。",
            RezPls.Config.ShowAllianceFrame,
            e => RezPls.Config.ShowAllianceFrame = e);

    private void DrawShowCasterNamesCheckbox()
        => DrawCheckbox("顯示施法者名稱",
            "標示玩家時，同時在列表中顯示正在對其施放復活或解除技能的角色名稱。",
            RezPls.Config.ShowCasterNames,
            e => RezPls.Config.ShowCasterNames = e);

    private void DrawShowIconCheckbox()
        => DrawCheckbox("顯示場景圖示",
            "在正在被復活或已有復活效果的倒地角色位置顯示圖示。", RezPls.Config.ShowIcon,
            e => RezPls.Config.ShowIcon = e);

    private void DrawShowIconDispelCheckbox()
        => DrawCheckbox("顯示場景圖示##Dispel",
            "在具有可解除負面狀態的玩家位置顯示狀態圖示。", RezPls.Config.ShowIconDispel,
            e => RezPls.Config.ShowIconDispel = e);

    private void DrawShowInWorldTextCheckbox()
        => DrawCheckbox("顯示場景文字",
            "在倒地角色位置顯示目前的復活施法者，或標示其已有復活效果。",
            RezPls.Config.ShowInWorldText,
            e => RezPls.Config.ShowInWorldText = e);

    private void DrawShowInWorldTextDispelCheckbox()
        => DrawCheckbox("顯示場景文字##Dispel",
            "在受影響的玩家位置顯示目前的解除施法者，或標示其具有可解除的負面狀態。",
            RezPls.Config.ShowInWorldTextDispel,
            e => RezPls.Config.ShowInWorldTextDispel = e);

    private void DrawRestrictJobsCheckbox()
        => DrawCheckbox("僅限可使用復活技能的職業",
            "只有目前職業本身可使用復活技能時才顯示復活資訊。\n"
          + "幻術師、白魔法師、秘術師、學者、召喚師、占星術士、青魔法師，以及 64 級以上的赤魔法師。\n"
          + "不包含失傳技能與文理技能。\n", RezPls.Config.RestrictedJobs,
            e => RezPls.Config.RestrictedJobs = e);

    private void DrawDispelHighlightingCheckbox()
        => DrawCheckbox("啟用可解除狀態標示",
            "標示具有可解除負面狀態的玩家。",
            RezPls.Config.EnabledDispel, e => RezPls.Config.EnabledDispel = e);

    private void DrawRestrictJobsDispelCheckbox()
        => DrawCheckbox("僅限可解除狀態的職業",
            "只有目前職業本身可使用狀態解除技能時才顯示資訊。\n"
          + "幻術師、白魔法師、學者、占星術士、35 級以上的吟遊詩人，以及青魔法師。",
            RezPls.Config.RestrictedJobsDispel, e => RezPls.Config.RestrictedJobsDispel = e);

    private void DrawTestModeCheckBox1()
        => DrawCheckbox("測試：已有復活效果", "在自己角色及小隊列表上模擬「已有復活效果」狀態。",
            ActorWatcher.TestMode == 1,       e => ActorWatcher.TestMode = e ? 1 : 0);

    private void DrawTestModeCheckBox2()
        => DrawCheckbox("測試：由目前目標復活",
            "在自己角色及小隊列表上模擬「正在被復活」狀態，並將目前目標視為施法者。",
            ActorWatcher.TestMode == 2, e => ActorWatcher.TestMode = e ? 2 : 0);

    private void DrawTestModeCheckBox3()
        => DrawCheckbox("測試：重複施放復活",
            "在自己角色上模擬「重複施放復活」狀態，視為自己與目前目標同時施放復活。",
            ActorWatcher.TestMode == 3, e => ActorWatcher.TestMode = e ? 3 : 0);

    private void DrawTestModeCheckBox4()
        => DrawCheckbox("測試：具有可解除狀態",
            "在自己角色上模擬「具有監控中的負面狀態」。",
            ActorWatcher.TestMode == 4, e => ActorWatcher.TestMode = e ? 4 : 0);

    private void DrawTestModeCheckBox5()
        => DrawCheckbox("測試：由目前目標解除狀態",
            "在自己角色上模擬「正在解除狀態」，並將目前目標視為施法者。",
            ActorWatcher.TestMode == 5, e => ActorWatcher.TestMode = e ? 5 : 0);

    private void DrawTestModeCheckBox6()
        => DrawCheckbox("測試：重複或無效解除",
            "在自己角色上模擬重複施放解除技能，或在沒有監控狀態時施放解除技能。",
            ActorWatcher.TestMode == 6, e => ActorWatcher.TestMode = e ? 6 : 0);


    private void DrawSingleStatusEffectList(string header, bool which, float width)
    {
        using var group = ImRaii.Group();
        var       list  = which ? _plugin.StatusSet.DisabledStatusSet : _plugin.StatusSet.EnabledStatusSet;
        _seenNames.Clear();
        if (ImGui.BeginListBox($"##{header}box", width / 2 * Vector2.UnitX))
        {
            for (var i = 0; i < list.Count; ++i)
            {
                var (status, name) = list[i];
                if (!name.Contains(_statusFilter) || _seenNames.Contains(name))
                    continue;

                _seenNames.Add(name);
                if (ImGui.Selectable($"{status.Name}##status{status.RowId}"))
                {
                    _plugin.StatusSet.Swap((ushort)status.RowId);
                    --i;
                }
            }

            ImGui.EndListBox();
        }

        if (which)
        {
            if (ImGui.Button("停用所有狀態", width / 2 * Vector2.UnitX))
                _plugin.StatusSet.ClearEnabledList();
        }
        else if (ImGui.Button("啟用所有狀態", width / 2 * Vector2.UnitX))
        {
            _plugin.StatusSet.ClearDisabledList();
        }
    }

    private static void DrawStatusSelectorTitles(float width)
    {
        const string disabledHeader = "未監控的狀態";
        const string enabledHeader  = "監控中的狀態";
        var          pos1           = width / 4 - ImGui.CalcTextSize(disabledHeader).X / 2;
        var          pos2           = 3 * width / 4 + ImGui.GetStyle().ItemSpacing.X - ImGui.CalcTextSize(enabledHeader).X / 2;
        ImGui.SetCursorPosX(pos1);
        ImGui.AlignTextToFramePadding();
        ImGui.Text(disabledHeader);
        ImGui.SameLine(pos2);
        ImGui.AlignTextToFramePadding();
        ImGui.Text(enabledHeader);
    }

    private void DrawStatusEffectList()
    {
        var width = ImGui.GetWindowContentRegionMax().X - ImGui.GetWindowContentRegionMin().X - ImGui.GetStyle().ItemSpacing.X;
        DrawStatusSelectorTitles(width);
        ImGui.SetNextItemWidth(width);
        ImGui.InputTextWithHint("##statusFilter", "篩選狀態……", ref _statusFilter, 64);
        DrawSingleStatusEffectList("未監控的狀態", true, width);
        ImGui.SameLine();
        DrawSingleStatusEffectList("監控中的狀態", false, width);
    }


    private void DrawColorPicker(string name, string tooltip, uint value, uint defaultValue, Action<uint> setter)
    {
        const ImGuiColorEditFlags flags = ImGuiColorEditFlags.AlphaPreviewHalf | ImGuiColorEditFlags.NoInputs;

        var tmp = ImGui.ColorConvertU32ToFloat4(value);
        if (ImGui.ColorEdit4($"##{name}", ref tmp, flags))
            ChangeAndSave(ImGui.ColorConvertFloat4ToU32(tmp), value, setter);
        ImGui.SameLine();
        if (ImGui.Button($"預設值##{name}"))
            ChangeAndSave(defaultValue, value, setter);
        if (ImGui.IsItemHovered())
            ImGui.SetTooltip(
                $"重設為預設值：#{defaultValue & 0xFF:X2}{(defaultValue >> 8) & 0xFF:X2}{(defaultValue >> 16) & 0xFF:X2}{defaultValue >> 24:X2}");
        ImGui.SameLine();
        ImGui.Text(name);
        if (ImGui.IsItemHovered())
            ImGui.SetTooltip(tooltip);
    }

    private void DrawCurrentRaiseColorPicker()
        => DrawColorPicker("正在被復活",
            "其他玩家或只有自己正在對目標施放復活時使用的標示顏色。",
            RezPls.Config.CurrentlyRaisingColor, RezPlsConfig.DefaultCurrentlyRaisingColor, c => RezPls.Config.CurrentlyRaisingColor = c);


    private void DrawAlreadyRaisedColorPicker()
        => DrawColorPicker("已有復活效果",
            "目標已有復活效果，且自己目前未對其施放復活時使用的標示顏色。",
            RezPls.Config.RaisedColor, RezPlsConfig.DefaultRaisedColor, c => RezPls.Config.RaisedColor = c);

    private void DrawDoubleRaiseColorPicker()
        => DrawColorPicker("重複或無效施法",
            "自己施放復活時，若目標已有復活效果或其他玩家也正在復活目標，將使用此顏色。\n"
          + "自己與其他玩家同時解除狀態，或對沒有監控中負面狀態的玩家施放解除技能時，也會使用此顏色。",
            RezPls.Config.DoubleRaiseColor, RezPlsConfig.DefaultDoubleRaiseColor, c => RezPls.Config.DoubleRaiseColor = c);

    private void DrawInWorldBackgroundColorPicker()
        => DrawColorPicker("復活場景文字背景",
            "顯示在倒地角色位置之復活文字的背景顏色。",
            RezPls.Config.InWorldBackgroundColor, RezPlsConfig.DefaultInWorldBackgroundColorRaise,
            c => RezPls.Config.InWorldBackgroundColor = c);

    private void DrawInWorldBackgroundColorPickerDispel()
        => DrawColorPicker("解除狀態場景文字背景",
            "顯示在具有監控中負面狀態之角色位置的文字背景顏色。",
            RezPls.Config.InWorldBackgroundColorDispel, RezPlsConfig.DefaultInWorldBackgroundColorDispel,
            c => RezPls.Config.InWorldBackgroundColorDispel = c);

    private void DrawDispellableColorPicker()
        => DrawColorPicker("具有監控中的負面狀態",
            "玩家具有任一監控中的負面狀態時使用的標示顏色。",
            RezPls.Config.DispellableColor, RezPlsConfig.DefaultDispellableColor, c => RezPls.Config.DispellableColor = c);

    private void DrawCurrentlyDispelledColorPicker()
        => DrawColorPicker("正在解除狀態",
            "其他玩家或只有自己正在對目標施放解除技能時使用的標示顏色。",
            RezPls.Config.CurrentlyDispelColor, RezPlsConfig.DefaultCurrentlyDispelColor, c => RezPls.Config.CurrentlyDispelColor = c);

    private void DrawScaleButton()
    {
        const float min  = 0.1f;
        const float max  = 3.0f;
        const float step = 0.005f;

        var tmp = RezPls.Config.IconScale;
        if (ImGui.DragFloat("場景圖示縮放", ref tmp, step, min, max))
            ChangeAndSave(tmp, RezPls.Config.IconScale, f => RezPls.Config.IconScale = Math.Max(min, Math.Min(f, max)));
        if (ImGui.IsItemHovered())
            ImGui.SetTooltip("設定顯示在倒地角色位置之復活圖示的大小。");
    }

    private static readonly string[] RectTypeStrings = new[]
    {
        "填滿",
        "僅外框",
        "僅不透明外框",
        "填滿及不透明外框",
    };

    private void DrawRectTypeSelector()
    {
        var type = (int)RezPls.Config.RectType;
        if (!ImGui.Combo("方框樣式", ref type, RectTypeStrings, RectTypeStrings.Length))
            return;

        ChangeAndSave(type, (int)RezPls.Config.RectType, t => RezPls.Config.RectType = (RectType)t);
    }

    public void Draw()
    {
        if (!Visible)
            return;

        var buttonHeight      = ImGui.GetFontSize() + ImGui.GetStyle().FramePadding.Y * 2;
        var horizontalSpacing = new Vector2(0, ImGui.GetTextLineHeightWithSpacing());

        var height = 15 * buttonHeight
          + 6 * horizontalSpacing.Y
          + 27 * ImGui.GetStyle().ItemSpacing.Y;
        var width       = 450 * ImGui.GetIO().FontGlobalScale;
        var constraints = new Vector2(width, height);
        ImGui.SetNextWindowSizeConstraints(constraints, constraints);

        if (!ImGui.Begin(_configHeader, ref Visible, ImGuiWindowFlags.NoResize))
            return;

        try
        {
            DrawEnabledCheckbox();

            if (ImGui.CollapsingHeader("復活設定"))
            {
                DrawEnabledRaiseCheckbox();
                DrawRestrictJobsCheckbox();
                DrawShowIconCheckbox();
                DrawShowInWorldTextCheckbox();
                ImGui.Dummy(horizontalSpacing);
            }

            if (ImGui.CollapsingHeader("狀態解除設定"))
            {
                DrawDispelHighlightingCheckbox();
                DrawRestrictJobsDispelCheckbox();
                DrawShowIconDispelCheckbox();
                DrawShowInWorldTextDispelCheckbox();
                ImGui.Dummy(horizontalSpacing);
                DrawStatusEffectList();
                ImGui.Dummy(horizontalSpacing);
            }

            if (ImGui.CollapsingHeader("一般設定"))
            {
                DrawShowCastProgressCheckbox();
                DrawHideSymbolsOnSelfCheckbox();
                DrawShowGroupCheckbox();
                DrawShowAllianceCheckbox();
                DrawShowCasterNamesCheckbox();
                DrawRectTypeSelector();
                DrawScaleButton();
                ImGui.Dummy(horizontalSpacing);
            }

            if (ImGui.CollapsingHeader("顏色"))
            {
                DrawCurrentRaiseColorPicker();
                DrawAlreadyRaisedColorPicker();
                ImGui.Dummy(horizontalSpacing);
                DrawDispellableColorPicker();
                DrawCurrentlyDispelledColorPicker();
                ImGui.Dummy(horizontalSpacing);
                DrawDoubleRaiseColorPicker();
                ImGui.Dummy(horizontalSpacing);
                DrawInWorldBackgroundColorPicker();
                DrawInWorldBackgroundColorPickerDispel();
                ImGui.Dummy(horizontalSpacing);
            }

            if (ImGui.CollapsingHeader("測試"))
            {
                DrawTestModeCheckBox1();
                DrawTestModeCheckBox2();
                DrawTestModeCheckBox3();
                DrawTestModeCheckBox4();
                DrawTestModeCheckBox5();
                DrawTestModeCheckBox6();
            }

            DrawDebug();
        }
        finally
        {
            ImGui.End();
        }
    }

    [Conditional("DEBUG")]
    private void DrawDebug()
    {
        if (!ImGui.CollapsingHeader("偵錯"))
            return;

        ImGui.TextUnformatted($"PvP 中：{Dalamud.ClientState.IsPvP}");
        ImGui.TextUnformatted($"測試模式：{ActorWatcher.TestMode}");
        using (var tree = ImRaii.TreeNode("名稱"))
        {
            if (tree)
                foreach (var (id, name) in _plugin.ActorWatcher.ActorNames)
                    ImRaii.TreeNode($"{name} ({id})", ImGuiTreeNodeFlags.Bullet | ImGuiTreeNodeFlags.Leaf).Dispose();
        }

        using (var tree = ImRaii.TreeNode("施法"))
        {
            if (tree)
                foreach (var (id, state) in _plugin.ActorWatcher.RezList)
                {
                    ImRaii.TreeNode($"{id}：{state.Type}，施法者 {state.Caster}，{(state.HasStatus ? "具有狀態" : string.Empty)}",
                        ImGuiTreeNodeFlags.Bullet | ImGuiTreeNodeFlags.Leaf).Dispose();
                }
        }
    }

    public void Enable()
        => Visible = true;

    public void Dispose()
    {
        Dalamud.PluginInterface.UiBuilder.Draw         -= Draw;
        Dalamud.PluginInterface.UiBuilder.OpenConfigUi -= Enable;
        Dalamud.PluginInterface.UiBuilder.OpenMainUi   -= Enable;
    }
}
