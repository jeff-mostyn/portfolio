using Rewired;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

public class CustomizationMenuController : MonoBehaviour
{
	public Player p;
	[SerializeField] private EventSystem myEventSystem;
	private PlayerDirectionalInput pInput;
	private GameObject DefaultMenuOption;

	[Header("Camera Control")]
	[SerializeField] private Transform MainCamera;
	[SerializeField] private Transform MainView, CustomizationView;
	public Coroutine cameraMoveCoroutine;
	[SerializeField] private float cameraMoveTime;

	[Header("Tabs")]
	[SerializeField] private Button ColorSelectTab;
	[SerializeField] private Button PortraitSelectTab;
	[SerializeField] private Button CosmeticSelectTab;
	[SerializeField] private GameObject tabControllerPrompts;

	[Header("Color Selection")]
	[SerializeField] private CanvasGroup colorSelectCG;
	[SerializeField] private List<GameObject> colorNodes;
	[SerializeField] private bool p1Active;
	[SerializeField] private Button p1Button, p2Button;
	[SerializeField] private Material unitMat;
	[SerializeField] private GameObject keepHead;
	private GameObject activeCosmetic;
	[SerializeField] private Material keepMat, headMat, flagMat;
	[SerializeField] private MeshRenderer keepRenderer, headRenderer;
	[SerializeField] private List<MeshRenderer> flagRenderers;
	[SerializeField] private GameObject playerControllerPrompts;

	[Header("Player Portrait Selection")]
	[SerializeField] private CanvasGroup portraitSelectCG;
	[SerializeField] private List<GameObject> portraitNodes;

	[Header("Cosmetics Selection")]
	[SerializeField] private CanvasGroup cosmeticSelectCG;
	[SerializeField] private List<GameObject> cosmeticNodes;
	[SerializeField] private GameObject cosmeticKeep;

	[Header("UI References")]
	public GameObject MainMenuCanvas;
	public GameObject CustomizationMenuCanvas;
	[SerializeField] GameObject mainMenuUnit;
	[SerializeField] private GameObject DefaultColorMenuOption, DefaultPortraitMenuOption, DefaultCosmeticsMenuOption;
	//[SerializeField] private GameObject DefaultMenuOption;
	[SerializeField] private BlurInterp blurScript;

	#region System Functions
	private void Awake() {
		SonsOfRa.Events.GeneralEvents.ControllerAssignmentChange += ToggleControllerPrompts;
	}

	// Start is called before the first frame update
	void Start() {
		p = ReInput.players.GetPlayer(PlayerIDs.player1);
		pInput = GetComponent<PlayerDirectionalInput>();

		foreach (GameObject g in colorNodes) {
			g.GetComponent<iconScaler>().SetNewMaxScale(new Vector3(1.3f, 1.3f, 1.3f));
		}
		foreach (GameObject g in portraitNodes) {
			g.GetComponent<iconScaler>().SetNewMaxScale(new Vector3(1.3f, 1.3f, 1.3f));
		}
		foreach (GameObject g in cosmeticNodes) {
			g.GetComponent<iconScaler>().SetNewMaxScale(new Vector3(1.3f, 1.3f, 1.3f));
		}

		unitMat = Instantiate(unitMat);
		keepMat = Instantiate(keepMat);
		headMat = Instantiate(headMat);
		ChangeMenuMaterialColors(PlayerIDs.player1);
		CustomizationMenuCanvas.GetComponent<CanvasGroup>().alpha = 0f;
		CustomizationMenuCanvas.SetActive(false);

		cameraMoveCoroutine = null;
	}

