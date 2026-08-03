using System;
using System.Collections.Generic;
using BepInEx;
using BepInEx.Configuration;
using BepInEx.Logging;
using BepInEx.Unity.IL2CPP;
using HarmonyLib;
using UnityEngine;

namespace SpiritValeSubstatHUD
{
    /// <summary>
    /// 讓裝備的隨機詞條品質「一眼可見」：
    ///   1. 背包裝備名稱前加星號並上色，不必 hover 就能篩選
    ///   2. 道具說明加一行品質總評
    ///   3.（選用）道具說明自動顯示詞條範圍，免按 ALT
    /// 全程只讀遊戲既有 API，不改任何數值，純顯示端。
    /// </summary>
    [BepInPlugin(GUID, NAME, VERSION)]
    public class Plugin : BasePlugin
    {
        public const string GUID = "local.spiritvale.substathud";
        public const string NAME = "SpiritVale Substat Quality HUD";
        public const string VERSION = "1.3.0";

        internal static ManualLogSource Logger;

        internal static ConfigEntry<bool> CfgTintInventory;
        internal static ConfigEntry<bool> CfgTooltipSummary;
        internal static ConfigEntry<bool> CfgAlwaysShowRange;
        internal static ConfigEntry<bool> CfgUseShownMaxed;
        internal static ConfigEntry<float> CfgTier3;
        internal static ConfigEntry<float> CfgTier2;
        internal static ConfigEntry<float> CfgTier1;
        internal static ConfigEntry<bool> CfgDiagnostic;

        public override void Load()
        {
            Logger = base.Log;

            CfgTintInventory = Config.Bind("1.功能", "背包星號標記", true,
                "在背包裝備名稱前加上 ★ 記號並上色，不必把滑鼠移上去就能篩選。");
            CfgTooltipSummary = Config.Bind("1.功能", "彈窗顯示品質總評", true,
                "在道具說明開頭加一行品質總評。");
            CfgAlwaysShowRange = Config.Bind("1.功能", "自動顯示詞條範圍", false,
                "道具說明一律顯示詞條範圍，不必按 ALT。" +
                "注意：這會干擾遊戲原本的按鍵對比裝備功能，故預設關閉。");

            CfgUseShownMaxed = Config.Bind("2.分級門檻", "以顯示滿值為準", true,
                "true＝星號依『數值已達範圍上限的條數比例』計算，也就是你肉眼看到的滿素質。" +
                "false＝依內部 roll 百分位平均計算，更精確反映稀有度，但會與肉眼所見不符" +
                "（窄範圍詞條如 2~3，roll 六成就已經顯示為上限值）。");

            CfgTier3 = Config.Bind("2.分級門檻", "三星", 1.00f,
                "達此比例標 ★★★（金色）。以顯示滿值為準時，1.00 代表每一條都頂到上限。");
            CfgTier2 = Config.Bind("2.分級門檻", "二星", 0.75f, "達此比例標 ★★（橘色）。");
            CfgTier1 = Config.Bind("2.分級門檻", "一星", 0.50f, "達此比例標 ★（紫色）。低於此值不標記。");

            CfgDiagnostic = Config.Bind("3.診斷", "診斷模式", false,
                "在道具說明印出每個詞條的執行期真值並寫入 log。僅供除錯，平常請保持 false。");

            var harmony = new Harmony(GUID);

            if (CfgAlwaysShowRange.Value)
            {
                // 必須先掛這個：它提供「玩家正在按鍵」的訊號，是退場判斷的依據
                TryPatch(harmony, "追蹤遊戲範圍顯示狀態",
                    () => AccessTools.PropertySetter(typeof(UIItemPopup), "showingSubstatRange"),
                    postfix: nameof(Patches.SetShowingRange_Postfix));

                TryPatch(harmony, "自動顯示詞條範圍",
                    () => AccessTools.Method(typeof(Extensions), nameof(Extensions.ToDescription),
                        new[] { typeof(EquipData), typeof(EquipConfig), typeof(bool), typeof(bool) }),
                    prefix: nameof(Patches.ToDescription_Prefix));
            }

            if (CfgTooltipSummary.Value)
            {
                TryPatch(harmony, "彈窗品質總評(裝備)",
                    () => AccessTools.Method(typeof(Extensions), nameof(Extensions.ToDescription),
                        new[] { typeof(EquipData), typeof(EquipConfig), typeof(bool), typeof(bool) }),
                    postfix: nameof(Patches.ToDescription_Postfix));

                TryPatch(harmony, "彈窗品質總評(神器)",
                    () => AccessTools.Method(typeof(Extensions), nameof(Extensions.ToDescription),
                        new[] { typeof(ArtifactData), typeof(ArtifactSetConfig), typeof(bool), typeof(bool) }),
                    postfix: nameof(Patches.ToDescriptionArtifact_Postfix));
            }

            if (CfgTintInventory.Value)
            {
                // 背包實際走泛型入口，兩個多載都掛才保險
                TryPatch(harmony, "背包星號標記(泛型入口)",
                    () => AccessTools.Method(typeof(UIInventoryItem), nameof(UIInventoryItem.Draw),
                        new[] { typeof(IInfoDrawable), typeof(bool) }),
                    postfix: nameof(Patches.InventoryDrawAny_Postfix));

                TryPatch(harmony, "背包星號標記(裝備)",
                    () => AccessTools.Method(typeof(UIInventoryItem), nameof(UIInventoryItem.Draw),
                        new[] { typeof(EquipData), typeof(bool) }),
                    postfix: nameof(Patches.InventoryDraw_Postfix));

                TryPatch(harmony, "背包星號標記(神器)",
                    () => AccessTools.Method(typeof(UIInventoryItem), nameof(UIInventoryItem.Draw),
                        new[] { typeof(ArtifactData), typeof(bool) }),
                    postfix: nameof(Patches.InventoryDrawArtifact_Postfix));
            }

            Logger.LogInfo($"{NAME} v{VERSION} 已載入。");
        }

