using BepInEx;
using HarmonyLib;
using KKAPI;
using KKAPI.Maker;
using KKAPI.Maker.UI;
using KKAPI.Utilities;
using MaterialEditorAPI;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using static Illusion.Utils;
using static MaterialEditorAPI.MaterialAPI;
using System.Linq;

#if AI || HS2
using AIChara;
using ChaClothesComponent = AIChara.CmpClothes;
using ChaCustomHairComponent = AIChara.CmpHair;
#endif

namespace KK_Plugins.MaterialEditor
{
    /// <summary>
    /// Plugin responsible for handling events from the character maker
    /// </summary>
#if KK
    [BepInProcess(Constants.MainGameProcessNameSteam)]
#endif
    [BepInProcess(Constants.MainGameProcessName)]
    [BepInDependency(KoikatuAPI.GUID, KoikatuAPI.VersionConst)]
    [BepInDependency(MaterialEditorPlugin.PluginGUID, MaterialEditorPlugin.PluginVersion)]
    [BepInDependency(XUnity.ResourceRedirector.Constants.PluginData.Identifier, XUnity.ResourceRedirector.Constants.PluginData.Version)]
#if !PH
    [BepInDependency(Sideloader.Sideloader.GUID, Sideloader.Sideloader.Version)]
#endif
    [BepInPlugin(GUID, PluginName, Version)]
    public partial class MEMaker : MaterialEditorUI
    {
        /// <summary>
        /// MaterialEditor Maker plugin GUID
        /// </summary>
        public const string GUID = MaterialEditorPlugin.PluginGUID + ".maker";
        /// <summary>
        /// MaterialEditor Maker plugin name
        /// </summary>
        public const string PluginName = MaterialEditorPlugin.PluginName + " Maker";
        /// <summary>
        /// MaterialEditor Maker plugin version
        /// </summary>
        public const string Version = MaterialEditorPlugin.PluginVersion;
        /// <summary>
        /// Instance of the plugin
        /// </summary>
        public static MEMaker Instance;

        public static MakerButton MaterialEditorButton;
        internal static int currentHairIndex;
        internal static int currentClothesIndex;

        private void Start()
        {
            try
            {
            Instance = this;
            MakerAPI.MakerBaseLoaded += MakerAPI_MakerBaseLoaded;
            MakerAPI.RegisterCustomSubCategories += MakerAPI_RegisterCustomSubCategories;
            MakerAPI.MakerFinishedLoading += (s, e) => ToggleButtonVisibility();
            MakerAPI.ReloadCustomInterface += (s, e) =>
            {
                StartCoroutine(Wait());
                IEnumerator Wait()
                {
                    yield return null;
                    ToggleButtonVisibility();
                }
            };
            MakerAPI.MakerExiting += (s, e) => ColorPalette = null;
            AccessoriesApi.SelectedMakerAccSlotChanged += (s, e) => ToggleButtonVisibility();
            AccessoriesApi.AccessoryKindChanged += (s, e) => ToggleButtonVisibility();
            AccessoriesApi.AccessoryTransferred += (s, e) => ToggleButtonVisibility();
#if KK || KKS
            AccessoriesApi.AccessoriesCopied += (s, e) => ToggleButtonVisibility();
#endif

            Harmony.CreateAndPatchAll(typeof(MakerHooks));
#if KK
            Harmony.CreateAndPatchAll(typeof(KKMakerFinishedLoadingFix));
#endif
            }
            catch (System.Exception ex)
            {
                MaterialEditorPluginBase.Logger.LogError($"[ME] Maker Start() failed: {ex}");
            }
        }

        private System.Collections.IEnumerator DelayedInitUI()
        {
            yield return null; // wait a frame before initializing to avoid conflicts with the maker loading
            try { InitUI(); MaterialEditorPluginBase.Logger.LogInfo("[ME] InitUI completed successfully"); }
            catch (System.Exception ex) { MaterialEditorPluginBase.Logger.LogError($"[ME] InitUI failed: {ex}"); }
        }