    void Update() {
		// reset selected object if player has controller and nothing is selected
		if (!p.controllers.hasMouse && myEventSystem.currentSelectedGameObject == null) {
			myEventSystem.SetSelectedGameObject(DefaultMenuOption);
		}
		else if (p.controllers.hasMouse && myEventSystem.currentSelectedGameObject != null) {
			myEventSystem.SetSelectedGameObject(null);
		}


		if (p.GetButtonDown(RewiredConsts.Action.UIBack) && cameraMoveCoroutine == null) {
			ReturnToMainMenu();
		}

		// controller input
		if (!p.controllers.hasMouse) {
			// swap tabs
			if (p.GetButtonDown(RewiredConsts.Action.RBumperUI)) {
				if (colorSelectCG.alpha == 1) {
					TAB_OpenPortraitSelect();
				}
				else if (portraitSelectCG.alpha == 1) {
					TAB_OpenCosmeticsSelect();
				}
			}
			else if (p.GetButtonDown(RewiredConsts.Action.LBumperUI)) {
				if (portraitSelectCG.alpha == 1) {
					TAB_OpenColorSelect();
				}
				else if (cosmeticSelectCG.alpha == 1) {
					TAB_OpenPortraitSelect();
				}
			}

			// swap color select player
			if (colorSelectCG.alpha == 1) {
				if (p1Active && p.GetButtonDown(RewiredConsts.Action.RTriggerUI)) {
					COLOR_SetActivePlayer(false);
				}
				else if (!p1Active && p.GetButtonDown(RewiredConsts.Action.LTriggerUI)) {
					COLOR_SetActivePlayer(true);
				}
			}
		}
	}

	private void OnEnable() {
		GetComponentInParent<CanvasGroup>().alpha = 1;
		if (p == null) {
			p = ReInput.players.GetPlayer(PlayerIDs.player1);
		}

		ToggleControllerPrompts();

		TAB_OpenColorSelect();
	}

	private void OnDestroy() {
		SonsOfRa.Events.GeneralEvents.ControllerAssignmentChange -= ToggleControllerPrompts;
	}
	#endregion

	public void StartupVisualAdjsutments() {
		if (cameraMoveCoroutine != null) {
			StopCoroutine(cameraMoveCoroutine);
		}
		cameraMoveCoroutine = StartCoroutine(CameraMove(CustomizationView, false));
	}

	public void ReturnToMainMenu() {
		GetComponentInParent<CanvasGroup>().alpha = 0;
		MainMenuCanvas.GetComponentInChildren<MainMenuController>().enabled = true;
		MainMenuCanvas.GetComponentInChildren<MainMenuController>().ConditionallyEnableButtons(true);
		MainMenuCanvas.GetComponentInChildren<MainMenuController>().UnhighlightButton("customization");
		if (!p.controllers.hasMouse) {
			MainMenuCanvas.GetComponentInChildren<MainMenuController>().SelectOption("customization");
		}

		cosmeticKeep.SetActive(false);
		mainMenuUnit.SetActive(true);

		if (cameraMoveCoroutine != null) {
			StopCoroutine(cameraMoveCoroutine);
		}
		cameraMoveCoroutine = StartCoroutine(CameraMove(MainView, true));
	}

	#region Color Select
	public void COLOR_SetActivePlayer(bool _p1Active) {
		p1Active = _p1Active;

		p1Button.interactable = !p1Active;
		p2Button.interactable = p1Active;

		if (!p.controllers.hasKeyboard) {
			myEventSystem.SetSelectedGameObject(DefaultColorMenuOption);
		}

		RefreshPalettes();
	}

	public void COLOR_SaveSelection(int index) {
		PlayerColorPalette selectedPalette = colorNodes[index].GetComponentInChildren<PlayerColorPalette>();

		if (p1Active) {
			CustomizationManager.Instance.SetPlayerPalette(true, selectedPalette);
		}
		else {
			CustomizationManager.Instance.SetPlayerPalette(false, selectedPalette);
		}

		CustomizationManager.Instance.SaveCustomizations();

		RefreshPalettes();
	}

	private void RefreshPalettes() {
		FillOutColorsByPlayer();
		ChangeMenuMaterialColors(p1Active ? PlayerIDs.player1 : PlayerIDs.player2);

		List<PlayerColorPalette> activePalettes = p1Active ? new List<PlayerColorPalette>(CustomizationManager.Instance.colorPalettes1) : new List<PlayerColorPalette>(CustomizationManager.Instance.colorPalettes2);
		activePalettes.RemoveAll(x => ContentManager.Instance.isLocked(x.name));

		for (int i = 0; i < colorNodes.Count; i++) {
			if (i < activePalettes.Count && 
				(p1Active && activePalettes[i].name == CustomizationManager.Instance.p1PaletteName ||
				!p1Active && activePalettes[i].name == CustomizationManager.Instance.p2PaletteName)) {
				colorNodes[i].GetComponent<Image>().enabled = true;
			}
			else {
				colorNodes[i].GetComponent<Image>().enabled = false;
			}
		}
	}