        /// <summary>
        /// 逐一掛載 patch，任一失敗只記警告不中斷 —— 遊戲改版時降級而非崩潰。
        /// </summary>
        private static void TryPatch(Harmony harmony, string label,
            Func<System.Reflection.MethodBase> resolver, string prefix = null, string postfix = null)
        {
            try
            {
                var target = resolver();
                if (target == null)
                {
                    Logger.LogWarning($"[{label}] 找不到目標方法，略過（遊戲版本可能已更新）。");
                    return;
                }

                harmony.Patch(target,
                    prefix: prefix == null ? null : new HarmonyMethod(typeof(Patches), prefix),
                    postfix: postfix == null ? null : new HarmonyMethod(typeof(Patches), postfix));

                Logger.LogInfo($"[{label}] 掛載成功。");
            }
            catch (Exception ex)
            {
                Logger.LogWarning($"[{label}] 掛載失敗，略過：{ex.Message}");
            }
        }
    }

    /// <summary>詞條品質評估結果。</summary>
    internal struct Quality
    {
        /// <summary>內部 roll 百分位平均（0~1）。反映真實稀有度。</summary>
        public float RollAverage;

        /// <summary>顯示數值已達範圍上限的條數 —— 玩家肉眼認定的「滿」。</summary>
        public int ShownMaxed;

        /// <summary>ShownMaxed 是否取得成功；失敗時只能退回用 RollAverage。</summary>
        public bool ShownValid;

        /// <summary>納入計算的詞條總數。</summary>
        public int Counted;

        /// <summary>依設定取得用於分級的分數（0~1）。</summary>
        public float Score
        {
            get
            {
                if (Plugin.CfgUseShownMaxed.Value && ShownValid && Counted > 0)
                    return (float)ShownMaxed / Counted;
                return RollAverage;
            }
        }
    }

    internal static class Evaluator
    {
        /// <summary>詞條 roll 值上限。執行期觀測區間為 0~100。</summary>
        private const int RollMax = 100;

        /// <summary>
        /// 計算裝備詞條品質，同時算出兩種指標。
        ///
        /// StatData.Value 不是實際數值，而是 0~100 的 roll 百分位（已由執行期診斷確認）。
        /// 但窄範圍詞條（如 2~3）在 roll 約六成時，顯示值就已經四捨五入到上限，
        /// 因此「肉眼看到的滿」與「roll 滿」是兩件事，兩種指標都算出來。
        ///
        /// 顯示值一律取自 Formula.GetSubstats()，也就是遊戲自己算好的結果，
        /// 不自行用 min/max 內插推算 —— 實測證明那樣推不準。
        /// </summary>
        public static bool TryEvaluate(EquipData data, out Quality q)
        {
            q = default;
            try
            {
                if (data == null) return false;
                return EvaluateCore(data.Substats, GetSubstatConfig(data), Formula.GetSubstats(data), out q);
            }
            catch (Exception ex)
            {
                Plugin.Logger.LogWarning($"評估裝備詞條失敗：{ex.Message}");
                return false;
            }
        }