        private void MakerAPI_MakerBaseLoaded(object s, RegisterCustomControlsEvent e)
        {
            MaterialEditorPluginBase.Logger.LogInfo("[ME] MakerBaseLoaded fired");
            StartCoroutine(DelayedInitUI());

#if KK || EC || KKS
            MaterialEditorPluginBase.Logger.LogInfo("[ME] Registering maker buttons");
            MaterialEditorButton = MakerAPI.AddAccessoryWindowControl(new MakerButton("Material Editor", null, this));
            MaterialEditorPluginBase.Logger.LogInfo($"[ME] Accessory button registered: {MaterialEditorButton != null}");
            //MaterialEditorButton.GroupingID = "Buttons"; // disabled, this can affect where the button ends up in KK
            MaterialEditorButton.OnClick.AddListener(UpdateUIAccessory);
            e.AddControl(new MakerButton("Material Editor", MakerConstants.Body.All, this)).OnClick.AddListener(() => UpdateUICharacter("body"));
            MaterialEditorPluginBase.Logger.LogInfo("[ME] Body button added");
            e.AddControl(new MakerButton("Material Editor (Body)", MakerConstants.Face.All, this)).OnClick.AddListener(() => UpdateUICharacter("body"));
            e.AddControl(new MakerButton("Material Editor (Face)", MakerConstants.Face.All, this)).OnClick.AddListener(() => UpdateUICharacter("face"));
            e.AddControl(new MakerButton("Material Editor (All)", MakerConstants.Face.All, this)).OnClick.AddListener(() => UpdateUICharacter());

            e.AddControl(new MakerButton("Material Editor", MakerConstants.Clothes.Top, this)).OnClick.AddListener(() => UpdateUIClothes(0));
            e.AddControl(new MakerButton("Material Editor", MakerConstants.Clothes.Bottom, this)).OnClick.AddListener(() => UpdateUIClothes(1));
            e.AddControl(new MakerButton("Material Editor", MakerConstants.Clothes.Bra, this)).OnClick.AddListener(() => UpdateUIClothes(2));
            e.AddControl(new MakerButton("Material Editor", MakerConstants.Clothes.Shorts, this)).OnClick.AddListener(() => UpdateUIClothes(3));
            e.AddControl(new MakerButton("Material Editor", MakerConstants.Clothes.Gloves, this)).OnClick.AddListener(() => UpdateUIClothes(4));
            e.AddControl(new MakerButton("Material Editor", MakerConstants.Clothes.Panst, this)).OnClick.AddListener(() => UpdateUIClothes(5));
            e.AddControl(new MakerButton("Material Editor", MakerConstants.Clothes.Socks, this)).OnClick.AddListener(() => UpdateUIClothes(6));
#if KK
            e.AddControl(new MakerButton("Material Editor", MakerConstants.Clothes.InnerShoes, this)).OnClick.AddListener(() => UpdateUIClothes(7));
            e.AddControl(new MakerButton("Material Editor", MakerConstants.Clothes.OuterShoes, this)).OnClick.AddListener(() => UpdateUIClothes(8));
#elif KKS
            e.AddControl(new MakerButton("Material Editor", MakerConstants.Clothes.OuterShoes, this)).OnClick.AddListener(() => UpdateUIClothes(8));
#elif EC
            e.AddControl(new MakerButton("Material Editor", MakerConstants.Clothes.Shoes, this)).OnClick.AddListener(() => UpdateUIClothes(7));
#endif
            e.AddControl(new MakerButton("Material Editor", MakerConstants.Hair.Back, this)).OnClick.AddListener(() => UpdateUIHair(0));
            e.AddControl(new MakerButton("Material Editor", MakerConstants.Hair.Front, this)).OnClick.AddListener(() => UpdateUIHair(1));
            e.AddControl(new MakerButton("Material Editor", MakerConstants.Hair.Side, this)).OnClick.AddListener(() => UpdateUIHair(2));
            e.AddControl(new MakerButton("Material Editor", MakerConstants.Hair.Extension, this)).OnClick.AddListener(() => UpdateUIHair(3));

            e.AddControl(new MakerButton("Material Editor", MakerConstants.Face.Eyebrow, this)).OnClick.AddListener(() => UpdateUICharacter("mayuge"));
            e.AddControl(new MakerButton("Material Editor", MakerConstants.Face.Eye, this)).OnClick.AddListener(() => UpdateUICharacter("eyeline,hitomi,sirome"));
            e.AddControl(new MakerButton("Material Editor", MakerConstants.Face.Nose, this)).OnClick.AddListener(() => UpdateUICharacter("nose"));
            e.AddControl(new MakerButton("Material Editor", MakerConstants.Face.Mouth, this)).OnClick.AddListener(() => UpdateUICharacter("tang,tooth,canine"));
#if KKS
            e.AddControl(new MakerButton("Material Editor", MakerConstants.Face.Iris, this)).OnClick.AddListener(() => UpdateUICharacter("eyeline,hitomi,sirome"));

#endif
#endif

#if PH
            MaterialEditorButton = MakerAPI.AddAccessoryWindowControl(new MakerButton("Material Editor", null, this));
            MaterialEditorButton.OnClick.AddListener(UpdateUIAccessory);
            e.AddControl(new MakerButton("Material Editor (Body)", MakerConstants.Body.General, this)).OnClick.AddListener(() => UpdateUICharacter("body"));
            e.AddControl(new MakerButton("Material Editor (All)", MakerConstants.Body.General, this)).OnClick.AddListener(() => UpdateUICharacter());
            e.AddControl(new MakerButton("Material Editor", MakerConstants.Body.Nail, this)).OnClick.AddListener(() => UpdateUICharacter("nail"));
            e.AddControl(new MakerButton("Material Editor", MakerConstants.Body.Lower, this)).OnClick.AddListener(() => UpdateUICharacter("mnpk"));

            e.AddControl(new MakerButton("Material Editor (Face)", MakerConstants.Face.General, this)).OnClick.AddListener(() => UpdateUICharacter("head,face"));
            e.AddControl(new MakerButton("Material Editor (All)", MakerConstants.Face.General, this)).OnClick.AddListener(() => UpdateUICharacter());
            e.AddControl(new MakerButton("Material Editor", MakerConstants.Face.Eye, this)).OnClick.AddListener(() => UpdateUICharacter("eye"));
            e.AddControl(new MakerButton("Material Editor", MakerConstants.Face.Eyebrow, this)).OnClick.AddListener(() => UpdateUICharacter("mayuge"));
            e.AddControl(new MakerButton("Material Editor", MakerConstants.Face.Eyelash, this)).OnClick.AddListener(() => UpdateUICharacter("matuge"));
            e.AddControl(new MakerButton("Material Editor", MakerConstants.Face.Mouth, this)).OnClick.AddListener(() => UpdateUICharacter("ha,sita"));

            e.AddControl(new MakerButton("Material Editor", MakerConstants.Wear.Tops, this)).OnClick.AddListener(() => UpdateUIClothes(0));
            e.AddControl(new MakerButton("Material Editor", MakerConstants.Wear.Bottoms, this)).OnClick.AddListener(() => UpdateUIClothes(1));
            e.AddControl(new MakerButton("Material Editor", MakerConstants.Wear.Bra, this)).OnClick.AddListener(() => UpdateUIClothes(2));
            e.AddControl(new MakerButton("Material Editor", MakerConstants.Wear.Shorts, this)).OnClick.AddListener(() => UpdateUIClothes(3));
            e.AddControl(new MakerButton("Material Editor", MakerConstants.Wear.SwimTops, this)).OnClick.AddListener(() => UpdateUIClothes(4));
            e.AddControl(new MakerButton("Material Editor", MakerConstants.Wear.SwimBottoms, this)).OnClick.AddListener(() => UpdateUIClothes(5));
            e.AddControl(new MakerButton("Material Editor", MakerConstants.Wear.SwimWear, this)).OnClick.AddListener(() => UpdateUIClothes(6));
            e.AddControl(new MakerButton("Material Editor", MakerConstants.Wear.Glove, this)).OnClick.AddListener(() => UpdateUIClothes(7));
            e.AddControl(new MakerButton("Material Editor", MakerConstants.Wear.Panst, this)).OnClick.AddListener(() => UpdateUIClothes(8));
            e.AddControl(new MakerButton("Material Editor", MakerConstants.Wear.Socks, this)).OnClick.AddListener(() => UpdateUIClothes(9));
            e.AddControl(new MakerButton("Material Editor", MakerConstants.Wear.Shoes, this)).OnClick.AddListener(() => UpdateUIClothes(10));

            e.AddControl(new MakerButton("Material Editor", MakerConstants.Hair.Set, this)).OnClick.AddListener(() => UpdateUIHair(0));
            e.AddControl(new MakerButton("Material Editor", MakerConstants.Hair.Back, this)).OnClick.AddListener(() => UpdateUIHair(0));
            e.AddControl(new MakerButton("Material Editor", MakerConstants.Hair.Front, this)).OnClick.AddListener(() => UpdateUIHair(1));
            e.AddControl(new MakerButton("Material Editor", MakerConstants.Hair.Side, this)).OnClick.AddListener(() => UpdateUIHair(2));

#endif
            currentHairIndex = 0;
            currentClothesIndex = 0;

            ColorPalette = new MakerColorPalette();
#if KK
            MakerAPI.MakerFinishedLoading += OnMakerFinished;
            _pendingButtonShow = true;
#endif
        }

#if KK
        private static string GetFullPath(Transform t)
        {
            string path = t.name;
            while (t.parent != null) { t = t.parent; path = t.name + "/" + path; }
            return path;
        }