	private void FillOutColorsByPlayer() {
		List<PlayerColorPalette> activePalettes = p1Active ? new List<PlayerColorPalette>(CustomizationManager.Instance.colorPalettes1) : new List<PlayerColorPalette>(CustomizationManager.Instance.colorPalettes2);
		activePalettes.RemoveAll(x => ContentManager.Instance.isLocked(x.name));

		for (int i = 0; i < colorNodes.Count; i++) {
			if (i < activePalettes.Count) {
				colorNodes[i].GetComponentInChildren<Button>().interactable = true;
				colorNodes[i].GetComponentInChildren<Button>().enabled = true;
				colorNodes[i].GetComponentInChildren<CanvasGroup>().alpha = 1;
				colorNodes[i].GetComponentInChildren<EventTrigger>().enabled = true;
				colorNodes[i].GetComponentInChildren<PlayerColorPalette>().AssignColorPalette(activePalettes[i]);
				colorNodes[i].GetComponentInChildren<PlayerColorPalette>().name = activePalettes[i].name;
			}
			else {
				colorNodes[i].GetComponentInChildren<Button>().interactable = false;
				colorNodes[i].GetComponentInChildren<Button>().enabled = false;
				colorNodes[i].GetComponentInChildren<CanvasGroup>().alpha = 0;
				colorNodes[i].GetComponentInChildren<EventTrigger>().enabled = false;
			}
		}
	}

	private void ChangeMenuMaterialColors(string playerID) {
		unitMat.SetColor("_PalCol1", CustomizationManager.Instance.getPaletteColor(0, playerID));
		unitMat.SetColor("_PalCol2", CustomizationManager.Instance.getPaletteColor(1, playerID));

		mainMenuUnit.GetComponentsInChildren<MeshRenderer>()[0].material = unitMat;
		mainMenuUnit.GetComponentsInChildren<MeshRenderer>()[1].material = unitMat;
		mainMenuUnit.GetComponentsInChildren<SkinnedMeshRenderer>()[0].material = unitMat;

		keepMat.SetColor("_PalCol1", CustomizationManager.Instance.getPaletteColor(0, playerID));
		keepMat.SetColor("_PalCol2", CustomizationManager.Instance.getPaletteColor(1, playerID));
		headMat.SetColor("_PalCol1", CustomizationManager.Instance.getPaletteColor(0, playerID));
		headMat.SetColor("_PalCol2", CustomizationManager.Instance.getPaletteColor(1, playerID));
		flagMat.SetColor("_PalCol1", CustomizationManager.Instance.getPaletteColor(0, playerID));
		flagMat.SetColor("_PalCol2", CustomizationManager.Instance.getPaletteColor(1, playerID));

		keepRenderer.material = keepMat;
		headRenderer.material = headMat;
		foreach(MeshRenderer m in flagRenderers) {
			m.material = flagMat;
		}
	}
	#endregion

	#region Portrait Select
	public void PORTRAIT_SaveSelection(int index) {
		Transform portraitTransform = portraitNodes[index].transform.GetChild(0).GetChild(1);
		Sprite selectedPortrait = portraitTransform.GetComponent<Image>().sprite;

		CustomizationManager.Instance.SetPlayerPortrait(selectedPortrait);

		CustomizationManager.Instance.SaveCustomizations();

		RefreshPortraits();
	}