        /// <summary>
        /// 神器版本。神器的詞條設定是全域固定的（三種區間 2~3 / 1~2% / 1~2%），
        /// 所以 GetArtifactSubstatConfig() 不需要任何參數。
        /// </summary>
        public static bool TryEvaluate(ArtifactData data, out Quality q)
        {
            q = default;
            try
            {
                if (data == null) return false;
                return EvaluateCore(data.Substats, Formula.GetArtifactSubstatConfig(), Formula.GetSubstats(data), out q);
            }
            catch (Exception ex)
            {
                Plugin.Logger.LogWarning($"評估神器詞條失敗：{ex.Message}");
                return false;
            }
        }

        /// <summary>裝備與神器共用的評估核心 —— 兩者的 Substats 都是 List&lt;StatData&gt;。</summary>
        private static bool EvaluateCore(
            Il2CppSystem.Collections.Generic.List<StatData> substats,
            EquipSubstatRuntime cfg,
            Il2CppSystem.Collections.Generic.List<StatValue> actual,
            out Quality q)
        {
            q = default;

            if (substats == null || substats.Count == 0) return false;

            float sum = 0f;
            int counted = 0;

            for (int i = 0; i < substats.Count; i++)
            {
                var s = substats[i];
                if (s == null) continue;

                float pct = s.Value / (float)RollMax;
                if (pct < 0f) pct = 0f;
                else if (pct > 1f) pct = 1f;

                sum += pct;
                counted++;
            }

            if (counted == 0) return false;

            q.RollAverage = sum / counted;
            q.Counted = counted;

            // 滿值判定：拿遊戲算好的實際值跟範圍上限比，不自行推算
            if (cfg != null && actual != null)
            {
                int shownMaxed = 0;

                for (int i = 0; i < substats.Count; i++)
                {
                    var s = substats[i];
                    if (s == null) continue;

                    if (!Formula.GetSubstatRange(s.Type, cfg, out int min, out int max)) continue;
                    if (!TryGetActualValue(actual, s.Type, out float val)) continue;

                    if (AtMax(val, min, max)) shownMaxed++;
                }

                q.ShownMaxed = shownMaxed;
                q.ShownValid = true;
            }

            return true;
        }

        /// <summary>
        /// 減益類詞條（如 MpCost 的 min=-7, max=-10）上限比下限小，方向要反過來。
        /// 浮點比較留一點容差，避免 2.9999 被判成沒滿。
        /// </summary>
        private static bool AtMax(float val, int min, int max)
        {
            const float eps = 0.01f;
            return (max >= min) ? (val >= max - eps) : (val <= max + eps);
        }

        public static EquipSubstatRuntime GetSubstatConfig(EquipData data)
        {
            var runtime = App.ServerRuntime;
            if (runtime == null) return null;

            var config = runtime.GetEquip(data.Id);
            if (config == null) return null;

            return Formula.GetSubstatConfig(config);
        }

        public static bool TryGetActualValue(
            Il2CppSystem.Collections.Generic.List<StatValue> list, StatType type, out float val)
        {
            val = 0f;

            for (int i = 0; i < list.Count; i++)
            {
                var sv = list[i];
                if (sv == null || sv.Type != type) continue;

                var scaled = sv.Value;
                if (scaled == null) return false;

                val = scaled.Value;
                return true;
            }

            return false;
        }

        /// <summary>依分數取得星級記號與顏色；未達最低門檻回傳 false。</summary>
        public static bool TryGetTier(float score, out Color color, out string marker, out string label)
        {
            if (score >= Plugin.CfgTier3.Value)
            {
                color = new Color(1.00f, 0.84f, 0.00f); marker = "★★★"; label = "全滿"; return true;   // 金
            }
            if (score >= Plugin.CfgTier2.Value)
            {
                color = new Color(1.00f, 0.55f, 0.00f); marker = "★★"; label = "優良"; return true;    // 橘
            }
            if (score >= Plugin.CfgTier1.Value)
            {
                color = new Color(0.78f, 0.49f, 1.00f); marker = "★"; label = "不錯"; return true;     // 紫
            }

            color = Color.white; marker = null; label = null; return false;
        }