        private void OnMakerFinished(object sender, System.EventArgs e)
        {
            MakerAPI.MakerFinishedLoading -= OnMakerFinished;
            MaterialEditorPluginBase.Logger.LogInfo("[ME] MakerFinishedLoading fired — showing button");
            if (MaterialEditorButton != null)
                MaterialEditorButton.Visible.OnNext(true);
            _pendingDirectButton = true;
        }

        private System.Collections.IEnumerator AddDirectAccessoryButton()
        {
            yield return null;
            try
            {
                // Try different path variations
                var grpParent = GameObject.Find("AcsParentWindow/BasePanel/grpParent")
                    ?? GameObject.Find("04_AccessoryTop/AcsParentWindow/BasePanel/grpParent")
                    ?? GameObject.Find("grpParent");

                if (grpParent == null)
                {
                    // Log all objects named grpParent
                    var allGrp = Resources.FindObjectsOfTypeAll<GameObject>();
                    foreach (var go in allGrp)
                    {
                        if (go.name == "grpParent")
                            MaterialEditorPluginBase.Logger.LogInfo($"[ME] Found grpParent at: {GetFullPath(go.transform)}");
                    }
                    MaterialEditorPluginBase.Logger.LogWarning("[ME] AddDirectAccessoryButton: grpParent not found, logged all candidates above");
                    yield break;
                }
                MaterialEditorPluginBase.Logger.LogInfo("[ME] AddDirectAccessoryButton: found grpParent, adding button");

                // Find an existing button to clone
                var existingButton = grpParent.GetComponentInChildren<UnityEngine.UI.Button>();
                if (existingButton == null)
                {
                    MaterialEditorPluginBase.Logger.LogWarning("[ME] AddDirectAccessoryButton: no existing button to clone");
                    yield break;
                }

                var btnGO = GameObject.Instantiate(existingButton.gameObject, grpParent.transform);
                btnGO.name = "btnMaterialEditor";
                var btnText = btnGO.GetComponentInChildren<UnityEngine.UI.Text>();
                if (btnText != null) btnText.text = "Material Editor";
                var btn = btnGO.GetComponent<UnityEngine.UI.Button>();
                btn.onClick.RemoveAllListeners();
                btn.onClick.AddListener(() => UpdateUIAccessory());
                btnGO.SetActive(true);
                _directButtonAdded = true;
                MaterialEditorPluginBase.Logger.LogInfo("[ME] AddDirectAccessoryButton: button added successfully");
            }
            catch (System.Exception ex)
            {
                MaterialEditorPluginBase.Logger.LogError($"[ME] AddDirectAccessoryButton failed: {ex}");
            }
        }

