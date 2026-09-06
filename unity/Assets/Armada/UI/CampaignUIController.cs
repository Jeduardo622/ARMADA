using System;
using System.Collections.Generic;
using System.Linq;
using Armada.Client.Core;
using Armada.Client.Services;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Armada.Client.UI
{
    public sealed class CampaignBattleActions
    {
        public Action NextShip, TurnLeft, TurnRight, SlowDown, SpeedUp, Target, Attack, Ammo, Confirm, Undo, Pause, Faster, Harbor;
    }
    /// <summary>Nautical campaign screens. Navigation is supplied by the composition root.</summary>
    public sealed class CampaignUIController : MonoBehaviour
    {
        private static readonly Color Navy = new Color(0.035f, 0.085f, 0.14f);
        private static readonly Color Brass = new Color(0.84f, 0.69f, 0.40f);
        private static readonly Color Parchment = new Color(0.94f, 0.89f, 0.77f);
        private RectTransform _safe;
        private RectTransform _screen;
        private Sprite _harbor;
        public string ScreenName { get; private set; }
        private TMP_Text _battleLog;

        public void ShowBattle(CampaignMissionDefinition mission, int turn, string orders, string conditions,
            bool editable, bool canUndo, CampaignBattleActions actions)
        {
            BeginScreen("Battle", false);
            Panel("Header", _screen, 0, 0.86f, 1, 1, Navy);
            Label("Mission", _screen, $"{mission.Number:00}  {mission.Name.ToUpperInvariant()}  /  TURN {turn} OF {mission.TurnLimit}", 33, Brass, 0.025f, 0.925f, 0.81f, 0.99f);
            Label("Conditions", _screen, conditions, 25, Parchment, 0.025f, 0.865f, 0.80f, 0.925f);
            Button("Harbor", _screen, "RETREAT", actions.Harbor, 0.84f, 0.86f, 0.98f, 0.99f).interactable = editable;
            Panel("OrdersShade", _screen, 0, 0.30f, 0.30f, 0.86f, new Color(Navy.r, Navy.g, Navy.b, 0.96f));
            Label("OrdersHeading", _screen, editable ? "YOUR ORDERS" : "BATTLE UNDER WAY", 28, Brass, 0.022f, 0.78f, 0.28f, 0.85f);
            var orderLabel = Label("Orders", _screen, orders, 26, Parchment, 0.022f, 0.44f, 0.28f, 0.78f);
            orderLabel.alignment = TextAlignmentOptions.TopLeft;
            orderLabel.enableAutoSizing = true; orderLabel.fontSizeMin = 21; orderLabel.fontSizeMax = 26;
            _battleLog = Label("BattleLog", _screen, editable ? "Choose each ship, set a target, then confirm the fleet's orders." : "Resolving the fleet's orders…", 23, Brass, 0.022f, 0.31f, 0.28f, 0.43f);
            Panel("ControlsShade", _screen, 0, 0, 1, 0.30f, Navy);
            var names = new[] { "NextShip", "TurnLeft", "TurnRight", "SlowDown", "SpeedUp", "Target", "Attack", "Ammo", "Undo", "Confirm" };
            var captions = new[] { "NEXT SHIP", "HELM −15°", "HELM +15°", "SPEED −", "SPEED +", "TARGET", "ACTION", "AMMUNITION", "UNDO TURN", "CONFIRM ORDERS" };
            var callbacks = new[] { actions.NextShip, actions.TurnLeft, actions.TurnRight, actions.SlowDown, actions.SpeedUp, actions.Target, actions.Attack, actions.Ammo, actions.Undo, actions.Confirm };
            for (var i = 0; i < names.Length; i++)
            {
                var x = 0.012f + i % 5 * 0.198f;
                var y = i < 5 ? 0.155f : 0.014f;
                var button = Button(names[i], _screen, captions[i], callbacks[i], x, y, x + 0.186f, y + 0.13f, i == 9);
                button.interactable = editable && (i != 8 || canUndo) && (i != 7 || mission.ChainShotAllowed);
            }
            if (!editable)
            {
                Button("Pause", _screen, "PAUSE / PLAY", actions.Pause, 0.57f, 0.31f, 0.78f, 0.44f);
                Button("Faster", _screen, "SPEED", actions.Faster, 0.80f, 0.31f, 0.98f, 0.44f);
            }
        }

        public void ShowResults(CampaignMissionDefinition mission, CampaignPhase phase, int stars,
            string detail, Action retry, Action next, Action harbor)
        {
            BeginScreen("Results");
            var saved = phase == CampaignPhase.Saved;
            var lost = phase == CampaignPhase.Defeat;
            Panel("ResultsShade", _screen, 0.07f, 0.10f, 0.74f, 0.91f, new Color(Navy.r, Navy.g, Navy.b, 0.97f));
            Label("Mission", _screen, mission.Name.ToUpperInvariant(), 32, Brass, 0.11f, 0.78f, 0.70f, 0.87f);
            Label("Title", _screen, lost ? "DEFEAT" : saved ? "VICTORY SECURED" : "VICTORY", 60, Parchment, 0.11f, 0.65f, 0.70f, 0.79f);
            Label("Stars", _screen, lost ? "Regroup, captain. A new course awaits." : $"{stars} OF 3 STARS", 34, Brass, 0.11f, 0.55f, 0.70f, 0.65f);
            Label("Detail", _screen, detail, 27, Parchment, 0.11f, 0.32f, 0.70f, 0.55f);
            if (saved && next != null) Button("Next", _screen, mission.Number == 10 ? "CAMPAIGN CHART" : "NEXT MISSION", next, 0.11f, 0.17f, 0.40f, 0.30f, true);
            else if (phase == CampaignPhase.SaveFailed) Button("RetrySave", _screen, "RETRY SAVE", retry, 0.11f, 0.17f, 0.40f, 0.30f, true);
            else if (lost) Button("RetryBattle", _screen, "TRY AGAIN", retry, 0.11f, 0.17f, 0.40f, 0.30f, true);
            if (saved || lost || phase == CampaignPhase.SaveFailed) Button("Harbor", _screen, "HARBOR", harbor, 0.43f, 0.17f, 0.70f, 0.30f);
        }

        public void ConfirmLeave(string message, Action stay, Action leave)
        {
            BeginScreen("ConfirmLeave");
            Panel("Shade", _screen, 0.07f, 0.15f, 0.76f, 0.85f, Navy);
            Label("Title", _screen, "RETURN TO HARBOR?", 44, Brass, 0.11f, 0.65f, 0.72f, 0.79f);
            Label("Detail", _screen, message, 32, Parchment, 0.11f, 0.40f, 0.72f, 0.64f);
            Button("Stay", _screen, "STAY", stay, 0.11f, 0.23f, 0.39f, 0.36f, true);
            Button("Leave", _screen, "LEAVE BATTLE", leave, 0.43f, 0.23f, 0.72f, 0.36f);
        }

        public void UpdateBattleLog(string text) { if (_battleLog != null) _battleLog.text = text; }

        public void ShowShipyard(UpgradesResponse data, List<InventoryItem> inventory, string notice,
            Action<string, int> purchase, Action back)
        {
            BeginScreen("Shipyard");
            Panel("Shade", _screen, 0, 0, 1, 1, new Color(Navy.r, Navy.g, Navy.b, 0.94f));
            Label("Title", _screen, "THE SHIPYARD", 48, Brass, 0.05f, 0.84f, 0.75f, 0.97f);
            Button("Back", _screen, "HARBOR", back, 0.80f, 0.85f, 0.96f, 0.97f);
            Label("Stores", _screen, string.Join("     ", new[] { "gold", "timber", "ore" }.Select(key => $"{key.ToUpperInvariant()} {inventory.Where(i => i.ItemKey == key).Sum(i => i.Quantity)}")), 29, Parchment, 0.05f, 0.76f, 0.95f, 0.84f);
            var entries = data.Catalog ?? new List<UpgradeCatalogEntry>();
            for (var i = 0; i < entries.Count; i++)
            {
                var entry = entries[i];
                var owned = data.Owned?.Find(o => o.Component == entry.Component)?.Tier ?? 0;
                var next = entry.Tiers?.Find(t => t.Tier == owned + 1);
                var y = 0.69f - i * 0.16f;
                Panel("Fitting", _screen, 0.05f, y - 0.13f, 0.95f, y, new Color(0.075f, 0.15f, 0.21f));
                Label("Component", _screen, $"{entry.Component.ToUpperInvariant()}  /  TIER {owned}", 30, Parchment, 0.07f, y - 0.12f, 0.41f, y - 0.01f);
                var costs = next?.Costs == null ? "Fully fitted" : string.Join(" + ", next.Costs.Select(c => $"{c.Quantity} {c.ItemKey}"));
                Label("Cost", _screen, costs, 28, Brass, 0.42f, y - 0.12f, 0.69f, y - 0.01f);
                var affordable = next?.Costs != null && next.Costs.All(c => inventory.Where(item => item.ItemKey == c.ItemKey).Sum(item => item.Quantity) >= c.Quantity);
                var button = Button("Purchase_" + entry.Component, _screen, next == null ? "MAX TIER" : "FIT TIER " + next.Tier,
                    () => { if (next != null) purchase(entry.Component, next.Tier); }, 0.72f, y - 0.13f, 0.93f, y, affordable);
                button.interactable = affordable;
            }
            Label("Notice", _screen, notice ?? "These fittings improve your fleet in Burning Seas. Earn supplies by completing campaign missions.", 27, Parchment, 0.05f, 0.055f, 0.95f, 0.20f);
        }

        public void Initialize(Sprite harborArt)
        {
            _harbor = harborArt;
            if (_safe != null) return;
            var canvasObject = new GameObject("CampaignCanvas", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
            canvasObject.transform.SetParent(transform, false);
            var canvas = canvasObject.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 20;
            var scaler = canvasObject.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1600, 900);
            scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
            scaler.matchWidthOrHeight = 0.5f;
            _safe = Rect("SafeArea", canvasObject.transform, 0, 0, 1, 1);
            _safe.gameObject.AddComponent<SafeAreaInsets>();
        }

        public void ShowHarbor(string resources, Action chart, Action shipyard, Action captain = null, Action sails = null)
        {
            BeginScreen("Harbor");
            Panel("MenuShade", _screen, 0, 0, 0.49f, 1, new Color(0.02f, 0.05f, 0.085f, 0.58f));
            Label("Eyebrow", _screen, "THE FIRST CAMPAIGN", 28, Brass, 0.065f, 0.82f, 0.55f, 0.88f);
            var title = Label("Title", _screen, "ARMADA", 104, Parchment, 0.06f, 0.66f, 0.60f, 0.82f);
            title.fontStyle = FontStyles.Bold;
            title.characterSpacing = 9;
            Label("Subtitle", _screen, "Chart a course. Command the seas.", 32, Parchment, 0.065f, 0.57f, 0.58f, 0.66f);
            Button("Chart", _screen, "SET SAIL", chart, 0.065f, 0.38f, 0.40f, 0.51f, true);
            Button("Shipyard", _screen, "SHIPYARD", shipyard, 0.43f, 0.38f, 0.765f, 0.51f);
            if (captain != null) Button("Captain", _screen, "CAPTAIN & CREW", captain, 0.065f, 0.22f, 0.40f, 0.35f);
            if (sails != null) Button("Sails", _screen, "SAILS", sails, 0.43f, 0.22f, 0.765f, 0.35f);
            Label("Resources", _screen, resources, 28, Parchment, 0.065f, 0.07f, 0.75f, 0.16f);
            var audio = GetComponent<CampaignAudio>();
            if (audio != null) Button("Sound", _screen, audio.Muted ? "SOUND OFF" : "SOUND ON",
                () => { audio.ToggleMuted(); ShowHarbor(resources, chart, shipyard, captain, sails); }, 0.82f, 0.065f, 0.97f, 0.19f);
        }

        public void ShowCaptain(CaptainProgressionResponse data, List<InventoryItem> inventory, string notice,
            Action<int> train, Action<string, string> assignCrew, Action back)
        {
            BeginScreen("Captain");
            Panel("Shade", _screen, 0, 0, 1, 1, new Color(Navy.r, Navy.g, Navy.b, 0.95f));
            Label("Title", _screen, "CAPTAIN & CREW", 48, Brass, 0.05f, 0.84f, 0.75f, 0.97f);
            Button("Back", _screen, "HARBOR", back, 0.80f, 0.85f, 0.96f, 0.98f);
            var captain = data.Captain;
            var training = data.Training;
            var shards = (inventory ?? new List<InventoryItem>()).Where(i => i.ItemKey == "captain_shard").Sum(i => i.Quantity);
            var capped = captain.Level >= 5 || captain.Xp >= 700 || !captain.NextLevelXp.HasValue;
            Panel("CaptainCard", _screen, 0.05f, 0.23f, 0.49f, 0.81f, new Color(0.075f, 0.15f, 0.21f));
            Label("CaptainName", _screen, captain.Name.ToUpperInvariant(), 39, Parchment, 0.075f, 0.70f, 0.465f, 0.79f);
            Label("CaptainLevel", _screen, $"{captain.Rarity.ToUpperInvariant()}  /  LEVEL {captain.Level} OF 5", 29, Brass, 0.075f, 0.62f, 0.465f, 0.70f);
            Label("CaptainXp", _screen, capped ? $"{captain.Xp} XP  /  MAXIMUM LEVEL" : $"{captain.Xp} XP  /  NEXT LEVEL AT {captain.NextLevelXp} XP", 29, Parchment, 0.075f, 0.54f, 0.465f, 0.62f);
            Label("TrainingCost", _screen, $"CAPTAIN SHARDS: {shards}\nTrain: {training.ShardCost} shard for {training.XpPerTraining} XP", 29, Parchment, 0.075f, 0.41f, 0.465f, 0.54f);
            // Capture the exact offer on screen. Refreshing a profile must not
            // turn a retry into a second paid training sequence.
            var displayedSequence = training.NextSequence;
            var canTrain = !capped && training.ShardCost > 0 && shards >= training.ShardCost && displayedSequence > 0;
            var trainButton = Button("TrainCaptain", _screen, capped ? "MAXIMUM LEVEL" : "TRAIN CAPTAIN", () => train(displayedSequence),
                0.075f, 0.255f, 0.465f, 0.39f, canTrain);
            trainButton.interactable = canTrain;
            var firstMate = data.Crew.FirstMate;
            var gunneryChief = data.Crew.GunneryChief;
            ShowCrewSlot("firstMate", "FIRST MATE", "calico_jim", "Calico Jim", firstMate,
                data.Crew.Roster, 0.79f, () => assignCrew(firstMate == "calico_jim" ? null : "calico_jim", gunneryChief));
            ShowCrewSlot("gunneryChief", "GUNNERY CHIEF", "one_eyed_ella", "One-Eyed Ella", gunneryChief,
                data.Crew.Roster, 0.50f, () => assignCrew(firstMate, gunneryChief == "one_eyed_ella" ? null : "one_eyed_ella"));
            var pair = firstMate == "calico_jim" && gunneryChief == "one_eyed_ella";
            Label("CaptainBonuses", _screen, $"Captain's command: +{captain.Level - 1}% gun damage.  Crew pair: {(pair ? "+2% active" : "+2% when both assigned")}.\nApplies to your next campaign battle; current battles keep their starting crew and level.",
                25, Brass, 0.05f, 0.105f, 0.95f, 0.22f);
            Label("Notice", _screen, notice ?? "First verified mission clears earn 25 XP. Train with captain shards to continue your captain's growth.",
                24, Parchment, 0.05f, 0.015f, 0.95f, 0.10f);
        }

        private void ShowCrewSlot(string slot, string heading, string id, string name, string assigned,
            List<CaptainCrewMember> roster, float top, Action toggle)
        {
            Panel("CrewCard_" + slot, _screen, 0.52f, top - 0.265f, 0.95f, top + 0.02f, new Color(0.075f, 0.15f, 0.21f));
            Label("CrewLabel", _screen, $"{heading}  /  {name}\n{(assigned == id ? "ASSIGNED" : "UNASSIGNED")}", 28, Parchment,
                0.54f, top - 0.10f, 0.93f, top);
            var owned = roster != null && roster.Any(member => member.Id == id && member.Slot == slot);
            var button = Button("Crew_" + slot, _screen, assigned == id ? "UNASSIGN" : "ASSIGN", toggle,
                0.54f, top - 0.245f, 0.93f, top - 0.115f, assigned != id && owned);
            button.interactable = owned;
        }

        public void ShowChart(ISet<string> completed, Action<int> launch, Action back, IReadOnlyDictionary<string, int?> stars = null)
        {
            BeginScreen("Chart");
            Panel("ChartShade", _screen, 0, 0, 1, 1, new Color(Navy.r, Navy.g, Navy.b, 0.86f));
            Label("Title", _screen, "THE CAMPAIGN CHART", 48, Parchment, 0.055f, 0.86f, 0.78f, 0.97f);
            Button("Back", _screen, "HARBOR", back, 0.80f, 0.86f, 0.95f, 0.98f);
            for (var i = 0; i < CampaignCatalog.All.Count; i++)
            {
                var mission = CampaignCatalog.All[i];
                var saved = completed != null && completed.Contains(mission.Code);
                var unlocked = saved || i == 0 || completed != null && completed.Contains(CampaignCatalog.All[i - 1].Code);
                var col = i < 5 ? 0 : 1;
                var row = i % 5;
                var left = col == 0 ? 0.055f : 0.515f;
                var top = 0.81f - row * 0.147f;
                var rating = saved && stars != null && stars.TryGetValue(mission.Code, out var value) && value.HasValue ? $"{value.Value} / 3 STARS  /  REPLAY" : "COMPLETED  /  REPLAY";
                var caption = $"{mission.Number:00}   {mission.Name.ToUpperInvariant()}\n{(saved ? rating : unlocked ? "READY TO SAIL" : "COMPLETE THE PREVIOUS MISSION")}";
                var button = Button($"Mission{mission.Number:00}", _screen, caption, () => launch(mission.Number), left, top - 0.13f, left + 0.43f, top, unlocked);
                button.interactable = unlocked;
            }
        }

        public void ShowBriefing(CampaignMissionDefinition mission, Action launch, Action back)
        {
            BeginScreen("Briefing");
            Panel("BriefingParchment", _screen, 0.04f, 0.08f, 0.59f, 0.94f, Parchment);
            Label("Number", _screen, $"MISSION {mission.Number:00}  /  {mission.TurnLimit} TURNS", 28, Navy, 0.075f, 0.81f, 0.55f, 0.89f);
            Label("Title", _screen, mission.Name.ToUpperInvariant(), 48, Navy, 0.075f, 0.69f, 0.55f, 0.81f).fontStyle = FontStyles.Bold;
            Label("Briefing", _screen, mission.Briefing, 32, Navy, 0.075f, 0.44f, 0.55f, 0.67f);
            Label("Objectives", _screen, mission.Objectives, 30, Navy, 0.075f, 0.28f, 0.55f, 0.44f);
            Button("Launch", _screen, "TO BATTLE", launch, 0.075f, 0.12f, 0.34f, 0.25f, true);
            Button("Back", _screen, "CHART", back, 0.36f, 0.12f, 0.55f, 0.25f);
        }

        public void ShowMessage(string title, string message, Action retry, Action back = null)
        {
            BeginScreen("Message");
            Panel("MessageShade", _screen, 0.07f, 0.13f, 0.75f, 0.86f, new Color(Navy.r, Navy.g, Navy.b, 0.96f));
            Label("Title", _screen, title, 48, Brass, 0.11f, 0.65f, 0.71f, 0.79f);
            Label("Message", _screen, message, 32, Parchment, 0.11f, 0.39f, 0.71f, 0.64f);
            if (retry != null) Button("Retry", _screen, "RETRY", retry, 0.11f, 0.21f, 0.38f, 0.34f, true);
            if (back != null) Button("Back", _screen, "HARBOR", back, 0.42f, 0.21f, 0.69f, 0.34f);
        }

        private void BeginScreen(string name, bool art = true)
        {
            if (_safe == null) Initialize(null);
            if (_screen != null)
            {
                _screen.gameObject.SetActive(false);
                Destroy(_screen.gameObject);
            }
            _screen = Rect(name, _safe, 0, 0, 1, 1);
            ScreenName = name;
            if (!art) return;
            var background = Panel("Background", _screen, 0, 0, 1, 1, Navy);
            if (_harbor != null)
            {
                background.color = Color.white;
                background.sprite = _harbor;
                background.preserveAspect = false;
            }
        }

        private static RectTransform Rect(string name, Transform parent, float x0, float y0, float x1, float y1)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.transform.SetParent(parent, false);
            var rect = (RectTransform)go.transform;
            rect.anchorMin = new Vector2(x0, y0); rect.anchorMax = new Vector2(x1, y1);
            rect.offsetMin = Vector2.zero; rect.offsetMax = Vector2.zero;
            return rect;
        }
        private static Image Panel(string name, Transform parent, float x0, float y0, float x1, float y1, Color color)
        {
            var image = Rect(name, parent, x0, y0, x1, y1).gameObject.AddComponent<Image>();
            image.color = color; image.raycastTarget = false;
            return image;
        }
        private static TMP_Text Label(string name, Transform parent, string text, float size, Color color, float x0, float y0, float x1, float y1)
        {
            var label = Rect(name, parent, x0, y0, x1, y1).gameObject.AddComponent<TextMeshProUGUI>();
            label.text = text; label.fontSize = size; label.color = color;
            label.alignment = TextAlignmentOptions.MidlineLeft;
            label.enableWordWrapping = true; label.richText = false; label.raycastTarget = false;
            return label;
        }
        private Button Button(string name, Transform parent, string caption, Action click, float x0, float y0, float x1, float y1, bool primary = false)
        {
            var rect = Rect(name, parent, x0, y0, x1, y1);
            var image = rect.gameObject.AddComponent<Image>();
            image.color = primary ? Brass : new Color(0.07f, 0.15f, 0.23f, 0.98f);
            var button = rect.gameObject.AddComponent<Button>();
            button.targetGraphic = image;
            var colors = button.colors;
            colors.highlightedColor = new Color(1.08f, 1.08f, 1.08f);
            colors.pressedColor = new Color(0.75f, 0.75f, 0.75f);
            colors.disabledColor = new Color(0.47f, 0.47f, 0.47f, 0.80f);
            button.colors = colors;
            button.onClick.AddListener(() => { GetComponent<CampaignAudio>()?.PlayClick(); click?.Invoke(); });
            var label = Label("Caption", rect, caption, 30, primary ? Navy : Parchment, 0.035f, 0.02f, 0.965f, 0.98f);
            label.alignment = TextAlignmentOptions.Center;
            return button;
        }
    }
}