        public static string ToHex(Color c)
        {
            return $"#{(int)(c.r * 255):X2}{(int)(c.g * 255):X2}{(int)(c.b * 255):X2}";
        }
    }

    internal static class Patches
    {
        // ---- 選用功能：沒按鍵時才強制帶出詞條範圍 ----
        // 玩家一旦自己按住修飾鍵（ALT 看範圍／CTRL 對比／Shift 比較），
        // 就完全退場交還遊戲原本邏輯，否則我們硬設的狀態會跟遊戲的狀態機打架，
        // 導致對比視窗每幀重繪而閃爍。
        public static void ToDescription_Prefix(ref bool showSubstatRange)
        {
            if (InterventionGate.ShouldStandDown()) return;
            showSubstatRange = true;
        }

        public static void SetShowingRange_Postfix(bool __0)
        {
            InterventionGate.GameShowingRange = __0;
        }

        // ---- 彈窗品質總評 ----
        public static void ToDescription_Postfix(EquipData data, EquipConfig config, ref string __result)
        {
            try
            {
                if (Plugin.CfgDiagnostic != null && Plugin.CfgDiagnostic.Value)
                {
                    if (InterventionGate.ShouldStandDown()) return;

                    string diag = Diagnostics.Describe(data, config);
                    if (diag != null) __result = diag + "\n" + __result;
                    return;
                }

                // 玩家按著鍵在對比裝備時不加料，保持版面與原廠一致
                if (InterventionGate.ShouldStandDown()) return;

                if (!Evaluator.TryEvaluate(data, out var q)) return;

                // 靜默寫入 log，不影響畫面 —— 用來核對判定是否與肉眼一致
                Diagnostics.LogQuiet(data, q);

                __result = BuildSummary(q) + __result;
            }
            catch (Exception ex)
            {
                Plugin.Logger.LogWarning($"總評產生失敗(Equip)：{ex.Message}");
            }
        }

        // ---- 神器的品質總評 ----
        public static void ToDescriptionArtifact_Postfix(ArtifactData data, ref string __result)
        {
            try
            {
                if (InterventionGate.ShouldStandDown()) return;
                if (!Evaluator.TryEvaluate(data, out var q)) return;

                __result = BuildSummary(q) + __result;
            }
            catch (Exception ex)
            {
                Plugin.Logger.LogWarning($"總評產生失敗(Artifact)：{ex.Message}");
            }
        }

        /// <summary>裝備與神器共用的總評文字。</summary>
        private static string BuildSummary(Quality q)
        {
            string hex, text;

            if (Evaluator.TryGetTier(q.Score, out var color, out var marker, out var label))
            {
                hex = Evaluator.ToHex(color);
                text = $"{marker} {label}";
            }
            else
            {
                hex = "#9AA0A6";
                text = "詞條";
            }

            if (q.ShownValid)
                text += $"　滿素質 {q.ShownMaxed}/{q.Counted}";

            text += $"　Roll {(int)Math.Round(q.RollAverage * 100f)}%";

            return $"<color={hex}>{text}</color>\n";
        }

        // ---- 背包名稱前加星號 ----
        // 加的是純符號（★）不是富文本標籤，繁中包的字典查不到就原樣返回，
        // 不會破壞已翻譯的中文名稱。顏色仍走 TMP_Text.color 屬性。
        //
        // 背包實際走的是 Draw(IInfoDrawable, bool) 這個泛型入口，
        // 只 patch Draw(EquipData, bool) 攔不到，因此兩個多載都掛。
        /// <summary>ok=false 代表這格不是裝備／神器，此時會把殘留的記號清掉。</summary>
        private static void ApplyMark(UIInventoryItem inst, bool ok, Quality q)
        {
            if (inst == null) return;

            var nameText = inst.Name;
            if (nameText == null) return;

            string marker = null;
            Color color = Color.white;

            if (ok) Evaluator.TryGetTier(q.Score, out color, out marker, out _);

            NameMarker.Apply(inst, nameText, marker, color);
        }

        public static void InventoryDraw_Postfix(UIInventoryItem __instance, EquipData data)
        {
            try
            {
                bool ok = Evaluator.TryEvaluate(data, out var q);
                ApplyMark(__instance, ok, q);
            }
            catch (Exception ex) { Plugin.Logger.LogWarning($"背包標記失敗(Equip)：{ex.Message}"); }
        }