        private bool _pendingButtonShow = false;
        private int _buttonShowAttempts = 0;
        private bool _pendingDirectButton = false;
        private bool _directButtonAdded = false;

        private void Update()
        {
            if (_pendingDirectButton)
            {
                _pendingDirectButton = false;
                StartCoroutine(AddDirectAccessoryButton());
            }
            if (_pendingButtonShow && MaterialEditorButton != null)
            {
                if (MaterialEditorButton.ControlObject != null)
                {
                    _pendingButtonShow = false;
                    _buttonShowAttempts = 0;
                    MaterialEditorPluginBase.Logger.LogInfo("[ME] Update: ControlObject ready, showing button");
                    MaterialEditorButton.Visible.OnNext(true);
                }
                else
                {
                    _buttonShowAttempts++;
                    if (_buttonShowAttempts % 60 == 0) // only log every 60 frames to avoid spam
                        MaterialEditorPluginBase.Logger.LogInfo($"[ME] Update: waiting for ControlObject... attempt {_buttonShowAttempts}");
                    if (_buttonShowAttempts > 600) // stop trying after about 10 seconds
                    {
                        _pendingButtonShow = false;
                        MaterialEditorPluginBase.Logger.LogWarning("[ME] Update: gave up waiting for ControlObject");
                    }
                }
            }
        }

        // On some KK installs with certain BepisPlugins versions, MakerFinishedLoading never fires.
        // The root cause is that KKAPI's internal waitForSceneFade coroutine waits on
        // Manager.Scene.IsNowLoadingFade returning false, but that never happens on affected installs.
        // This patch forces IsNowLoadingFade to false while in maker so the coroutine can complete.
        [HarmonyPatch]
        private static class KKMakerFinishedLoadingFix
        {
            [HarmonyPostfix]
            [HarmonyPatch(typeof(Manager.Scene), nameof(Manager.Scene.IsNowLoadingFade), MethodType.Getter)]
            private static void IsNowLoadingFade_Postfix(ref bool __result)
            {
                if (KKAPI.Maker.MakerAPI.InsideMaker && !KKAPI.Maker.MakerAPI.InsideAndLoaded)
                    __result = false;
            }

            // Once MakerFinishedLoading fires, make sure the button is visible if ControlObject was built
            [HarmonyPostfix]
            [HarmonyPatch(typeof(KKAPI.Maker.MakerAPI), "OnMakerFinishedLoading")]
            private static void AfterMakerFinishedLoading()
            {
                if (MEMaker.MaterialEditorButton?.ControlObject != null)
                {
                    MaterialEditorPluginBase.Logger.LogInfo("[ME] AfterMakerFinishedLoading: ControlObject exists, forcing visible");
                    MEMaker.MaterialEditorButton.Visible.OnNext(true);
                }
                else
                {
                    MaterialEditorPluginBase.Logger.LogWarning("[ME] AfterMakerFinishedLoading: ControlObject still null");
                }
            }
        }
#endif

        private void MakerAPI_RegisterCustomSubCategories(object sender, RegisterSubCategoriesEvent e)
        {
#if AI || HS2
            MaterialEditorButton = MakerAPI.AddAccessoryWindowControl(new MakerButton("Material Editor", null, this));
            MaterialEditorButton.GroupingID = "Buttons";
            MaterialEditorButton.OnClick.AddListener(UpdateUIAccessory);
            e.AddControl(new MakerButton("Material Editor (Body)", MakerConstants.Body.All, this)).OnClick.AddListener(() => UpdateUICharacter("body"));
            e.AddControl(new MakerButton("Material Editor (Head)", MakerConstants.Body.All, this)).OnClick.AddListener(() => UpdateUICharacter("head"));
            e.AddControl(new MakerButton("Material Editor (All)", MakerConstants.Body.All, this)).OnClick.AddListener(() => UpdateUICharacter());

            MakerCategory hairCategory = new MakerCategory(MakerConstants.Hair.CategoryName, "ME", 0, "Material Editor");
            e.AddControl(new MakerButton("Material Editor (Back)", hairCategory, this)).OnClick.AddListener(() => UpdateUIHair(0));
            e.AddControl(new MakerButton("Material Editor (Front)", hairCategory, this)).OnClick.AddListener(() => UpdateUIHair(1));
            e.AddControl(new MakerButton("Material Editor (Side)", hairCategory, this)).OnClick.AddListener(() => UpdateUIHair(2));
            e.AddControl(new MakerButton("Material Editor (Extension)", hairCategory, this)).OnClick.AddListener(() => UpdateUIHair(3));
            e.AddSubCategory(hairCategory);

            MakerCategory clothesCategory = new MakerCategory(MakerConstants.Clothes.CategoryName, "ME", 0, "Material Editor");
            e.AddControl(new MakerButton("Material Editor (Top)", clothesCategory, this)).OnClick.AddListener(() => UpdateUIClothes(0));
            e.AddControl(new MakerButton("Material Editor (Bottom)", clothesCategory, this)).OnClick.AddListener(() => UpdateUIClothes(1));
            e.AddControl(new MakerButton("Material Editor (Bra)", clothesCategory, this)).OnClick.AddListener(() => UpdateUIClothes(2));
            e.AddControl(new MakerButton("Material Editor (Underwear)", clothesCategory, this)).OnClick.AddListener(() => UpdateUIClothes(3));
            e.AddControl(new MakerButton("Material Editor (Gloves)", clothesCategory, this)).OnClick.AddListener(() => UpdateUIClothes(4));
            e.AddControl(new MakerButton("Material Editor (Pantyhose)", clothesCategory, this)).OnClick.AddListener(() => UpdateUIClothes(5));
            e.AddControl(new MakerButton("Material Editor (Socks)", clothesCategory, this)).OnClick.AddListener(() => UpdateUIClothes(6));
            e.AddControl(new MakerButton("Material Editor (Shoes)", clothesCategory, this)).OnClick.AddListener(() => UpdateUIClothes(7));
            e.AddSubCategory(clothesCategory);

            e.AddControl(new MakerButton("Material Editor", MakerConstants.Face.Mouth, this)).OnClick.AddListener(() => UpdateUICharacter("tang,tooth"));
            e.AddControl(new MakerButton("Material Editor", MakerConstants.Face.Eyes, this)).OnClick.AddListener(() => UpdateUICharacter("eyebase,eyeshadow"));
            e.AddControl(new MakerButton("Material Editor", MakerConstants.Face.HL, this)).OnClick.AddListener(() => UpdateUICharacter("eyebase,eyeshadow"));
            e.AddControl(new MakerButton("Material Editor", MakerConstants.Face.Eyelashes, this)).OnClick.AddListener(() => UpdateUICharacter("eyelashes"));
#endif
        }