	private void RefreshPortraits() {
		List<Sprite> activePortraits = new List<Sprite>(CustomizationManager.Instance.portaits);
		activePortraits.RemoveAll(x => ContentManager.Instance.isLocked(x.name));

		for (int i = 0; i < portraitNodes.Count; i++) {
			if (i < activePortraits.Count && activePortraits[i].name == CustomizationManager.Instance.playerPortraitName) {
				portraitNodes[i].GetComponent<Image>().enabled = true;
			}
			else {
				portraitNodes[i].GetComponent<Image>().enabled = false;
			}
		}

		for (int i = 0; i < portraitNodes.Count; i++) {
			Transform portraitTransform = portraitNodes[i].transform.GetChild(0).GetChild(1);   // the player portrait image is second child of first child of node
			if (i < activePortraits.Count) {
				portraitNodes[i].GetComponentInChildren<Button>().interactable = true;
				portraitNodes[i].GetComponentInChildren<Button>().enabled = true;
				portraitNodes[i].GetComponentInChildren<CanvasGroup>().alpha = 1;
				portraitNodes[i].GetComponentInChildren<EventTrigger>().enabled = true;
				portraitTransform.GetComponent<Image>().sprite = activePortraits[i];
			}
			else {
				portraitNodes[i].GetComponentInChildren<Button>().interactable = false;
				portraitNodes[i].GetComponentInChildren<Button>().enabled = false;
				portraitNodes[i].GetComponentInChildren<CanvasGroup>().alpha = 0;
				portraitNodes[i].GetComponentInChildren<EventTrigger>().enabled = false;
			}
		}
	}
	#endregion

	#region Cosmetics Select
	public void COSMETIC_SaveSelection(int index) {
		Transform cosmeticTransform = cosmeticNodes[index].transform;
		GameObject selectedCosmetic = cosmeticTransform.GetComponentInChildren<cosmeticSelection>().Cosmetic;

		CustomizationManager.Instance.SetKeepCosmetic(selectedCosmetic);

		CustomizationManager.Instance.SaveCustomizations();

		RefreshCosmetics();
	}

	private void RefreshCosmetics() {
		List<GameObject> activeCosmetics = new List<GameObject>(CustomizationManager.Instance.cosmetics);
		activeCosmetics.RemoveAll(x => ContentManager.Instance.isLocked(x.name));

		// this turns the buttons selection image on and off if it's the active one or not
		for (int i = 0; i < cosmeticNodes.Count; i++) {
			if (i < activeCosmetics.Count && activeCosmetics[i].name == CustomizationManager.Instance.cosmeticName) {
				cosmeticNodes[i].GetComponent<Image>().enabled = true;
			}
			else {
				cosmeticNodes[i].GetComponent<Image>().enabled = false;
			}
		}

		// this sets the the buttons up and turns them on if they can be selected
		for (int i = 0; i < cosmeticNodes.Count; i++) {
			Transform cosmeticTransform = cosmeticNodes[i].transform.GetChild(0).GetChild(1);   // the image is second child of first child of node
			if (i < activeCosmetics.Count) {
				cosmeticNodes[i].GetComponentInChildren<Button>().interactable = true;
				cosmeticNodes[i].GetComponentInChildren<Button>().enabled = true;
				cosmeticNodes[i].GetComponentInChildren<CanvasGroup>().alpha = 1;
				cosmeticNodes[i].GetComponentInChildren<EventTrigger>().enabled = true;
				cosmeticNodes[i].GetComponentInChildren<cosmeticSelection>().Cosmetic = activeCosmetics[i];
				cosmeticTransform.GetComponent<Image>().sprite = activeCosmetics[i].GetComponent<keepCosmetic>().thumbnail;
			}
			else {
				cosmeticNodes[i].GetComponentInChildren<Button>().interactable = false;
				cosmeticNodes[i].GetComponentInChildren<Button>().enabled = false;
				cosmeticNodes[i].GetComponentInChildren<CanvasGroup>().alpha = 0;
				cosmeticNodes[i].GetComponentInChildren<EventTrigger>().enabled = false;
			}
		}

		InstantiateCosmetic(CustomizationManager.Instance.keepCosmetic);
	}

	private void InstantiateCosmetic(GameObject cosmeticPrefab) {
		if (activeCosmetic != null) {
			Destroy(activeCosmetic);
		}

		keepCosmetic cosmeticScript = cosmeticPrefab.GetComponent<keepCosmetic>();
		string locatorName = "";

		if (cosmeticScript.location == keepCosmetic.cosmeticLocation.hat) {
			locatorName = "HatLocator";
		}
		else if (cosmeticScript.location == keepCosmetic.cosmeticLocation.leftEye) {
			locatorName = "LeftEyeLocator";
		}
		else if (cosmeticScript.location == keepCosmetic.cosmeticLocation.nose) {
			locatorName = "NoseLocator";
		}
		else if (cosmeticScript.location == keepCosmetic.cosmeticLocation.rightEye) {
			locatorName = "RightEyeLocator";
		}
		else if (cosmeticScript.location == keepCosmetic.cosmeticLocation.glasses) {
			locatorName = "GlassesLocator";
		}

		Transform locator = keepHead.transform.Find(locatorName);
		activeCosmetic = Instantiate(cosmeticPrefab, locator);

		activeCosmetic.transform.localPosition = Vector3.zero;
		activeCosmetic.transform.localRotation = Quaternion.identity;
	}
	#endregion