        public static void InventoryDrawArtifact_Postfix(UIInventoryItem __instance, ArtifactData data)
        {
            try
            {
                bool ok = Evaluator.TryEvaluate(data, out var q);
                ApplyMark(__instance, ok, q);
            }
            catch (Exception ex) { Plugin.Logger.LogWarning($"背包標記失敗(Artifact)：{ex.Message}"); }
        }

        /// <summary>
        /// 背包／倉庫實際走的入口。裝備與神器分別 cast，
        /// 兩者都不是（藥水、卡片⋯）時 ok=false，正好把殘留記號清掉。
        /// </summary>
        public static void InventoryDrawAny_Postfix(UIInventoryItem __instance, IInfoDrawable data)
        {
            try
            {
                if (data == null) { ApplyMark(__instance, false, default); return; }

                var equip = data.TryCast<EquipData>();
                if (equip != null)
                {
                    bool okE = Evaluator.TryEvaluate(equip, out var qe);
                    ApplyMark(__instance, okE, qe);
                    return;
                }

                var artifact = data.TryCast<ArtifactData>();
                if (artifact != null)
                {
                    bool okA = Evaluator.TryEvaluate(artifact, out var qa);
                    ApplyMark(__instance, okA, qa);
                    return;
                }

                ApplyMark(__instance, false, default);
            }
            catch (Exception ex) { Plugin.Logger.LogWarning($"背包標記失敗(Any)：{ex.Message}"); }
        }
    }

    /// <summary>
    /// 決定何時該退場。玩家自己按了修飾鍵時，我們一律不介入，
    /// 讓 ALT 看範圍、CTRL 對比、Shift 比較全部保持原廠行為。
    /// </summary>
    internal static class InterventionGate
    {
        /// <summary>由 UIItemPopup.set_showingSubstatRange 的 patch 即時更新。</summary>
        internal static bool GameShowingRange;

        public static bool ShouldStandDown()
        {
            if (GameShowingRange) return true;
            return AnyModifierHeld();
        }

        private static bool AnyModifierHeld()
        {
            try
            {
                return Input.GetKey(KeyCode.LeftAlt) || Input.GetKey(KeyCode.RightAlt)
                    || Input.GetKey(KeyCode.LeftControl) || Input.GetKey(KeyCode.RightControl)
                    || Input.GetKey(KeyCode.LeftShift) || Input.GetKey(KeyCode.RightShift);
            }
            catch
            {
                return false;
            }
        }
    }

    /// <summary>校正用：把每個詞條的執行期真值攤開來看。</summary>
    internal static class Diagnostics
    {
        private static readonly HashSet<string> _logged = new HashSet<string>();
        private static readonly HashSet<string> _quietLogged = new HashSet<string>();
        private static int _quietCount;
        private const int QuietLimit = 40;

        /// <summary>
        /// 只寫 log、完全不動 UI。用來核對「我判定的滿值」與「玩家肉眼看到的滿值」
        /// 是否一致，特別是 GetSubstats 回傳的究竟是精確浮點還是四捨五入後的顯示值。
        /// </summary>
        public static void LogQuiet(EquipData data, Quality q)
        {
            try
            {
                if (_quietCount >= QuietLimit || data == null) return;

                string key = string.IsNullOrEmpty(data.UID) ? data.Id : data.UID;
                if (string.IsNullOrEmpty(key) || !_quietLogged.Add(key)) return;

                var substats = data.Substats;
                if (substats == null || substats.Count == 0) return;

                var cfg = Evaluator.GetSubstatConfig(data);
                var actual = Formula.GetSubstats(data);

                var log = new System.Text.StringBuilder();
                log.AppendLine($"--- {data.Id} | 滿素質判定 {q.ShownMaxed}/{q.Counted} (valid={q.ShownValid}) " +
                               $"Roll={(int)Math.Round(q.RollAverage * 100f)}% ---");

                for (int i = 0; i < substats.Count; i++)
                {
                    var s = substats[i];
                    if (s == null) continue;

                    bool rangeOk = false; int min = 0, max = 0;
                    if (cfg != null)
                    {
                        try { rangeOk = Formula.GetSubstatRange(s.Type, cfg, out min, out max); } catch { }
                    }

                    bool hasVal = actual != null && Evaluator.TryGetActualValue(actual, s.Type, out float v);
                    float val = 0f;
                    if (actual != null) Evaluator.TryGetActualValue(actual, s.Type, out val);

                    log.AppendLine($"    {s.Type,-18} roll={s.Value,3}  actual={(hasVal ? val.ToString("0.####") : "N/A"),-10} " +
                                   $"range={(rangeOk ? min + "~" + max : "FAIL")}");
                }

                _quietCount++;
                Plugin.Logger.LogInfo(log.ToString());
            }
            catch { }
        }