        public static void ToggleButtonVisibility()
        {
            if (!MakerAPI.InsideMaker || MaterialEditorButton == null)
            {
                MaterialEditorPluginBase.Logger.LogInfo($"[ME] ToggleButtonVisibility skipped: InsideMaker={MakerAPI.InsideMaker} ButtonNull={MaterialEditorButton == null}");
                return;
            }

#if KK
            MaterialEditorPluginBase.Logger.LogInfo("[ME] ToggleButtonVisibility: showing button (KK always-show)");
            // In KK we always show the button regardless of slot state.
            // GetAccessoryObject can return null even when an accessory is equipped
            // because of how slot indices work in this game version.
            MaterialEditorButton.Visible.OnNext(true);
            // Also queue up the direct button injection if it hasn't been done yet
            if (Instance != null && !Instance._directButtonAdded)
                Instance._pendingDirectButton = true;
#else
            var accessory = MakerAPI.GetCharacterControl().GetAccessoryObject(AccessoriesApi.SelectedMakerAccSlot);
            if (accessory == null)
            {
                MaterialEditorButton.Visible.OnNext(false);
            }
            else
            {
                MaterialEditorButton.Visible.OnNext(true);
            }
#endif
        }

        /// <summary>
        /// Shows the MaterialEditor UI for the character or refreshes the UI if already open
        /// </summary>
        /// <param name="filter"></param>
        public void UpdateUICharacter(string filter = "")
        {
#if KK
            if (!MakerAPI.InsideMaker) return;
#else
            if (!MakerAPI.InsideAndLoaded) return;
#endif

            var chaControl = MakerAPI.GetCharacterControl();
            PopulateList(chaControl.gameObject, new ObjectData(0, MaterialEditorCharaController.ObjectType.Character), filter);
        }

        /// <summary>
        /// Shows the MaterialEditor UI for the specified clothing index or refreshes the UI if already open
        /// </summary>
        /// <param name="index"></param>
        public void UpdateUIClothes(int index) => UpdateUIClothes(index, false);

        /// <summary>
        /// Shows the MaterialEditor UI for the specified clothing index, or skips if autoRefreshOnly and ME isn't showing that slot.
        /// </summary>
        public void UpdateUIClothes(int index, bool autoRefreshOnly)
        {
#if KK
            if (!MakerAPI.InsideMaker) return;
#else
            if (!MakerAPI.InsideAndLoaded) return;
#endif

#if KK || KKS
            if (index > 8)
#elif PH
            if (index > 10)
#else
            if (index > 7)
#endif
                return;

            // When autoRefreshOnly is true (called from hooks), skip the update
            // unless ME is already open on this specific slot
            if (autoRefreshOnly)
            {
                if (!Visible) return;
                if (!(CurrentData is ObjectData autoOd
                    && autoOd.ObjectType == MaterialEditorCharaController.ObjectType.Clothing
                    && autoOd.Slot == index))
                    return;
            }

            var chaControl = MakerAPI.GetCharacterControl();
            var clothes = chaControl.GetClothes(index);
#if PH
            if (clothes == null) return;
#else
            if (clothes == null || clothes.GetComponentInChildren<ChaClothesComponent>() == null) return;
#endif
            PopulateList(clothes, new ObjectData(index, MaterialEditorCharaController.ObjectType.Clothing));
        }

        /// <summary>
        /// Shows the MaterialEditor UI for the currently selected accesory or refreshes the UI if already open
        /// </summary>
        public void UpdateUIAccessory()
        {
#if KK
            if (!MakerAPI.InsideMaker) return;
#else
            if (!MakerAPI.InsideAndLoaded) return;
#endif

            var accessory = MakerAPI.GetCharacterControl().GetAccessoryObject(AccessoriesApi.SelectedMakerAccSlot);
            if (accessory == null)
                Visible = false;
            else
                PopulateList(accessory, new ObjectData(AccessoriesApi.SelectedMakerAccSlot, MaterialEditorCharaController.ObjectType.Accessory));
        }

        /// <summary>
        /// Shows the MaterialEditor UI for the specified hair index or refreshes the UI if already open
        /// </summary>
        public void UpdateUIHair(int index) => UpdateUIHair(index, false);