	#region Tab Switching
	public void TAB_OpenColorSelect() {
		if (!p.controllers.hasKeyboard && myEventSystem) {
			myEventSystem.SetSelectedGameObject(DefaultColorMenuOption);
			DefaultMenuOption = DefaultColorMenuOption;
		}

		ToggleColorSelect(true);
		TogglePortraitSelect(false);
		ToggleCosmeticsSelect(false);

		p1Active = true;

		p1Button.interactable = !p1Active;
		p2Button.interactable = p1Active;

		RefreshPalettes();
	}

	public void TAB_OpenPortraitSelect() {
		if (!p.controllers.hasKeyboard) {
			myEventSystem.SetSelectedGameObject(DefaultPortraitMenuOption);
			DefaultMenuOption = DefaultPortraitMenuOption;
		}

		ToggleColorSelect(false);
		TogglePortraitSelect(true);
		ToggleCosmeticsSelect(false);

		RefreshPortraits();
	}


	public void TAB_OpenCosmeticsSelect() {
		if (!p.controllers.hasKeyboard) {
			myEventSystem.SetSelectedGameObject(DefaultCosmeticsMenuOption);
			DefaultMenuOption = DefaultCosmeticsMenuOption;
		}

		ToggleColorSelect(false);
		TogglePortraitSelect(false);
		ToggleCosmeticsSelect(true);

		RefreshCosmetics();
	}
	#endregion

	#region UI Effects
	private IEnumerator CameraMove(Transform target, bool closingMenu) {
		float timer = 0;
		Vector3 startPos = MainCamera.transform.position;
		Vector3 startRot = MainCamera.transform.rotation.eulerAngles;

		while (timer < cameraMoveTime) {
			Vector3 pos = Vector3.Lerp(startPos, target.position, Mathf.SmoothStep(0, 1, timer / cameraMoveTime));
			Vector3 rot = Vector3.Lerp(startRot, target.rotation.eulerAngles, Mathf.SmoothStep(0, 1, timer / cameraMoveTime));

			MainCamera.transform.position = pos;
			MainCamera.transform.rotation = Quaternion.Euler(rot);

			timer += Time.deltaTime;
			yield return null;
		}

		MainCamera.transform.position = target.position;
		MainCamera.transform.rotation = Quaternion.Euler(target.rotation.eulerAngles);

		if (closingMenu) {
			CustomizationMenuCanvas.SetActive(false);
		}

		cameraMoveCoroutine = null;
	}
	#endregion

	#region Enable and Disable Tabs
	private void ToggleColorSelect(bool on) {
		colorSelectCG.alpha = on ? 1f : 0f;
		colorSelectCG.blocksRaycasts = on;
		colorSelectCG.interactable = on;

		ColorSelectTab.interactable = !on;
	}

	private void TogglePortraitSelect(bool on) {
		portraitSelectCG.alpha = on ? 1f : 0f;
		portraitSelectCG.blocksRaycasts = on;
		portraitSelectCG.interactable = on;

		PortraitSelectTab.interactable = !on;
	}

	private void ToggleCosmeticsSelect(bool on) {
		cosmeticSelectCG.alpha = on ? 1f : 0f;
		cosmeticSelectCG.blocksRaycasts = on;
		cosmeticSelectCG.interactable = on;
		cosmeticKeep.SetActive(on);
		mainMenuUnit.SetActive(!on);

		CosmeticSelectTab.interactable = !on;
	}
	#endregion

	private void ToggleControllerPrompts() {
		playerControllerPrompts.SetActive(!p.controllers.hasMouse);
		tabControllerPrompts.SetActive(!p.controllers.hasMouse);
	}
}