        public static string Describe(EquipData data, EquipConfig config)
        {
            try
            {
                if (data == null) return null;

                var substats = data.Substats;
                if (substats == null || substats.Count == 0) return null;

                var cfg = Evaluator.GetSubstatConfig(data);
                if (cfg == null) return "<color=#FF6666>[診斷] 取不到 substatConfig</color>";

                var actual = Formula.GetSubstats(data);
                if (actual == null) return "<color=#FF6666>[診斷] GetSubstats 回傳 null</color>";

                var line = new System.Text.StringBuilder();
                var log = new System.Text.StringBuilder();

                log.AppendLine($"=== 詞條診斷 id={data.Id} uid={data.UID} ===");
                line.Append("<color=#66CCFF>[診斷] ");

                for (int i = 0; i < substats.Count; i++)
                {
                    var s = substats[i];
                    if (s == null) continue;

                    bool rangeOk = false; int min = 0, max = 0;
                    try { rangeOk = Formula.GetSubstatRange(s.Type, cfg, out min, out max); } catch { }

                    bool hasVal = Evaluator.TryGetActualValue(actual, s.Type, out float val);

                    if (i > 0) line.Append("　｜　");
                    line.Append($"{s.Type} roll={s.Value} 實際={(hasVal ? val.ToString("0.##") : "?")} 範圍={(rangeOk ? min + "~" + max : "FAIL")}");

                    log.AppendLine($"  [{i}] Type={s.Type} roll={s.Value} actual={(hasVal ? val.ToString("0.####") : "N/A")} " +
                                   $"rangeOk={rangeOk} min={min} max={max}");
                }

                line.Append("</color>");

                string key = string.IsNullOrEmpty(data.UID) ? data.Id : data.UID;
                if (!string.IsNullOrEmpty(key) && _logged.Add(key))
                    Plugin.Logger.LogInfo(log.ToString());

                return line.ToString();
            }
            catch (Exception ex)
            {
                return "<color=#FF6666>[診斷] 例外：" + ex.Message + "</color>";
            }
        }
    }

    /// <summary>
    /// 背包格子會重複利用（object pooling），同一格換裝別的道具時
    /// 必須清掉上一件留下的記號與顏色，否則會殘留或疊加。
    /// </summary>
    internal static class NameMarker
    {
        private struct Record
        {
            public Color Mine;
            public Color Original;
        }

        private static readonly Dictionary<IntPtr, Record> _records = new Dictionary<IntPtr, Record>();

        /// <summary>由長到短排列，剝除時必須依此順序比對。</summary>
        private static readonly string[] Markers = { "★★★ ", "★★ ", "★ " };

        public static void Apply(UIInventoryItem owner, TMPro.TMP_Text text, string marker, Color color)
        {
            // ---- 名稱前綴 ----
            string current = text.text ?? string.Empty;
            string bare = Strip(current);
            string want = string.IsNullOrEmpty(marker) ? bare : marker + " " + bare;

            if (!string.Equals(current, want, StringComparison.Ordinal))
                text.text = want;

            // ---- 顏色 ----
            IntPtr key = owner.Pointer;
            Color now = text.color;
            Color original = now;

            if (_records.TryGetValue(key, out var rec) && Same(now, rec.Mine))
                original = rec.Original;

            if (!string.IsNullOrEmpty(marker))
            {
                text.color = color;
                _records[key] = new Record { Mine = color, Original = original };
            }
            else
            {
                text.color = original;
                _records.Remove(key);
            }
        }

        private static string Strip(string s)
        {
            foreach (var m in Markers)
                if (s.StartsWith(m, StringComparison.Ordinal))
                    return s.Substring(m.Length);
            return s;
        }

        private static bool Same(Color a, Color b)
        {
            const float eps = 0.004f;   // 約 1/255
            return Math.Abs(a.r - b.r) < eps
                && Math.Abs(a.g - b.g) < eps
                && Math.Abs(a.b - b.b) < eps;
        }
    }
}