        /// <summary>
        /// Shows the MaterialEditor UI for the specified hair index, or skips if autoRefreshOnly and ME isn't showing that slot.
        /// </summary>
        public void UpdateUIHair(int index, bool autoRefreshOnly)
        {
#if KK
            if (!MakerAPI.InsideMaker) return;
#else
            if (!MakerAPI.InsideAndLoaded) return;
#endif

            if (index > 3)
                return;

            // When autoRefreshOnly is true (called from hooks), skip the update
            // unless ME is already open on this specific slot
            if (autoRefreshOnly)
            {
                if (!Visible) return;
                if (!(CurrentData is ObjectData autoOd
                    && autoOd.ObjectType == MaterialEditorCharaController.ObjectType.Hair
                    && autoOd.Slot == index))
                    return;
            }

            var chaControl = MakerAPI.GetCharacterControl();
            var hair = chaControl.GetHair(index);
#if PH
            if (hair == null) return;
#else
            if (hair.GetComponent<ChaCustomHairComponent>() == null) return;
#endif
            PopulateList(hair, new ObjectData(index, MaterialEditorCharaController.ObjectType.Hair));
        }

        internal override void ExportTexture(Material mat, string property)
        {
            byte[] texData = null;
            if (CurrentData is ObjectData objData)
            {
                var controller = (MaterialEditorCharaController)CurrentGameObject.GetComponentInParent(typeof(MaterialEditorCharaController));
                if (controller != null)
                {
                    var textureProperty = controller.MaterialTexturePropertyList.FirstOrDefault(x => x.ObjectType == objData.ObjectType && x.CoordinateIndex == controller.GetCoordinateIndex(objData.ObjectType) && x.Slot == objData.Slot && x.Property == property && x.MaterialName == mat.NameFormatted());
                    if (textureProperty?.TexID != null)
                        texData = controller.TextureDictionary[textureProperty.TexID.Value].Data;
                }
            }
            string ext = ImageTypeIdentifier.Identify(texData, "XXX");
            if (texData != null && ext != "XXX")
                base.ExportTextureOriginal(mat, property, ext, texData);
            else
                base.ExportTexture(mat, property);
        }

        public override string GetRendererPropertyValueOriginal(object data, Renderer renderer, RendererProperties property, GameObject go)
        {
            ObjectData objectData = (ObjectData)data;
            return MaterialEditorPlugin.GetCharaController(MakerAPI.GetCharacterControl()).GetRendererPropertyValueOriginal(objectData.Slot, objectData.ObjectType, renderer, property, go);
        }
        public override string GetRendererPropertyValue(object data, Renderer renderer, RendererProperties property, GameObject go)
        {
            ObjectData objectData = (ObjectData)data;
            return MaterialEditorPlugin.GetCharaController(MakerAPI.GetCharacterControl()).GetRendererPropertyValue(objectData.Slot, objectData.ObjectType, renderer, property, go);
        }
        public override void SetRendererProperty(object data, Renderer renderer, RendererProperties property, string value, GameObject go)
        {
            ObjectData objectData = (ObjectData)data;
            MaterialEditorPlugin.GetCharaController(MakerAPI.GetCharacterControl()).SetRendererProperty(objectData.Slot, objectData.ObjectType, renderer, property, value, go);
        }
        public override void RemoveRendererProperty(object data, Renderer renderer, RendererProperties property, GameObject go)
        {
            ObjectData objectData = (ObjectData)data;
            MaterialEditorPlugin.GetCharaController(MakerAPI.GetCharacterControl()).RemoveRendererProperty(objectData.Slot, objectData.ObjectType, renderer, property, go);
        }

        public override float? GetProjectorPropertyValueOriginal(object data, Projector projector, ProjectorProperties property, GameObject gameObject)
        {
            ObjectData objectData = (ObjectData)data;
            return MaterialEditorPlugin.GetCharaController(MakerAPI.GetCharacterControl()).GetProjectorPropertyValueOriginal(objectData.Slot, objectData.ObjectType, projector, property, gameObject);
        }

        public override float? GetProjectorPropertyValue(object data, Projector projector, ProjectorProperties property, GameObject gameObject)
        {
            ObjectData objectData = (ObjectData)data;
            return MaterialEditorPlugin.GetCharaController(MakerAPI.GetCharacterControl()).GetProjectorPropertyValue(objectData.Slot, objectData.ObjectType, projector, property, gameObject);
        }

        public override void SetProjectorProperty(object data, Projector projector, ProjectorProperties property, float value, GameObject gameObject)
        {
            ObjectData objectData = (ObjectData)data;
            MaterialEditorPlugin.GetCharaController(MakerAPI.GetCharacterControl()).SetProjectorProperty(objectData.Slot, objectData.ObjectType, projector, property, value, gameObject);
        }

        public override void RemoveProjectorProperty(object data, Projector projector, ProjectorProperties property, GameObject gameObject)
        {
            ObjectData objectData = (ObjectData)data;
            MaterialEditorPlugin.GetCharaController(MakerAPI.GetCharacterControl()).RemoveProjectorProperty(objectData.Slot, objectData.ObjectType, projector, property, gameObject);
        }
        public override IEnumerable<Projector> GetProjectorList(object data, GameObject gameObject)
        {
            ObjectData objectData = (ObjectData)data;
            return MaterialEditorPlugin.GetCharaController(MakerAPI.GetCharacterControl()).GetProjectorList(objectData.ObjectType, gameObject);
        }

        public override void MaterialCopyEdits(object data, Material material, GameObject go)
        {
            ObjectData objectData = (ObjectData)data;
            MaterialEditorPlugin.GetCharaController(MakerAPI.GetCharacterControl()).MaterialCopyEdits(objectData.Slot, objectData.ObjectType, material, go);
        }
        public override void MaterialPasteEdits(object data, Material material, GameObject go)
        {
            ObjectData objectData = (ObjectData)data;
            MaterialEditorPlugin.GetCharaController(MakerAPI.GetCharacterControl()).MaterialPasteEdits(objectData.Slot, objectData.ObjectType, material, go);
        }
        public override void MaterialCopyRemove(object data, Material material, GameObject go)
        {
            ObjectData objectData = (ObjectData)data;
            MaterialEditorPlugin.GetCharaController(MakerAPI.GetCharacterControl()).MaterialCopyRemove(objectData.Slot, objectData.ObjectType, material, go);
        }

        public override string GetMaterialNameOriginal(object data, Renderer renderer, Material material, GameObject gameObject)
        {
            ObjectData objectData = (ObjectData)data;
            return MaterialEditorPlugin.GetCharaController(MakerAPI.GetCharacterControl()).GetMaterialNamePropertyValueOriginal(objectData.Slot, objectData.ObjectType, renderer, material, gameObject);
        }
        public override void SetMaterialName(object data, Renderer renderer, Material material, string value, GameObject gameObject)
        {
            ObjectData objectData = (ObjectData)data;
            MaterialEditorPlugin.GetCharaController(MakerAPI.GetCharacterControl()).SetMaterialNameProperty(objectData.Slot, objectData.ObjectType, renderer, material, value, gameObject);
        }
        public override void RemoveMaterialName(object data, Renderer renderer, Material material, GameObject gameObject)
        {
            ObjectData objectData = (ObjectData)data;
            MaterialEditorPlugin.GetCharaController(MakerAPI.GetCharacterControl()).RemoveMaterialNameProperty(objectData.Slot, objectData.ObjectType, renderer, material, gameObject);
        }

        public override string GetMaterialShaderNameOriginal(object data, Material material, GameObject go)
        {
            ObjectData objectData = (ObjectData)data;
            return MaterialEditorPlugin.GetCharaController(MakerAPI.GetCharacterControl()).GetMaterialShaderOriginal(objectData.Slot, objectData.ObjectType, material, go);
        }
        public override void SetMaterialShaderName(object data, Material material, string value, GameObject go)
        {
            ObjectData objectData = (ObjectData)data;
            MaterialEditorPlugin.GetCharaController(MakerAPI.GetCharacterControl()).SetMaterialShader(objectData.Slot, objectData.ObjectType, material, value, go);
        }
        public override void RemoveMaterialShaderName(object data, Material material, GameObject go)
        {
            ObjectData objectData = (ObjectData)data;
            MaterialEditorPlugin.GetCharaController(MakerAPI.GetCharacterControl()).RemoveMaterialShader(objectData.Slot, objectData.ObjectType, material, go);
        }

        public override int? GetMaterialShaderRenderQueueOriginal(object data, Material material, GameObject go)
        {
            ObjectData objectData = (ObjectData)data;
            return MaterialEditorPlugin.GetCharaController(MakerAPI.GetCharacterControl()).GetMaterialShaderRenderQueueOriginal(objectData.Slot, objectData.ObjectType, material, go);
        }
        public override void SetMaterialShaderRenderQueue(object data, Material material, int value, GameObject go)
        {
            ObjectData objectData = (ObjectData)data;
            MaterialEditorPlugin.GetCharaController(MakerAPI.GetCharacterControl()).SetMaterialShaderRenderQueue(objectData.Slot, objectData.ObjectType, material, value, go);
        }
        public override void RemoveMaterialShaderRenderQueue(object data, Material material, GameObject go)
        {
            ObjectData objectData = (ObjectData)data;
            MaterialEditorPlugin.GetCharaController(MakerAPI.GetCharacterControl()).RemoveMaterialShaderRenderQueue(objectData.Slot, objectData.ObjectType, material, go);
        }

        public override bool GetMaterialTextureValueOriginal(object data, Material material, string propertyName, GameObject go)
        {
            ObjectData objectData = (ObjectData)data;
            return MaterialEditorPlugin.GetCharaController(MakerAPI.GetCharacterControl()).GetMaterialTextureOriginal(objectData.Slot, objectData.ObjectType, material, propertyName, go);
        }
        public override void SetMaterialTexture(object data, Material material, string propertyName, string filePath, GameObject go)
        {
            ObjectData objectData = (ObjectData)data;
            MaterialEditorPlugin.GetCharaController(MakerAPI.GetCharacterControl()).SetMaterialTextureFromFile(objectData.Slot, objectData.ObjectType, material, propertyName, filePath, go, true);
        }
        public override void RemoveMaterialTexture(object data, Material material, string propertyName, GameObject go)
        {
            ObjectData objectData = (ObjectData)data;
            MaterialEditorPlugin.GetCharaController(MakerAPI.GetCharacterControl()).RemoveMaterialTexture(objectData.Slot, objectData.ObjectType, material, propertyName, go);
        }

        public override Vector2? GetMaterialTextureOffsetOriginal(object data, Material material, string propertyName, GameObject go)
        {
            ObjectData objectData = (ObjectData)data;
            return MaterialEditorPlugin.GetCharaController(MakerAPI.GetCharacterControl()).GetMaterialTextureOffsetOriginal(objectData.Slot, objectData.ObjectType, material, propertyName, go);
        }
        public override void SetMaterialTextureOffset(object data, Material material, string propertyName, Vector2 value, GameObject go)
        {
            ObjectData objectData = (ObjectData)data;
            MaterialEditorPlugin.GetCharaController(MakerAPI.GetCharacterControl()).SetMaterialTextureOffset(objectData.Slot, objectData.ObjectType, material, propertyName, value, go);
        }
        public override void RemoveMaterialTextureOffset(object data, Material material, string propertyName, GameObject go)
        {
            ObjectData objectData = (ObjectData)data;
            MaterialEditorPlugin.GetCharaController(MakerAPI.GetCharacterControl()).RemoveMaterialTextureOffset(objectData.Slot, objectData.ObjectType, material, propertyName, go);
        }

        public override Vector2? GetMaterialTextureScaleOriginal(object data, Material material, string propertyName, GameObject go)
        {
            ObjectData objectData = (ObjectData)data;
            return MaterialEditorPlugin.GetCharaController(MakerAPI.GetCharacterControl()).GetMaterialTextureScaleOriginal(objectData.Slot, objectData.ObjectType, material, propertyName, go);
        }
        public override void SetMaterialTextureScale(object data, Material material, string propertyName, Vector2 value, GameObject go)
        {
            ObjectData objectData = (ObjectData)data;
            MaterialEditorPlugin.GetCharaController(MakerAPI.GetCharacterControl()).SetMaterialTextureScale(objectData.Slot, objectData.ObjectType, material, propertyName, value, go);
        }
        public override void RemoveMaterialTextureScale(object data, Material material, string propertyName, GameObject go)
        {
            ObjectData objectData = (ObjectData)data;
            MaterialEditorPlugin.GetCharaController(MakerAPI.GetCharacterControl()).RemoveMaterialTextureScale(objectData.Slot, objectData.ObjectType, material, propertyName, go);
        }

        public override Color? GetMaterialColorPropertyValueOriginal(object data, Material material, string propertyName, GameObject go)
        {
            ObjectData objectData = (ObjectData)data;
            return MaterialEditorPlugin.GetCharaController(MakerAPI.GetCharacterControl()).GetMaterialColorPropertyValueOriginal(objectData.Slot, objectData.ObjectType, material, propertyName, go);
        }
        public override void SetMaterialColorProperty(object data, Material material, string propertyName, Color value, GameObject go)
        {
            ObjectData objectData = (ObjectData)data;
            MaterialEditorPlugin.GetCharaController(MakerAPI.GetCharacterControl()).SetMaterialColorProperty(objectData.Slot, objectData.ObjectType, material, propertyName, value, go);
        }
        public override void RemoveMaterialColorProperty(object data, Material material, string propertyName, GameObject go)
        {
            ObjectData objectData = (ObjectData)data;
            MaterialEditorPlugin.GetCharaController(MakerAPI.GetCharacterControl()).RemoveMaterialColorProperty(objectData.Slot, objectData.ObjectType, material, propertyName, go);
        }

        public override float? GetMaterialFloatPropertyValueOriginal(object data, Material material, string propertyName, GameObject go)
        {
            ObjectData objectData = (ObjectData)data;
            return MaterialEditorPlugin.GetCharaController(MakerAPI.GetCharacterControl()).GetMaterialFloatPropertyValueOriginal(objectData.Slot, objectData.ObjectType, material, propertyName, go);
        }
        public override void SetMaterialFloatProperty(object data, Material material, string propertyName, float value, GameObject go)
        {
            ObjectData objectData = (ObjectData)data;
            MaterialEditorPlugin.GetCharaController(MakerAPI.GetCharacterControl()).SetMaterialFloatProperty(objectData.Slot, objectData.ObjectType, material, propertyName, value, go);
        }
        public override void RemoveMaterialFloatProperty(object data, Material material, string propertyName, GameObject go)
        {
            ObjectData objectData = (ObjectData)data;
            MaterialEditorPlugin.GetCharaController(MakerAPI.GetCharacterControl()).RemoveMaterialFloatProperty(objectData.Slot, objectData.ObjectType, material, propertyName, go);
        }

        public override bool? GetMaterialKeywordPropertyValueOriginal(object data, Material material, string propertyName, GameObject go)
        {
            ObjectData objectData = (ObjectData)data;
            return MaterialEditorPlugin.GetCharaController(MakerAPI.GetCharacterControl()).GetMaterialKeywordPropertyValueOriginal(objectData.Slot, objectData.ObjectType, material, propertyName, go);
        }
        public override void SetMaterialKeywordProperty(object data, Material material, string propertyName, bool value, GameObject go)
        {
            ObjectData objectData = (ObjectData)data;
            MaterialEditorPlugin.GetCharaController(MakerAPI.GetCharacterControl()).SetMaterialKeywordProperty(objectData.Slot, objectData.ObjectType, material, propertyName, value, go);
        }
        public override void RemoveMaterialKeywordProperty(object data, Material material, string propertyName, GameObject go)
        {
            ObjectData objectData = (ObjectData)data;
            MaterialEditorPlugin.GetCharaController(MakerAPI.GetCharacterControl()).RemoveMaterialKeywordProperty(objectData.Slot, objectData.ObjectType, material, propertyName, go);
        }
    }
}
