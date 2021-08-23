using Rewired;
using Rewired.Platforms.XboxOne;
using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class Human_PlayerController : PlayerController {
	#region declarations

	//  PLEASE NOTE: Player0 is Player 1
	private Player rewiredPlayer;
	private Joystick joystick = null;
	private XboxOneGamepadExtension gamepadExtension = null;
	private Camera mainCam;

	// ---------------------- Constants ----------------------
	private const float INPUT_DEADZONE = 0.85f;
	private const float INPUT_ZERO_TIME = 0.25f;
	private const float RETICULE_MOVE_DELAY = 0.25f;

	// ---------------------- States ----------------------
	public enum radialStates { main, chooseUnitLane, placeTower, confirmActiveBlessing, chooseBlessingLane, upgrades, none }
	private radialStates rState = radialStates.none;

	// ---------------------- Radial UI ----------------------
	// visual elements
	private Color radialOn, radialOff;
	[Header("Radial UI Elements")]
	[SerializeField] private RectTransform placementRect;
	private Vector3 initialRadialPosition, initialRadialLocalPosition;
	private bool locationSet;
	[SerializeField] private GameObject radialRing;
	[SerializeField] private Image radialTop, radialMid, radialBot, radialRingHalf;
	[SerializeField] private GameObject SingleCostTextBoxes, singleCostGold, singleCostFavor;
	[SerializeField] private GameObject NoCostTextBoxes;
	[SerializeField] private GameObject tooltipUI;
	[SerializeField] private GameObject selector, pointer, maskedIcons;
	[SerializeField] private List<GameObject> mainIconGroups;
	[SerializeField] private List<Image> mainSummaryIcons;
	[SerializeField] private List<GameObject> mainIconsIndiv, mainIconsIndivMasked;
	private List<Image> unitIcons, unitIconsMasked, towerIcons, towerIconsMasked;
	[SerializeField] private GameObject blessingCountdownGroup;
	[SerializeField] private List<Text> blessingCountdowns;
	[SerializeField] private GameObject laneIcons, laneIconsMasked;
	[SerializeField] private List<GameObject> laneArrows;
	[SerializeField] private GameObject upgradeIcons, upgradeIconsMasked;
	[SerializeField] private GameObject buttonPrompts;
	[SerializeField] private Image radialBlur;
	[SerializeField] private Sprite blurThirds, blurHalves;
	public Dictionary<int, bool> baseZoneLocks = new Dictionary<int, bool> {
		{0, false }, {1, false}, {2, true}, {3, true}, {4, false}, {5, false}, {6, false}, {7, false}, {8, false},
		{9, true}, {10, true}, {11, true}, {12, false}
	};

	[Header("Radial Variables")]
	[SerializeField] private float radialScaleTime;
	private Vector3 originalRadialScale;

	[Header("Tooltip and Info Elements")]
	[SerializeField] private TextMeshProUGUI tooltipText;
	[SerializeField] private TextMeshProUGUI tooltipNameText, singleCost;
	private blessingDurationsDisplay blessingDurations;

	[Header("Misc. References")]
	private PlayerCameraControl camControl;

	// UI-related variables
	public bool goldCost = true;
	private bool p2MirrorLanes = false;     // used to flip the UI for player 2's lane selection
	private bool spawningBlessingUnit = false;  // turned on when the lane choice state is being used for spawning blessing units
	private UnitBlessing_I unitBlessingInUse = null;    // holding onto the Unit Blessing being used while in the lane choice function
    private bool hudHidden = false;

	// UI Input variables
	private float angle;
	private int zone = 0, oldZone = 0;
	private float inputZeroTimeCounter = 0f;
    private float reticuleMoveDelay = 0;
	public bool isPauseMenuOpen = false;

	// UI Scripts
	private PlayerDirectionalInput playerInput;

	// ---------------------- Audio ----------------------
	[Header("FX")]
	public List<AudioClip> soundClips;
	private AudioSource soundPlayer;
	public LayerMask pokeEffectMask;
	public GameObject pokeFX;

	#endregion declarations

	#region System Functions
	protected override void Start() {
		base.Start();

		inputList = new List<string>();

		if (!canSpawnInMid) {
			for (int i = 0; i <= 5; i++) {
				laneArrows[i].SetActive(false);
			}
			for (int i = 6; i <= 9; i++) {
				laneArrows[i].SetActive(true);
			}
		}

        // handle online player assignment
        if (!SettingsManager.Instance.GetIsOnline()) {
            rewiredPlayer = ReInput.players.GetPlayer(rewiredPlayerKey);
        }
        else {
            if (rewiredPlayerKey == OnlineManager.Instance.fakePlayerKey) {
                rewiredPlayer = ReInput.players.GetPlayer(PlayerIDs.player1);
            }
            else {
                rewiredPlayer = null;
                buttonPrompts.SetActive(false);
                meter.TurnOffPrompts();
                radialMenu.SetActive(false);
            }
        }

		activeBlessing = null;
		soundPlayer = GetComponent<AudioSource>();

		for (int i = 0; i < blessings.Length; i++) {
			blessings[i] = Instantiate(blessings[i]);
			blessings[i].GetComponent<Blessing>().UIIcon = mainIconsIndiv[7 - i].GetComponent<Image>(); // account for loadout manager order being different
			blessings[i].GetComponent<Blessing>().UIIconMasked = mainIconsIndivMasked[7 - i].GetComponent<Image>();
			blessings[i].GetComponent<Blessing>().countdownTimer = blessingCountdowns[i];
		}
		blessingScripts = new Blessing[LOADOUT_COUNT];
		for (int i = 0; i < blessingScripts.Length; i++) {
			blessingScripts[i] = blessings[i].GetComponent<Blessing>();
		}
		blessingNames = new string[LOADOUT_COUNT];
		for (int i = 0; i < blessingNames.Length; i++) {
			blessingNames[i] = Lang.blessingTitles[blessingScripts[i].bID][SettingsManager.Instance.language];
		}

		// set up UI
		radVis = GetComponent<RadialVisuals>();
		playerInput = GetComponent<PlayerDirectionalInput>();
		radialOff = radialMid.color;
		radialOn = radialMid.color;
		radialOff.a = Constants.RADIAL_DEFAULT_ALPHA;
		radialOn.a = Constants.RADIAL_SELECTED_ALPHA;
		originalRadialScale = radialMenu.transform.localScale;

		radVis.SetUp(this);

		radVis.ApplyLockIcon(baseZoneLocks);

		blessingDurations = GetComponent<blessingDurationsDisplay>();

		// set up front-level icons
		unitIcons = new List<Image>();
		unitIconsMasked = new List<Image>();
		towerIcons = new List<Image>();
		towerIconsMasked = new List<Image>();
		for (int i = 0; i < 4; i++) {
			unitIcons.Add(mainIconsIndiv[i].GetComponent<Image>());
			unitIconsMasked.Add(mainIconsIndivMasked[i].GetComponent<Image>());
			towerIcons.Add(mainIconsIndiv[i + 8].GetComponent<Image>());
			towerIconsMasked.Add(mainIconsIndivMasked[i + 8].GetComponent<Image>());
		}

		// get camera ref
		camControl = GameManager.Instance.CameraParent.GetComponent<PlayerCameraControl>();
		mainCam = GameObject.Find("Main Camera").GetComponent<Camera>();

#if UNITY_XBOXONE
		joystick = rewiredPlayer.controllers.Joysticks[0];
		gamepadExtension = joystick.GetExtension<XboxOneGamepadExtension>();
#else
        locationSet = false;
		StartCoroutine(LateSetMouseDefault(0.1f));
#endif
	}

	// Update is called once per frame
	void Update() {
		//check to make sure that this will only occur when the game isn't paused
		if (Time.timeScale == 1 && rewiredPlayer != null && !isPauseMenuOpen) {
			radVis.setCurrencyValues(gold, favor);

			if (rState == radialStates.main) {
				manageRadialBase();
				updateMainVisual();
				updateMainFunctional();
			}
			else if (rState == radialStates.chooseUnitLane) {
				manageRadialBase();
				updateChooseLaneVisual();
				updateChooseUnitLaneFunctional();
			}
			else if (rState == radialStates.placeTower) {
				updatePlaceTowerVisual();
				updatePlaceTowerFunctional();
			}
			else if (rState == radialStates.confirmActiveBlessing) {
				updateConfirmActivePowerVisual();
				updateConfirmActivePowerFunctional();
			}
			else if (rState == radialStates.chooseBlessingLane) {
				manageRadialBase();
				updateChooseLaneVisual();
				updateChooseBlessingLaneFunctional();
			}
			else if (rState == radialStates.upgrades) {
				manageRadialBase();
				updateUpgradesVisual();
				updateUpgradesFunctional();
			}
			else if (rState == radialStates.none) {
				manageRadialBase();
				updateMainVisual();
				updateMainFunctional();

				if (rewiredPlayer.GetButtonDown(RewiredConsts.Action.Upgrade) && meter.CanUpgrade()
				&& (rState == radialStates.main || rState == radialStates.chooseUnitLane)) {
					rState = radialStates.upgrades;
					primeUpgradeRadial();
					updateUpgradesVisual();
				}

				if (rewiredPlayer.GetButtonDown(RewiredConsts.Action.LClick)) {
					// make raycast
					SpawnPokeEffect();
				}
			}

			if (SettingsManager.Instance.GetIsSinglePlayer() || SettingsManager.Instance.GetIsOnline()) {
				camControl.SetPlayerRadialOpen(radialMenu.activeSelf);
				if (!radialMenu.activeSelf) {
					camControl.PanCamera(new Vector2(playerInput.GetHorizNonRadialInput(rewiredPlayer), playerInput.GetVertNonRadialInput(rewiredPlayer)));
				}

                if (rewiredPlayer.GetButtonDown(RewiredConsts.Action.DisableUI)) {
                    if (!hudHidden) {
                        Debug.Log("cull on");
                        foreach (GameObject g in GameObject.FindGameObjectsWithTag("CullableUI")) {
                            g.GetComponent<CanvasGroup>().alpha = 0;
                        }
                    }
                    else {
                        foreach (GameObject g in GameObject.FindGameObjectsWithTag("CullableUI")) {
                            g.GetComponent<CanvasGroup>().alpha = 1;
                        }
                    }

                    hudHidden = !hudHidden;
                }
			}

#if UNITY_EDITOR
			if (Input.GetKeyDown(KeyCode.Z)) {
				GameManager.Instance.p2.GetComponentInChildren<KeepManager>().takeDamage(int.MaxValue);
			}
			if (Input.GetKeyDown(KeyCode.M)) {
				GameManager.Instance.p1.GetComponentInChildren<KeepManager>().takeDamage(int.MaxValue);
			}
			if(Input.GetKeyDown(KeyCode.B)){
				SetFavor(50);
			}
#endif
		}
		else if (Time.timeScale == 0) {
			radialMenu.SetActive(false);
		}
	}
	#endregion

	private void manageRadialBase() {
#if !UNITY_XBOXONE
		if (rewiredPlayer.controllers.hasMouse 
            && rewiredPlayer.GetButtonDown(RewiredConsts.Action.OpenRadial) 
            && (SettingsManager.Instance.GetIsSinglePlayer() || SettingsManager.Instance.GetIsOnline())) {
			MoveRadialToMouse();
		}
		else if (!rewiredPlayer.controllers.hasMouse
            && radialMenu.transform.position != initialRadialPosition 
            && (SettingsManager.Instance.GetIsSinglePlayer() || SettingsManager.Instance.GetIsOnline()) 
            && locationSet) {
			radialMenu.transform.localPosition = initialRadialLocalPosition;
			radialMenu.transform.position = initialRadialPosition;
		}
#endif

		float horizInput = playerInput.GetHorizRadialInput(rewiredPlayer, rState);
		float vertInput = playerInput.GetVertRadialInput(rewiredPlayer, rState);

		if (((Mathf.Sqrt((horizInput * horizInput) + (vertInput * vertInput)) >= INPUT_DEADZONE && !isStunned) || rState == radialStates.upgrades
			|| (rewiredPlayer.controllers.hasMouse && rewiredPlayer.GetButton(RewiredConsts.Action.OpenRadial)))
			&& GameManager.Instance.gameStarted) {
			if (rState == radialStates.none) {
				rState = radialStates.main; // when opening menu from close, set it to main
			}

			if (!radialMenu.activeSelf) {
				ScaleRadial(true);
			}

			radialMenu.SetActive(true);
			buttonPrompts.SetActive(true);

			tooltipUI.gameObject.SetActive(true);
			if (rState != radialStates.upgrades) {
				SingleCostTextBoxes.SetActive(true);
				NoCostTextBoxes.SetActive(false);
				if (zone >= 5 && zone <= 8 && rState != radialStates.chooseUnitLane) { // blessings
					singleCostGold.SetActive(false);
					singleCostFavor.SetActive(true);
				}
				else {
					singleCostGold.SetActive(true);
					singleCostFavor.SetActive(false);
				}
			}
			else {
				SingleCostTextBoxes.SetActive(false);
				NoCostTextBoxes.SetActive(true);
			}

			inputZeroTimeCounter = 0;

			angle = Mathf.Atan2(vertInput, horizInput) * Mathf.Rad2Deg;
			radVis.RadialPointerRotation(ref pointer, angle);
			if (zone != oldZone) {
				if (shouldPlayRadialClickFeedback()) {
					playRadialHoverFeedback();
				}
				radVis.RadialSelectorSnapRotation(ref selector, zone, rState);
			}

			maskedIcons.transform.eulerAngles = new Vector3(selector.transform.rotation.x, selector.transform.rotation.y, 0f);

			oldZone = zone;

			// set highlighted segment
			if (!canSpawnInMid && (rState == radialStates.chooseBlessingLane || rState == radialStates.chooseUnitLane)) {
				radialTop.color = radialOff;
				radialMid.color = radialOff;
				radialBot.color = radialOff;
				radialRingHalf.color = radialOn;
				radialBlur.sprite = blurHalves;
			}
			else if (rewiredPlayerKey == PlayerIDs.player2 && (rState == radialStates.chooseUnitLane || rState == radialStates.chooseBlessingLane)) {
				radialRingHalf.color = radialOff;
				radialBlur.sprite = blurThirds;
				if (angle >= Constants.RADIAL_ZONE_ANGLES[3] - Constants.RADIAL_ANGLE_RANGE_DEG
					&& angle < Constants.RADIAL_ZONE_ANGLES[6] + Constants.RADIAL_ANGLE_RANGE_DEG) {
					radialTop.color = radialOn;
					radialMid.color = radialOff;
					radialBot.color = radialOff;
				}
				else if (angle >= Constants.RADIAL_ZONE_ANGLES[7] - Constants.RADIAL_ANGLE_RANGE_DEG && angle < Constants.RADIAL_ZONE_ANGLES[10] + Constants.RADIAL_ANGLE_RANGE_DEG) {
					radialTop.color = radialOff;
					radialMid.color = radialOff;
					radialBot.color = radialOn;
				}
				else {
					radialTop.color = radialOff;
					radialMid.color = radialOn;
					radialBot.color = radialOff;
				}
			}
			//  ------------ normal ------------
			else {
				radialRingHalf.color = radialOff;
				radialBlur.sprite = blurThirds;
				if (angle >= Constants.RADIAL_TOP3_MIN && angle < Constants.RADIAL_TOP3_MAX) {
					radialTop.color = radialOn;
					radialMid.color = radialOff;
					radialBot.color = radialOff;
				}
				else if (angle >= Constants.RADIAL_MID3_MIN && angle < Constants.RADIAL_MID3_MAX) {
					radialTop.color = radialOff;
					radialMid.color = radialOn;
					radialBot.color = radialOff;
				}
				else {
					radialTop.color = radialOff;
					radialMid.color = radialOff;
					radialBot.color = radialOn;
				}
			}

			// select zone
			// ---------------- stupid player 2 fucking stupid unit lane bullshit ------------------
			if (rewiredPlayerKey == PlayerIDs.player2 && (rState == radialStates.chooseUnitLane || rState == radialStates.chooseBlessingLane)) {
				if (angle >= Constants.RADIAL_ZONE_ANGLES[3] - Constants.RADIAL_ANGLE_RANGE_DEG
					&& angle < Constants.RADIAL_ZONE_ANGLES[6] + Constants.RADIAL_ANGLE_RANGE_DEG) {
					if (angle < (Constants.RADIAL_ZONE_ANGLES[3] + Constants.RADIAL_ANGLE_RANGE_DEG)) {
						zone = 3;
					}
					else if (angle < (Constants.RADIAL_ZONE_ANGLES[4] + Constants.RADIAL_ANGLE_RANGE_DEG)) {
						zone = 4;
					}
					else if (angle < (Constants.RADIAL_ZONE_ANGLES[5] + Constants.RADIAL_ANGLE_RANGE_DEG)) {
						zone = 5;
					}
					else {
						zone = 6;
					}
				}
				else if (angle >= Constants.RADIAL_ZONE_ANGLES[7] - Constants.RADIAL_ANGLE_RANGE_DEG && angle < Constants.RADIAL_ZONE_ANGLES[10] + Constants.RADIAL_ANGLE_RANGE_DEG) {
					if (angle < (Constants.RADIAL_ZONE_ANGLES[7] + Constants.RADIAL_ANGLE_RANGE_DEG)) {
						zone = 7;
					}
					else if (angle < (Constants.RADIAL_ZONE_ANGLES[8] + Constants.RADIAL_ANGLE_RANGE_DEG)) {
						zone = 8;
					}
					else if (angle < (Constants.RADIAL_ZONE_ANGLES[9] + Constants.RADIAL_ANGLE_RANGE_DEG)) {
						zone = 9;
					}
					else {
						zone = 10;
					}
				}
				else {
					if (angle > (Constants.RADIAL_ZONE_ANGLES[1] - Constants.RADIAL_ANGLE_RANGE_DEG) &&
						angle < (Constants.RADIAL_ZONE_ANGLES[2] - Constants.RADIAL_ANGLE_RANGE_DEG)) {
						zone = 1;
					}
					else if (angle > (Constants.RADIAL_ZONE_ANGLES[2] - Constants.RADIAL_ANGLE_RANGE_DEG) &&
						angle < (Constants.RADIAL_ZONE_ANGLES[3] - Constants.RADIAL_ANGLE_RANGE_DEG)) {
						zone = 2;
					}
					else if (angle < (Constants.RADIAL_ZONE_ANGLES[11] + Constants.RADIAL_ANGLE_RANGE_DEG)) {
						zone = 11;
					}
					else {
						zone = 12;
					}
				}
			}
			//  ------------ normal ------------
			else {
				if (angle >= Constants.RADIAL_TOP3_MIN && angle < Constants.RADIAL_TOP3_MAX) {
					if (angle < (Constants.RADIAL_ZONE_ANGLES[1] + Constants.RADIAL_ANGLE_RANGE_DEG)) {
						zone = 1;
					}
					else if (angle < (Constants.RADIAL_ZONE_ANGLES[2] + Constants.RADIAL_ANGLE_RANGE_DEG)) {
						zone = 2;
					}
					else if (angle < (Constants.RADIAL_ZONE_ANGLES[3] + Constants.RADIAL_ANGLE_RANGE_DEG)) {
						zone = 3;
					}
					else {
						zone = 4;
					}
				}
				else if (angle >= Constants.RADIAL_MID3_MIN && angle < Constants.RADIAL_MID3_MAX) {
					if (angle < (Constants.RADIAL_ZONE_ANGLES[5] + Constants.RADIAL_ANGLE_RANGE_DEG)) {
						zone = 5;
					}
					else if (angle < (Constants.RADIAL_ZONE_ANGLES[6] + Constants.RADIAL_ANGLE_RANGE_DEG)) {
						zone = 6;
					}
					else if (angle < (Constants.RADIAL_ZONE_ANGLES[7] + Constants.RADIAL_ANGLE_RANGE_DEG)) {
						zone = 7;
					}
					else {
						zone = 8;
					}
				}
				else {
					if (angle < (Constants.RADIAL_ZONE_ANGLES[9] + Constants.RADIAL_ANGLE_RANGE_DEG)) {
						zone = 9;
					}
					else if (angle < (Constants.RADIAL_ZONE_ANGLES[10] + Constants.RADIAL_ANGLE_RANGE_DEG)) {
						zone = 10;
					}
					else if (angle < (Constants.RADIAL_ZONE_ANGLES[11] + Constants.RADIAL_ANGLE_RANGE_DEG)) {
						zone = 11;
					}
					else {
						zone = 12;
					}
				}
			}
		}
		else {
			// clear screen and prevent input
			if (radialMenu.activeSelf) {
				ScaleRadial(false);
			}

			// radialMenu.SetActive(false);
			buttonPrompts.SetActive(false);
			zone = 0;

			// make sure last selected icon is returned to correct size
			if (oldZone != 0 && zone != oldZone) {
				radVis.scaleIconOnDeselect(oldZone - 1);
			}

			// reset to main
			inputZeroTimeCounter += Time.deltaTime;
			if (inputZeroTimeCounter >= INPUT_ZERO_TIME) {
				if (spawningBlessingUnit) {
					spawningBlessingUnit = false;
					unitBlessingInUse = null;
				}

				rState = radialStates.none; // no input
				inputList.Clear();
			}

			// make sure arrows are small
			laneArrows[(int)Constants.radialCodes.top].transform.localScale = new Vector3(1f, 1f, 1f);
			laneArrows[(int)Constants.radialCodes.mid].transform.localScale = new Vector3(1f, 1f, 1f);
			laneArrows[(int)Constants.radialCodes.bot].transform.localScale = new Vector3(1f, 1f, 1f);
			laneArrows[(int)Constants.radialCodes.top + 3].transform.localScale = new Vector3(1f, 1f, 1f);
			laneArrows[(int)Constants.radialCodes.mid + 3].transform.localScale = new Vector3(1f, 1f, 1f);
			laneArrows[(int)Constants.radialCodes.bot + 3].transform.localScale = new Vector3(1f, 1f, 1f);
		}
	}

	#region radialMain
	private void updateMainVisual() {
		mainSummaryIcons[(int)Constants.radialCodes.top].gameObject.SetActive(true);
		mainSummaryIcons[(int)Constants.radialCodes.mid].gameObject.SetActive(true);
		mainSummaryIcons[(int)Constants.radialCodes.bot].gameObject.SetActive(true);

		radVis.viewButtonPrompts(true, false, false);

		radVis.ApplyLockIcon(baseZoneLocks);

		if (zone >= 1 && zone <= 4) {    // "top" sector
										 // display summary icons
			mainSummaryIcons[(int)Constants.radialCodes.top].gameObject.SetActive(false);
			mainSummaryIcons[(int)Constants.radialCodes.mid].gameObject.SetActive(true);
			mainSummaryIcons[(int)Constants.radialCodes.bot].gameObject.SetActive(true);
			blessingCountdownGroup.SetActive(false);

			// display sub-icons
			mainIconGroups[(int)Constants.radialCodes.top].SetActive(true);
			mainIconGroups[(int)Constants.radialCodes.top + 3].SetActive(true);
			mainIconGroups[(int)Constants.radialCodes.mid].SetActive(false);
			mainIconGroups[(int)Constants.radialCodes.mid + 3].SetActive(false);
			mainIconGroups[(int)Constants.radialCodes.bot].SetActive(false);
			mainIconGroups[(int)Constants.radialCodes.bot + 3].SetActive(false);

			// gray out unit icons if the player doesn't have enough gold to buy
			radVis.GreyOutUnitIcons(unitIcons, unitIconsMasked, uSpawner);
		}
		else if (zone >= 5 && zone <= 8) {   // "mid" sector
											 // display summary icons
			mainSummaryIcons[(int)Constants.radialCodes.top].gameObject.SetActive(true);
			mainSummaryIcons[(int)Constants.radialCodes.mid].gameObject.SetActive(false);
			mainSummaryIcons[(int)Constants.radialCodes.bot].gameObject.SetActive(true);
			blessingCountdownGroup.SetActive(true);

			// display sub-icons
			mainIconGroups[(int)Constants.radialCodes.top].SetActive(false);
			mainIconGroups[(int)Constants.radialCodes.top + 3].SetActive(false);
			mainIconGroups[(int)Constants.radialCodes.mid].SetActive(true);
			mainIconGroups[(int)Constants.radialCodes.mid + 3].SetActive(true);
			mainIconGroups[(int)Constants.radialCodes.bot].SetActive(false);
			mainIconGroups[(int)Constants.radialCodes.bot + 3].SetActive(false);

			// tell blessings how much favor player has for icon fill
			for (int i = 0; i < blessings.Length; i++) {
				blessingScripts[i].setPlayerFavor(favor);
			}
		}
		else if (zone >= 9 && zone <= 12) {  // "bot" sector
											 // display summary icons
			mainSummaryIcons[(int)Constants.radialCodes.top].gameObject.SetActive(true);
			mainSummaryIcons[(int)Constants.radialCodes.mid].gameObject.SetActive(true);
			mainSummaryIcons[(int)Constants.radialCodes.bot].gameObject.SetActive(false);
			blessingCountdownGroup.SetActive(false);

			// display sub-icons
			mainIconGroups[(int)Constants.radialCodes.top].SetActive(false);
			mainIconGroups[(int)Constants.radialCodes.top + 3].SetActive(false);
			mainIconGroups[(int)Constants.radialCodes.mid].SetActive(false);
			mainIconGroups[(int)Constants.radialCodes.mid + 3].SetActive(false);
			mainIconGroups[(int)Constants.radialCodes.bot].SetActive(true);
			mainIconGroups[(int)Constants.radialCodes.bot + 3].SetActive(true);

			// gray out tower icons if the player doesn't have enough gold to buy
			radVis.GreyOutTowerIcons(towerIcons, towerIconsMasked, towerGoldCosts);
		}

		if (zone != 0 && meter.CanUnlock()) {
			radVis.toggleUnlockButtonPrompt(baseZoneLocks[zone]);
		}
		else {
			radVis.toggleUnlockButtonPrompt(false);
		}

		radVis.toggleUpgradeButtonPrompt(meter.CanUpgrade());

		// Scaling icons when selected
		if (zone != oldZone && zone != 0) {
			// scale selected
			radVis.scaleIconOnSelect(zone - 1);

			// scale down previously selected thing
			if (oldZone != 0) {
				radVis.scaleIconOnDeselect(oldZone - 1);
			}
		}

		// try to only do this when the zone changes to minimize getComponent calls
		// also should try to find a way around the getComponent calls
		if (oldZone != zone) {
			displayInfoInRadialMain();
		}
	}

	private void updateMainFunctional() {
		if (rewiredPlayer.GetButtonDown(RewiredConsts.Action.Upgrade)) {
			if (meter.CanUpgrade()) {
				rState = radialStates.upgrades;
				primeUpgradeRadial();
				updateUpgradesVisual();
			}
		}
		else if (rewiredPlayer.GetButtonDown(RewiredConsts.Action.Unlock)) {
			if (zone != 0 && meter.CanUnlock() && baseZoneLocks[zone]) {
				meter.Unlock(zone);
				radVis.ApplyLockIcon(baseZoneLocks);

                if (SettingsManager.Instance.GetIsOnline()) {
                    SendUnlockSync(zone);
                }
			}
		}
		else if (rewiredPlayer.GetButtonDown(RewiredConsts.Action.Select)) {
			if (!baseZoneLocks[zone]) {
				if (zone == 1) {
					playRadialClickFeedback();
					inputList.Add(((int)Constants.unitType.spearman).ToString());
					rState = radialStates.chooseUnitLane;
					updateChooseLaneVisual();
				}
				else if (zone == 2) {
					playRadialClickFeedback();
					inputList.Add(((int)Constants.unitType.shieldbearer).ToString());
					rState = radialStates.chooseUnitLane;
					updateChooseLaneVisual();
				}
				else if (zone == 3) {
					playRadialClickFeedback();
					inputList.Add(((int)Constants.unitType.archer).ToString());
					rState = radialStates.chooseUnitLane;
					updateChooseLaneVisual();
				}
				else if (zone == 4) {
					playRadialClickFeedback();
					inputList.Add(((int)Constants.unitType.catapult).ToString());
					rState = radialStates.chooseUnitLane;
					updateChooseLaneVisual();
				}
				else if (zone == 5) {   // THE NUMBERS, MASON, WHAT DO THEY MEAN?
					blessingHelper((int)Constants.blessingType.ultimate);
				}
				else if (zone == 6) {
					blessingHelper((int)Constants.blessingType.special);
				}
				else if (zone == 7) {
					blessingHelper((int)Constants.blessingType.basic2);
				}
				else if (zone == 8) {
					blessingHelper((int)Constants.blessingType.basic1);
				}
				else if (zone >= 9) {
					int towerIndex = Constants.RADIAL_ZONE_COUNT - zone;

					if (getGold() >= towerGoldCosts[towerIndex]) {
						playRadialClickFeedback();
						reticule.SetActive(true);
						reticule.GetComponent<TowerSpawner>().holdTower(towers[towerIndex]);
						reticule.GetComponent<TowerSpawner>().PreviewInfluence();

						rState = radialStates.placeTower;

						reticuleMoveDelay = RETICULE_MOVE_DELAY;
					}
					else {
						purchaseErrorFeedback("gold");
					}
				}
			}
			else {
				purchaseErrorFeedback("locked");
			}
		}

		if (rState != radialStates.main) {
			// undisplay all "main" level icons and info
			mainSummaryIcons[(int)Constants.radialCodes.top].gameObject.SetActive(false);
			mainSummaryIcons[(int)Constants.radialCodes.mid].gameObject.SetActive(false);
			mainSummaryIcons[(int)Constants.radialCodes.bot].gameObject.SetActive(false);
			mainIconGroups[(int)Constants.radialCodes.top].SetActive(false);
			mainIconGroups[(int)Constants.radialCodes.top + 3].SetActive(false);
			mainIconGroups[(int)Constants.radialCodes.mid].SetActive(false);
			mainIconGroups[(int)Constants.radialCodes.mid + 3].SetActive(false);
			mainIconGroups[(int)Constants.radialCodes.bot].SetActive(false);
			mainIconGroups[(int)Constants.radialCodes.bot + 3].SetActive(false);
			radVis.toggleUnlockButtonPrompt(false);
			blessingCountdownGroup.SetActive(false);
		}

		if (rState != radialStates.main && rState != radialStates.chooseUnitLane && rState != radialStates.chooseBlessingLane) {
			radVis.toggleUpgradeButtonPrompt(false);
		}
	}

	public void UpdateLocks()
	{
		radVis.ApplyLockIcon(baseZoneLocks);
	}

	// Display name of selected thing, gold and favor cost, and tooltip
	private void displayInfoInRadialMain() {
		string[] infoText = { "", "", "", "" };

		if (zone == 1) {
			infoText = radVis.GetUnitInfoText(Constants.unitType.spearman, uSpawner);
		}
		else if (zone == 2) {
			infoText = radVis.GetUnitInfoText(Constants.unitType.shieldbearer, uSpawner);
		}
		else if (zone == 3) {
			infoText = radVis.GetUnitInfoText(Constants.unitType.archer, uSpawner);
		}
		else if (zone == 4) {
			infoText = radVis.GetUnitInfoText(Constants.unitType.catapult, uSpawner);
		}
		else if (zone == 5) {
			infoText = radVis.GetBlessingInfoText(Constants.blessingType.ultimate, blessingNames, blessings);
		}
		else if (zone == 6) {
			infoText = radVis.GetBlessingInfoText(Constants.blessingType.special, blessingNames, blessings);
		}
		else if (zone == 7) {
			infoText = radVis.GetBlessingInfoText(Constants.blessingType.basic2, blessingNames, blessings);
		}
		else if (zone == 8) {
			infoText = radVis.GetBlessingInfoText(Constants.blessingType.basic1, blessingNames, blessings);
		}
		else if (zone == 9) {
			infoText = radVis.GetTowerInfoText(Constants.towerType.stasisTower, towers);
		}
		else if (zone == 10) {
			infoText = radVis.GetTowerInfoText(Constants.towerType.obelisk, towers);
		}
		else if (zone == 11) {
			infoText = radVis.GetTowerInfoText(Constants.towerType.sunTower, towers);
		}
		else if (zone == 12) {
			infoText = radVis.GetTowerInfoText(Constants.towerType.archerTower, towers);
		}

		tooltipText.SetText(infoText[3]);

		if (rState != radialStates.upgrades) {
			if (zone >= 5 && zone <= 8) { // blessing zone, use favor
				singleCost.SetText(infoText[2]);
			}
			else {
				singleCost.SetText(infoText[1]);
			}
		}

		tooltipNameText.SetText(infoText[0]);
	}
	#endregion

	#region radialChooseLane
	private void updateChooseLaneVisual() {
		laneIcons.SetActive(true);
		laneIconsMasked.SetActive(true);
		blessingCountdownGroup.SetActive(false);

		radVis.viewButtonPrompts(true, true, false);
		radVis.RadialSelectorSnapRotation(ref selector, zone, rState);

		if (rewiredPlayerKey == PlayerIDs.player2 && !p2MirrorLanes) {
			radialRing.transform.localScale = new Vector3(-radialRing.transform.localScale.x, radialRing.transform.localScale.y, radialRing.transform.localScale.z);
			p2MirrorLanes = true;
		}

		radVis.ScaleLaneIcon(convertToLaneChoiceZone(zone, rewiredPlayerKey == PlayerIDs.player2) - 1,
			convertToLaneChoiceZone(oldZone, rewiredPlayerKey == PlayerIDs.player2) - 1, canSpawnInMid);
	}

	private void updateChooseUnitLaneFunctional() {
		if (rewiredPlayer.GetButtonDown(RewiredConsts.Action.Select) && zone != 0) {
			radVis.scaleIconOnDeselect(convertToLaneChoiceZone(zone, rewiredPlayerKey == PlayerIDs.player2) - 1);
			if (canSpawnInMid) {
				if (rewiredPlayerKey == PlayerIDs.player1) {
					if (zone >= 1 && zone <= 4) {
						playRadialClickFeedback();
						inputList.Add("top");
					}
					else if (zone >= 5 && zone <= 8 && canSpawnInMid) {
						playRadialClickFeedback();
						inputList.Add("mid");
					}
					else if (zone >= 9 && zone <= 12) {
						playRadialClickFeedback();
						inputList.Add("bot");
					}
				}
				else {  // stupid fucking player 2 unit lane selection bullshit
					if (zone >= 3 && zone <= 6) {
						playRadialClickFeedback();
						inputList.Add("top");
					}
					else if (zone >= 7 && zone <= 10) {
						playRadialClickFeedback();
						inputList.Add("bot");
					}
					else if ((zone >= 11 || zone >= 1) && canSpawnInMid) {
						playRadialClickFeedback();
						inputList.Add("mid");
					}
				}
			}
			else {
				if (zone <= 6) {
					playRadialClickFeedback();
					inputList.Add("top");
				}
				else {
					playRadialClickFeedback();
					inputList.Add("bot");
				}
			}

			if (inputList.Count > 1) {
				if (spawningBlessingUnit && favor >= unitBlessingInUse.cost) {
					unitBlessingInUse.Fire();
					spendFavor(unitBlessingInUse.cost);
					stats.recordBlessingUse(rewiredPlayerKey, unitBlessingIndex);

                    if (SettingsManager.Instance.GetIsOnline()) {
                        PO_UnitBlessing packet = new PO_UnitBlessing(unitBlessingInUse.bID, rewiredPlayerKey, inputList[1], inputList[0], unitBlessingInUse.allLanesSpawn);
						Debug.Log("sending unit blessing packet");
                        OnlineManager.Instance.SendPacket(packet);
                    }

                    uSpawner.addToSpawnQueue(System.Int32.Parse(inputList[0]), inputList[1]);
					inputList.RemoveAt(1);
					rState = radialStates.main;
				}
				else if (!spawningBlessingUnit) {
					uSpawner.addToSpawnQueue(System.Int32.Parse(inputList[0]), inputList[1]);
					inputList.RemoveAt(1);
				}
			}
		}
		else if (rewiredPlayer.GetButtonUp(RewiredConsts.Action.Select) && zone != 0) {
			radVis.scaleIconOnSelect(convertToLaneChoiceZone(zone, rewiredPlayerKey == PlayerIDs.player2) - 1);
		}
		else if (rewiredPlayer.GetButtonDown(RewiredConsts.Action.BackRadial)) {
			rState = radialStates.main;
		}
		else if (rewiredPlayer.GetButtonDown(RewiredConsts.Action.Upgrade)) {
			if (meter.CanUpgrade()) {
				rState = radialStates.upgrades;
				primeUpgradeRadial();
				updateUpgradesVisual();
			}
		}


		if (rState != radialStates.chooseUnitLane) {
			// clean up any to-be-spawned unit and close out of spawning blessing units
			inputList.Clear();
			if (spawningBlessingUnit) {
				spawningBlessingUnit = false;
				unitBlessingInUse = null;
				unitBlessingIndex = -1;
			}

			radVis.scaleDownLaneIcons();
			radVis.RadialSelectorSnapRotation(ref selector, zone, rState);

			// undisplay all "choose unit lane" level icons
			laneIcons.SetActive(false);
			laneIconsMasked.SetActive(false);

			// display whatever center text needs to be there
			tooltipUI.gameObject.SetActive(true);
			displayInfoInRadialMain();  // reset tooltip text

			if (rewiredPlayerKey == PlayerIDs.player2 && p2MirrorLanes) {
				radialRing.transform.localScale = new Vector3(-radialRing.transform.localScale.x, radialRing.transform.localScale.y, radialRing.transform.localScale.z);
				p2MirrorLanes = false;
			}
		}
	}

	private void updateChooseBlessingLaneFunctional() {
		if (rewiredPlayer.GetButtonDown(RewiredConsts.Action.Select) && zone != 0) {
			radVis.scaleIconOnDeselect(convertToLaneChoiceZone(zone, rewiredPlayerKey == PlayerIDs.player2) - 1);
			if (rewiredPlayerKey == PlayerIDs.player1) {
				if (zone >= 1 && zone <= 4) {
					laneBlessing.SetLane("top");
				}
				else if (zone >= 5 && zone <= 8 && canSpawnInMid) {
					laneBlessing.SetLane("mid");
				}
				else if (zone >= 9 && zone <= 12) {
					laneBlessing.SetLane("bot");
				}
			}
			else {  // stupid fucking player 2 unit lane selection bullshit
				if (zone >= 3 && zone <= 6) {
					laneBlessing.SetLane("top");
				}
				else if ((zone >= 11 || zone >= 1) && canSpawnInMid) {
					laneBlessing.SetLane("mid");
				}
				else if (zone >= 7 && zone <= 10) {
					laneBlessing.SetLane("bot");
				}
			}

			if (laneBlessing.CanFire(getFavor())) {
				favor -= laneBlessing.cost;
				stats.recordBlessingUse(rewiredPlayerKey, laneBlessingIndex);

				if (laneBlessing.duration > 0) {
					playRadialClickFeedback();
					blessingDurations.AddBlessingToQueue(laneBlessing.duration, laneBlessing.icon);
				}

				rState = radialStates.main;
			}
			else {
				Debug.Log("Sandstorm ded");
			}
		}
		else if (rewiredPlayer.GetButtonUp(RewiredConsts.Action.Select) && zone != 0) {
			radVis.scaleIconOnSelect(convertToLaneChoiceZone(zone, rewiredPlayerKey == PlayerIDs.player2) - 1);
		}
		else if (rewiredPlayer.GetButtonDown(RewiredConsts.Action.BackRadial)) {
			rState = radialStates.main;
		}
		else if (rewiredPlayer.GetButtonDown(RewiredConsts.Action.Upgrade) && meter.CanUpgrade()) {
			rState = radialStates.upgrades;
			primeUpgradeRadial();
			updateUpgradesVisual();
		}

		if (rState != radialStates.chooseBlessingLane) {
			// clean up lane blessing values
			laneBlessing = null;
			laneBlessingIndex = -1;

			// undisplay all "choose unit lane" level icons
			laneIcons.SetActive(false);
			laneIconsMasked.SetActive(false);

			radVis.scaleDownLaneIcons();
			radVis.RadialSelectorSnapRotation(ref selector, zone, rState);

			// display whatever center text needs to be there
			tooltipUI.gameObject.SetActive(true);
			displayInfoInRadialMain();  // reset tooltip text

			if (rewiredPlayerKey == PlayerIDs.player2 && p2MirrorLanes) {
				radialRing.transform.localScale = new Vector3(-radialRing.transform.localScale.x, radialRing.transform.localScale.y, radialRing.transform.localScale.z);
				p2MirrorLanes = false;
			}
		}
	}

	private int convertToLaneChoiceZone(int _zone, bool isPlayerTwo) {
		if (canSpawnInMid) {
			if (!isPlayerTwo) {
				if (_zone >= 1 && _zone <= 4) {
					return 16;
				}
				else if (_zone >= 5 && _zone <= 8) {
					return 17;
				}
				else {
					return 18;
				}
			}
			else {
				if (_zone >= 3 && _zone <= 6) {
					return 16;  // top
				}
				else if (_zone >= 7 && _zone <= 10) {
					return 18;  // bottom
				}
				else {
					return 17;  // mid
				}
			}
		}
		else {
			if (_zone > 0 && _zone <= 6) {
				return 16;
			}
			else {
				return 18;
			}
		}
	}

	#endregion

	#region radialPlaceTower
	private void updatePlaceTowerVisual() {
		radialMenu.SetActive(false);

		radVis.viewButtonPrompts(true, true, true);
	}

	private void updatePlaceTowerFunctional() {
#if UNITY_XBOXONE
		reticuleMoveCountdown();
#else
		if (rewiredPlayer.controllers.hasMouse) {
			reticule.GetComponent<CursorMovement>().MoveCursorWithMouse(true);
		}
		else {
			reticuleMoveCountdown();
		}
#endif

		if (rewiredPlayer.GetButtonDown(RewiredConsts.Action.Select)) {
			if (reticule.GetComponent<TowerSpawner>().trySpawn()) {
				rState = radialStates.main;

				InfluenceTileDictionary.UncolorTiles(rewiredPlayerKey);

				reticule.GetComponent<TowerSpawner>().ClearPreview();

				reticule.SetActive(false);
			}
		}
		else if (rewiredPlayer.GetButtonDown(RewiredConsts.Action.BackRadial)) {
			rState = radialStates.main;

			clearTowerAndUncolor();
			reticule.GetComponent<TowerSpawner>().ClearPreview();
			reticule.SetActive(false);

			radVis.viewButtonPrompts(true, false, false);
		}
		else if (rewiredPlayer.GetButtonDown(RewiredConsts.Action.RotateTowerLeft)) {
			reticule.GetComponent<TowerSpawner>().rotateTowerLeft();
		}
		else if (rewiredPlayer.GetButtonDown(RewiredConsts.Action.RotateTowerRight)) {
			reticule.GetComponent<TowerSpawner>().rotateTowerRight();
		}
	}

    public void ForcePlaceTower(Constants.towerType type, int entityID, string tileName, int rotation) {
        reticule.SetActive(true);
        reticule.GetComponent<TowerSpawner>().holdTower(towers[(int)type]);
        reticule.GetComponent<TowerSpawner>().PreviewInfluence();

        Debug.Log(rotation);

        // rotate tower to passed tower rotation
		if (rotation < 0) {
			for (int i = 0; i<Mathf.Abs(rotation) / 90; i++) {
				reticule.GetComponent<TowerSpawner>().rotateTowerLeft();
			}
		}
		else {
			for (int i = 0; i < Mathf.Abs(rotation) / 90; i++) {
				reticule.GetComponent<TowerSpawner>().rotateTowerRight();
			}
		}

        reticule.GetComponent<CursorMovement>().MoveToTemp(GameObject.Find(tileName));

        reticule.GetComponent<TowerSpawner>().trySpawn(entityID);
        reticule.GetComponent<TowerSpawner>().ClearPreview();
        InfluenceTileDictionary.UncolorTiles(rewiredPlayerKey);
        reticule.SetActive(false);
    }
	#endregion

	#region radialBlessingUse
	private void updateConfirmActivePowerVisual() {
		radialMenu.SetActive(false);

		radVis.viewButtonPrompts(true, true, false);
	}

	//Function to confirm active blessing
	void updateConfirmActivePowerFunctional() {
#if UNITY_XBOXONE
		reticuleMoveCountdown();
#else
		if (rewiredPlayer.controllers.hasMouse) {
			reticule.GetComponent<CursorMovement>().MoveCursorWithMouse(false);
		}
		else {
			reticuleMoveCountdown();
		}
#endif

		if (rewiredPlayer.GetButtonDown(RewiredConsts.Action.Select)) {
			if (activeBlessing.GetType().IsSubclassOf(typeof(ActiveBlessing_MultiTarget_I))) {  // multi target
				if (!multiTargetActiveBlessing) {
					multiTargetActiveBlessing = (ActiveBlessing_MultiTarget_I)activeBlessing;
				}

				multiTargetActiveBlessing.AddTarget(Instantiate(activeTargeter.gameObject, reticule.transform.position + new Vector3(0, .15f, 0), Quaternion.identity), reticule);

				if (multiTargetActiveBlessing.canFire(rewiredPlayerKey, reticule.transform.position, favor)) {
					spendFavor(multiTargetActiveBlessing.cost);
					stats.recordBlessingUse(rewiredPlayerKey, activeBlessingIndex);

					if (multiTargetActiveBlessing.duration > 0) {
						blessingDurations.AddBlessingToQueue(multiTargetActiveBlessing.duration, multiTargetActiveBlessing.icon);
					}

					// clean up reticule and stored data after fire
					reticule.SetActive(false);

					activeBlessing = null;
					multiTargetActiveBlessing = null;
					activeBlessingIndex = -1;
					Destroy(activeTargeter.gameObject);
					if (secondaryActiveTargeter) {
						Destroy(secondaryActiveTargeter.gameObject);
					}

					rState = radialStates.main;
				}
			}
			else {  // single target
				if (activeBlessing.canFire(rewiredPlayerKey, reticule.transform.position, favor)) {
					spendFavor(activeBlessing.cost);
					stats.recordBlessingUse(rewiredPlayerKey, activeBlessingIndex); // record use of (active) blessing for stats

					if (activeBlessing.duration > 0) {
						blessingDurations.AddBlessingToQueue(activeBlessing.duration, activeBlessing.icon);
					}

					reticule.SetActive(false);

					activeBlessing = null;
					activeBlessingIndex = -1;
					Destroy(activeTargeter.gameObject);
					if (secondaryActiveTargeter) {
						Destroy(secondaryActiveTargeter.gameObject);
					}

					rState = radialStates.main;
				}
				else {
					purchaseErrorFeedback("favor");
				}
			}
		}

		if (rewiredPlayer.GetButtonDown(RewiredConsts.Action.BackRadial)) { //Return to the power selector
			reticule.SetActive(false);

			radVis.viewButtonPrompts(true, false, false);

			if (multiTargetActiveBlessing) {
				multiTargetActiveBlessing.ResetTargets();
				multiTargetActiveBlessing = null;
			}

			activeBlessing = null;
			activeBlessingIndex = -1;

			Destroy(activeTargeter.gameObject);
			if (secondaryActiveTargeter) {
				Destroy(secondaryActiveTargeter.gameObject);
			}

			rState = radialStates.main;
		}
	}

	//Helper function to assign the blessing properly
	private void blessingHelper(int bIndex) {
		Blessing blessing = blessings[bIndex].GetComponent<Blessing>(); // retrieve proper blessing from array based on passed index

		// Assign an active blessing if an active blessing was chosen from the list
		if (blessing.GetType().IsSubclassOf(typeof(ActiveBlessing_I))) {
			activeBlessing = (ActiveBlessing_I)blessing;
			activeBlessingIndex = bIndex;
			if (getFavor() >= activeBlessing.cost && !activeBlessing.isOnCd) {
				playRadialClickFeedback();

				rState = radialStates.confirmActiveBlessing;

				reticule.SetActive(true);

				reticuleMoveDelay = RETICULE_MOVE_DELAY;

				// set up targeter
				if (rewiredPlayerKey == PlayerIDs.player1) {
					activeTargeter = Instantiate(activeBlessing.P1targeter, reticule.transform.position + new Vector3(0, .15f, 0), Quaternion.identity);

					// scale targeter by blessing range
					float scale = activeBlessing.GetComponent<ActiveBlessing_I>().radius;
					activeTargeter.transform.localScale *= scale;
					for (int i = 0; i < activeTargeter.transform.childCount; i++) {
						activeTargeter.transform.GetChild(i).transform.localScale *= scale;
					}

					if (activeBlessing.secondaryRange != 0) {
						secondaryActiveTargeter = Instantiate(activeBlessing.P1targeter, reticule.transform.position, Quaternion.identity);
						secondaryActiveTargeter.transform.localScale *= activeBlessing.GetComponent<ActiveBlessing_I>().secondaryRange;
						secondaryActiveTargeter.transform.SetParent(reticule.transform);
						secondaryActiveTargeter.transform.position += new Vector3(0f, -0.05f, 0f);  // make sure secondary renders below primary for visibility
					}
				}
				else {
					activeTargeter = Instantiate(activeBlessing.P2targeter, reticule.transform.position + new Vector3(0, .15f, 0), Quaternion.identity);

					// scale targeter by blessing range
					float scale = activeBlessing.GetComponent<ActiveBlessing_I>().radius;
					activeTargeter.transform.localScale *= scale;
					for (int i = 0; i < activeTargeter.transform.childCount; i++) {
						activeTargeter.transform.GetChild(i).transform.localScale *= scale;
					}

					if (activeBlessing.secondaryRange != 0) {
						secondaryActiveTargeter = Instantiate(activeBlessing.P2targeter, reticule.transform.position, Quaternion.identity);
						secondaryActiveTargeter.transform.localScale *= activeBlessing.GetComponent<ActiveBlessing_I>().secondaryRange;
						secondaryActiveTargeter.transform.SetParent(reticule.transform);
						secondaryActiveTargeter.transform.position += new Vector3(0f, -0.05f, 0f);  // make sure secondary renders below primary for visibility
					}
				}
				activeTargeter.transform.SetParent(reticule.transform);
			}
			else {
				purchaseErrorFeedback("favor");
			}
		}
		// Assign a support buff blessing if that is what was chosen
		else if (blessing.GetType().IsSubclassOf(typeof(BuffBlessing_I))) {
			//Cast the buff blessing so that we can call the fire function
			BuffBlessing_I buffBless = (BuffBlessing_I)blessing;
			if (buffBless.canFire(rewiredPlayerKey, getFavor())) {
				spendFavor(buffBless.cost);
				playRadialClickFeedback();

				if (buffBless.duration > 0) {
					blessingDurations.AddBlessingToQueue(buffBless.duration, buffBless.icon);
				}

				stats.recordBlessingUse(rewiredPlayerKey, bIndex);  // record use of (buff) blessing for stats
			}
			else {
				purchaseErrorFeedback("favor");
			}
		}
		// Global affect-everything blessings
		else if (blessing.GetType().IsSubclassOf(typeof(GlobalBlessing_I))) {
			//Cast the blessing so that we can call the fire function
			GlobalBlessing_I globalBless = (GlobalBlessing_I)blessing;
			globalBless.playerID = rewiredPlayerKey;
			if (globalBless.canFire(getFavor())) {
				spendFavor(globalBless.cost);
				playRadialClickFeedback();

				if (globalBless.duration > 0) {
					blessingDurations.AddBlessingToQueue(globalBless.duration, globalBless.icon);
				}

				stats.recordBlessingUse(rewiredPlayerKey, bIndex); // record use of (global) blessing for stats
			}
			else {
				purchaseErrorFeedback("favor");
			}
		}
		// Special unit spawning blessings
		else if (blessing.GetType().IsSubclassOf(typeof(UnitBlessing_I))) {
			UnitBlessing_I unitBless = (UnitBlessing_I)blessing;
			unitBlessingIndex = bIndex;
			if (unitBless.canFire(getFavor())) {
				playRadialClickFeedback();

				if (unitBless.allLanesSpawn) {
					unitBless.Fire();
					spendFavor(unitBless.cost);
				}
				else {
					inputList.Clear();
					inputList.Add(((int)unitBless.unitTypeToSpawn).ToString());

					spawningBlessingUnit = true;
					unitBlessingInUse = unitBless;
					rState = radialStates.chooseUnitLane;
				}
			}
		}
		// Blessings that affect units within a single lane
		else if (blessing.GetType().IsSubclassOf(typeof(LaneBlessing_I))) {
			LaneBlessing_I laneBless = (LaneBlessing_I)blessing;
			if (favor >= laneBless.cost && !laneBless.isOnCd) {
				playRadialClickFeedback();
				laneBless.playerID = rewiredPlayerKey;

				laneBlessing = laneBless;
				laneBlessingIndex = bIndex;
				rState = radialStates.chooseBlessingLane;
			}
			else {
				purchaseErrorFeedback("favor");
			}
		}
	}

    public void ForceBlessingUse(PO_BlessingCast packet) {
        switch (packet.blessingId) {
			case Blessing.blessingID.battleRage:
				PO_BattleRage p_battleRage = (PO_BattleRage)packet;
				Vector3 loc_battleRage = new Vector3(p_battleRage.location[0], p_battleRage.location[1], p_battleRage.location[2]);
				BattleRage battleRage = (BattleRage)System.Array.Find(blessingScripts, b => b.bID == packet.blessingId);

				if (battleRage.forceCanFire(rewiredPlayerKey, loc_battleRage, favor, p_battleRage.targets)) {
					blessingDurations.AddBlessingToQueue(battleRage.duration, battleRage.icon);
					spendFavor(battleRage.cost);
					stats.recordBlessingUse(rewiredPlayerKey, System.Array.FindIndex(blessingScripts, b => b.bID == packet.blessingId));

					QuipsManager.Instance.PlayBlessingUseQuip(Blessing.blessingID.battleRage, rewiredPlayerKey);
				}
				break;
			case Blessing.blessingID.betrayal:
                PO_Betrayal p_betrayal = (PO_Betrayal)packet;
                Vector3 loc_betrayal = new Vector3(p_betrayal.location[0], p_betrayal.location[1], p_betrayal.location[2]);
                Betrayal betrayal = (Betrayal)System.Array.Find(blessingScripts, b => b.bID == packet.blessingId);

                if (betrayal.forceCanFire(rewiredPlayerKey, loc_betrayal, favor, p_betrayal.targets)) {
                    blessingDurations.AddBlessingToQueue(betrayal.duration, betrayal.icon);
                    spendFavor(betrayal.cost);
                    stats.recordBlessingUse(rewiredPlayerKey, System.Array.FindIndex(blessingScripts, b => b.bID == packet.blessingId));

					QuipsManager.Instance.PlayBlessingUseQuip(Blessing.blessingID.betrayal, rewiredPlayerKey);
				}
                break;
            case Blessing.blessingID.cyclone:
                PO_Cyclone p_cyclone = (PO_Cyclone)packet;
                Vector3 loc_cyclone = new Vector3(p_cyclone.location[0], p_cyclone.location[1], p_cyclone.location[2]);
                Cyclone cyclone = (Cyclone)System.Array.Find(blessingScripts, b => b.bID == packet.blessingId);

                if (cyclone.canFire(rewiredPlayerKey, loc_cyclone, favor, false)) {
                    spendFavor(cyclone.cost);
                    stats.recordBlessingUse(rewiredPlayerKey, System.Array.FindIndex(blessingScripts, b => b.bID == packet.blessingId));
                }
                break;
            case Blessing.blessingID.decay:
                PO_Decay p_decay = (PO_Decay)packet;
                Vector3 loc_decay = new Vector3(p_decay.location[0], p_decay.location[1], p_decay.location[2]);
                Decay decay = (Decay)System.Array.Find(blessingScripts, b => b.bID == packet.blessingId);

                if (decay.canFire(rewiredPlayerKey, loc_decay, favor, false)) {
                    blessingDurations.AddBlessingToQueue(decay.duration, decay.icon);
                    spendFavor(decay.cost);
                    stats.recordBlessingUse(rewiredPlayerKey, System.Array.FindIndex(blessingScripts, b => b.bID == packet.blessingId));

					QuipsManager.Instance.PlayBlessingUseQuip(Blessing.blessingID.decay, rewiredPlayerKey);
				}
                break;
            case Blessing.blessingID.earthquake:
                Earthquake earthquake = (Earthquake)System.Array.Find(blessingScripts, b => b.bID == packet.blessingId);

                if (earthquake.canFire(favor, false)) {
                    blessingDurations.AddBlessingToQueue(earthquake.duration, earthquake.icon);
                    spendFavor(earthquake.cost);
                    stats.recordBlessingUse(rewiredPlayerKey, System.Array.FindIndex(blessingScripts, b => b.bID == packet.blessingId));
                }
                break;
            case Blessing.blessingID.embalming:
                PO_UnitBlessing p_embalming = (PO_UnitBlessing)packet;
                Embalming embalming = (Embalming)System.Array.Find(blessingScripts, b => b.bID == packet.blessingId);
                embalming.Fire();
                spendFavor(embalming.cost);
                stats.recordBlessingUse(rewiredPlayerKey, System.Array.FindIndex(blessingScripts, b => b.bID == packet.blessingId));

				QuipsManager.Instance.PlayBlessingUseQuip(Blessing.blessingID.embalming, rewiredPlayerKey);
				break;
            case Blessing.blessingID.empower:
                PO_Empower p_empower = (PO_Empower)packet;
                Vector3 loc_empower = new Vector3(p_empower.location[0], p_empower.location[1], p_empower.location[2]);
                Empower empower = (Empower)System.Array.Find(blessingScripts, b => b.bID == packet.blessingId);

                if (empower.canFire(rewiredPlayerKey, loc_empower, favor, false)) {
                    blessingDurations.AddBlessingToQueue(empower.duration, empower.icon);
                    spendFavor(empower.cost);
                    stats.recordBlessingUse(rewiredPlayerKey, System.Array.FindIndex(blessingScripts, b => b.bID == packet.blessingId));

					QuipsManager.Instance.PlayBlessingUseQuip(Blessing.blessingID.empower, rewiredPlayerKey);
				}
                break;
            case Blessing.blessingID.grasp:
                PO_Grasp p_grasp = (PO_Grasp)packet;
                Vector3 loc_grasp = new Vector3(p_grasp.location[0], p_grasp.location[1], p_grasp.location[2]);
                Grasp grasp = (Grasp)System.Array.Find(blessingScripts, b => b.bID == packet.blessingId);

                if (grasp.canFire(rewiredPlayerKey, loc_grasp, favor, false)) {
                    blessingDurations.AddBlessingToQueue(grasp.duration, grasp.icon);
                    spendFavor(grasp.cost);
                    stats.recordBlessingUse(rewiredPlayerKey, System.Array.FindIndex(blessingScripts, b => b.bID == packet.blessingId));

					QuipsManager.Instance.PlayBlessingUseQuip(Blessing.blessingID.grasp, rewiredPlayerKey);
				}
                break;
			case Blessing.blessingID.haste:
				Haste haste = (Haste)System.Array.Find(blessingScripts, b => b.bID == packet.blessingId);

				if (haste.canFire(rewiredPlayerKey, favor, false)) {
					blessingDurations.AddBlessingToQueue(haste.duration, haste.icon);
					spendFavor(haste.cost);
					stats.recordBlessingUse(rewiredPlayerKey, System.Array.FindIndex(blessingScripts, b => b.bID == packet.blessingId));
				}
				break;
			case Blessing.blessingID.ignite:
                PO_Ignite p_ignite = (PO_Ignite)packet;
                Vector3 loc_ignite = new Vector3(p_ignite.location[0], p_ignite.location[1], p_ignite.location[2]);
                Ignite ignite = (Ignite)System.Array.Find(blessingScripts, b => b.bID == packet.blessingId);

                if (ignite.canFire(rewiredPlayerKey, loc_ignite, favor, false)) {
                    spendFavor(ignite.cost);
                    stats.recordBlessingUse(rewiredPlayerKey, System.Array.FindIndex(blessingScripts, b => b.bID == packet.blessingId));
                }
                break;
            case Blessing.blessingID.immunity:
                MysticalImmunity immunity = (MysticalImmunity)System.Array.Find(blessingScripts, b => b.bID == packet.blessingId);

                if (immunity.canFire(rewiredPlayerKey, favor, false)) {
                    blessingDurations.AddBlessingToQueue(immunity.duration, immunity.icon);
                    spendFavor(immunity.cost);
                    stats.recordBlessingUse(rewiredPlayerKey, System.Array.FindIndex(blessingScripts, b => b.bID == packet.blessingId));

					QuipsManager.Instance.PlayBlessingUseQuip(Blessing.blessingID.immunity, rewiredPlayerKey);
				}
                break;
            case Blessing.blessingID.recovery:
                Recovery recovery = (Recovery)System.Array.Find(blessingScripts, b => b.bID == packet.blessingId);

                if (recovery.canFire(rewiredPlayerKey, favor, false)) {
                    blessingDurations.AddBlessingToQueue(recovery.duration, recovery.icon);
                    spendFavor(recovery.cost);
                    stats.recordBlessingUse(rewiredPlayerKey, System.Array.FindIndex(blessingScripts, b => b.bID == packet.blessingId));
                }
                break;
            case Blessing.blessingID.sandstorm:
                PO_Sandstorm p_sandstorm = (PO_Sandstorm)packet;
                Sandstorm sandstorm = (Sandstorm)System.Array.Find(blessingScripts, b => b.bID == packet.blessingId);
                sandstorm.SetLane(p_sandstorm.lane);
                sandstorm.playerID = rewiredPlayerKey;

                if (sandstorm.CanFire(favor, false)) {
                    blessingDurations.AddBlessingToQueue(sandstorm.duration, sandstorm.icon);
                    spendFavor(sandstorm.cost);
                    stats.recordBlessingUse(rewiredPlayerKey, System.Array.FindIndex(blessingScripts, b => b.bID == packet.blessingId));

					QuipsManager.Instance.PlayBlessingUseQuip(Blessing.blessingID.sandstorm, rewiredPlayerKey);
				}
                break;
			case Blessing.blessingID.huntressSpawn:
				PO_UnitBlessing p_huntress = (PO_UnitBlessing)packet;
				HuntressSpawn huntress = (HuntressSpawn)System.Array.Find(blessingScripts, b => b.bID == packet.blessingId);
				huntress.Fire();
				uSpawner.addToSpawnQueue(int.Parse(p_huntress.unitIndex), p_huntress.lane);
				spendFavor(huntress.cost);
				stats.recordBlessingUse(rewiredPlayerKey, System.Array.FindIndex(blessingScripts, b => b.bID == packet.blessingId));

				QuipsManager.Instance.PlayBlessingUseQuip(Blessing.blessingID.huntressSpawn, rewiredPlayerKey);
				break;
			case Blessing.blessingID.siphon:
				PO_Siphon p_siphon = (PO_Siphon)packet;
				Vector3 loc_siphon = new Vector3(p_siphon.location[0], p_siphon.location[1], p_siphon.location[2]);
				Siphon siphon = (Siphon)System.Array.Find(blessingScripts, b => b.bID == packet.blessingId);

				if (siphon.forceCanFire(rewiredPlayerKey, loc_siphon, favor, p_siphon.targets)) {
					blessingDurations.AddBlessingToQueue(siphon.duration, siphon.icon);
					spendFavor(siphon.cost);
					stats.recordBlessingUse(rewiredPlayerKey, System.Array.FindIndex(blessingScripts, b => b.bID == packet.blessingId));
				}
				break;
			case Blessing.blessingID.solarFlare:
                PO_SolarFlare p_solar = (PO_SolarFlare)packet;
                Vector3 loc_solar1 = new Vector3(p_solar.location1[0], p_solar.location1[1], p_solar.location1[2]);
                Vector3 loc_solar2 = new Vector3(p_solar.location2[0], p_solar.location2[1], p_solar.location2[2]);
                SolarFlare solar = (SolarFlare)System.Array.Find(blessingScripts, b => b.bID == packet.blessingId);
                solar.AddTarget(loc_solar1);
                solar.AddTarget(loc_solar2);

                if (solar.canFire(rewiredPlayerKey, loc_solar1, favor, false)) {
                    blessingDurations.AddBlessingToQueue(solar.duration, solar.icon);
                    spendFavor(solar.cost);
                    stats.recordBlessingUse(rewiredPlayerKey, System.Array.FindIndex(blessingScripts, b => b.bID == packet.blessingId));

					QuipsManager.Instance.PlayBlessingUseQuip(Blessing.blessingID.solarFlare, rewiredPlayerKey);
				}
                break;
            default:
                break;
        }
    }
	#endregion

	#region radialUpgrades
	private void updateUpgradesVisual() {
		upgradeIcons.SetActive(true);
		upgradeIconsMasked.SetActive(true);
		blessingCountdownGroup.SetActive(false);

		radVis.viewButtonPrompts(true, true, false);

		// for the purposes of proper scaling of icons on hover
		// "extend"ing the zones from the original 12
		int upgradeZone = convertToUpgradeZone(zone);
		int oldUpgradeZone = convertToUpgradeZone(oldZone);

		// limit getComponent calls
		BaseExpansion e;

		if (upgradeZone == 13) {
			e = expansions[(int)BaseExpansion.ExpansionTypes.temple].GetComponent<BaseExpansion>();

			tooltipText.SetText(Lang.ExpansionTooltips[BaseExpansion.ExpansionTypes.temple][SettingsManager.Instance.language]);
			tooltipNameText.SetText(Lang.ExpansionNames[BaseExpansion.ExpansionTypes.temple][SettingsManager.Instance.language]);
		}
		else if (upgradeZone == 14) {
			e = expansions[(int)BaseExpansion.ExpansionTypes.barracks].GetComponent<BaseExpansion>();

			tooltipText.SetText(Lang.ExpansionTooltips[BaseExpansion.ExpansionTypes.barracks][SettingsManager.Instance.language]);
			tooltipNameText.SetText(Lang.ExpansionNames[BaseExpansion.ExpansionTypes.barracks][SettingsManager.Instance.language]);
		}
		else if (upgradeZone == 15) {
			e = expansions[(int)BaseExpansion.ExpansionTypes.mine].GetComponent<BaseExpansion>();

			tooltipText.SetText(Lang.ExpansionTooltips[BaseExpansion.ExpansionTypes.mine][SettingsManager.Instance.language]);
			tooltipNameText.SetText(Lang.ExpansionNames[BaseExpansion.ExpansionTypes.mine][SettingsManager.Instance.language]);
		}

		// Scaling icons when selected
		if (upgradeZone != oldUpgradeZone && zone != 0) {
			// scale selected
			radVis.scaleIconOnSelect(upgradeZone - 1);

			// scale down previously selected thing
			if (oldUpgradeZone != 0) {
				radVis.scaleIconOnDeselect(oldUpgradeZone - 1);
			}
		}
	}

	private void updateUpgradesFunctional() {
		// when menu fills up we can clean this up a bit
		if (rewiredPlayer.GetButtonDown(RewiredConsts.Action.Select)) {
			BaseExpansion e;

			if (zone >= 1 && zone <= 4) {
				e = expansions[(int)Constants.expansionType.temple].GetComponent<BaseExpansion>();
				if (expansionCount < 2) {
					eSpawner.SpawnExpansion(expansions[(int)Constants.expansionType.temple], expansionCount);
					stats.recordExpansionSpawned(rewiredPlayerKey, Constants.expansionType.temple);

					GetComponentInChildren<KeepManager>().RemoveFlagsOnExpansionSpawn(expansionCount);

					playRadialClickFeedback();
					meter.Upgrade();
					sound_wallDrops();

					if (SettingsManager.Instance.GetIsOnline()) {
                        SendUpgradeSync(Constants.expansionType.temple);
                    }

					upgradesToPriorState();
				}
			}
			else if (zone >= 5 && zone <= 8) {
				e = expansions[(int)Constants.expansionType.barracks].GetComponent<BaseExpansion>();
				if (expansionCount < 2) {
					eSpawner.SpawnExpansion(expansions[(int)Constants.expansionType.barracks], expansionCount);
					stats.recordExpansionSpawned(rewiredPlayerKey, Constants.expansionType.barracks);

					GetComponentInChildren<KeepManager>().RemoveFlagsOnExpansionSpawn(expansionCount);

					playRadialClickFeedback();
					meter.Upgrade();
					sound_wallDrops();

					if (SettingsManager.Instance.GetIsOnline()) {
                        SendUpgradeSync(Constants.expansionType.barracks);
                    }

                    upgradesToPriorState();
				}
			}
			else if (zone >= 9) {
				e = expansions[(int)Constants.expansionType.mine].GetComponent<BaseExpansion>();
				if (expansionCount < 2) {
					eSpawner.SpawnExpansion(expansions[(int)Constants.expansionType.mine], expansionCount);
					stats.recordExpansionSpawned(rewiredPlayerKey, Constants.expansionType.mine);

					GetComponentInChildren<KeepManager>().RemoveFlagsOnExpansionSpawn(expansionCount);

					playRadialClickFeedback();
					meter.Upgrade();
					sound_wallDrops();

					if (SettingsManager.Instance.GetIsOnline()) {
                        SendUpgradeSync(Constants.expansionType.mine);
                    }

                    upgradesToPriorState();
				}
			}
		}
		else if (rewiredPlayer.GetButtonDown(RewiredConsts.Action.BackRadial)) {
			upgradesToPriorState();
		}

		if (rState != radialStates.upgrades) {
			upgradeIcons.SetActive(false);
			upgradeIconsMasked.SetActive(false);
		}
	}

	// converts global zone number into one compatible with upgrade menu calculations
	private int convertToUpgradeZone(int _zone) {
		if (_zone >= 1 && _zone <= 4) {
			return 13;
		}
		else if (_zone >= 5 && _zone <= 8) {
			return 14;
		}
		else {
			return 15;
		}
	}

	// anything that needs to be done once and no more when you go to upgrades menu
	private void primeUpgradeRadial() {
		// for the purposes of proper scaling of icons on hover
		// "extend"ing the zones from the original 12
		int upgradeZone = convertToUpgradeZone(zone);

#if !UNITY_XBOXONE
		if (rewiredPlayer.controllers.hasMouse && !rewiredPlayer.GetButton(RewiredConsts.Action.OpenRadial)) {
			radialMenu.transform.localPosition = initialRadialLocalPosition;
			radialMenu.transform.position = initialRadialPosition;
			playerInput.SetMouseDefaultPosition(radialMenu.transform.position);
		}
#endif

		// pre-emptively scale selected icon
		if (zone != 0) {
			radVis.scaleIconOnSelect(upgradeZone - 1);
		}
	}

	private void upgradesToPriorState() {
		if (zone != 0) {    // only go to main if there is input on the LS
			rState = radialStates.main;

			// this is supposed to prevent holdover of upgrades info/tooltip text
			displayInfoInRadialMain();
		}
	}
	#endregion

	public void clearTowerAndUncolor() {
		reticule.GetComponent<TowerSpawner>().clearTower();

		//Uncolor the tiles
		InfluenceTileDictionary.UncolorTiles(rewiredPlayerKey);
	}

	private void reticuleMoveCountdown() {
		if (reticuleMoveDelay > 0) {
			canMoveReticule = false;
			reticuleMoveDelay -= Time.deltaTime;
		}
		else {
			canMoveReticule = true;
		}
	}

	#region Effect/Feedback Functions
	public override void purchaseErrorFeedback(string errorType) {
		SoundManager.Instance.sound_UIDeny();

		GameManager.Instance.CostFeedback(rewiredPlayerKey, errorType); //call to change cost text as feedback
		radVis.ErrorBackgroundFlash();
	}

	public void playRadialHoverFeedback() {
		s.sound_radialHover();
	}

	public void playRadialClickFeedback() {
		s.sound_radialClick();
	}

	public bool shouldPlayRadialClickFeedback() {
		if (zone == 0) {
			return false;
		}
		else if (rState == radialStates.main) {
			return true;
		}
		else if ((rState == radialStates.chooseUnitLane || rState == radialStates.chooseBlessingLane) && !canSpawnInMid) {
			if (oldZone >= 1 && oldZone <= 6 && zone > 6) {
				return true;
			}
			else if (oldZone > 6 && zone >= 1 && zone <= 6) {
				return true;
			}
			else return false;
		}
		else if (rState == radialStates.chooseUnitLane || rState == radialStates.chooseBlessingLane || rState == radialStates.upgrades) {
			if (rewiredPlayerKey == PlayerIDs.player1 || rState == radialStates.upgrades) {
				if (oldZone <= 4 && zone > 4) {
					return true;
				}
				else if (oldZone >= 5 && oldZone <= 8 && (zone < 5 || zone > 8)) {
					return true;
				}
				else if (oldZone >= 9 && zone < 9) {
					return true;
				}
				else return false;
			}
			else {
				if (oldZone >= 3 && oldZone <= 6 && (zone > 6 || zone < 3)) {
					return true;
				}
				else if ((oldZone <= 2 || oldZone >= 11) && (zone > 2 || zone < 11)) {
					return true;
				}
				else if (oldZone >= 7 && oldZone <= 10 && (zone < 7 || zone > 10)) {
					return true;
				}
				else return false;
			}
		}
		else return false;
	}

	public override void ControllerVibration(int motorIndex, float fullMotorSpeed, float duration) {
#if UNITY_XBOXONE
		gamepadExtension.SetVibration(XboxOneGamepadMotorType.LeftMotor, fullMotorSpeed, duration);
		gamepadExtension.SetVibration(XboxOneGamepadMotorType.RightMotor, fullMotorSpeed, duration);
#else
        if (rewiredPlayer != null) {
            rewiredPlayer.SetVibration(motorIndex, fullMotorSpeed, duration);
        }
#endif
	}
	
	public void SpawnPokeEffect() {
		Ray cameraRay = mainCam.ScreenPointToRay(new Vector3(Input.mousePosition.x, Input.mousePosition.y, 0));
		RaycastHit[] collisions = Physics.RaycastAll(cameraRay, Mathf.Infinity, pokeEffectMask);

		if (collisions.Length > 0) {
			Instantiate(pokeFX, collisions[0].point, Quaternion.LookRotation(collisions[0].normal));
		}
	}
	#endregion

	#region Getters and Setters
	public radialStates GetState() {
		return rState;
	}

	public override bool UnlockedAll() {
		return !baseZoneLocks.ContainsValue(true);
	}
	#endregion

	#region Radial and Mouse Positional Stuff
	IEnumerator LateSetMouseDefault(float waitTime) {
		yield return new WaitForSecondsRealtime(waitTime);

		playerInput.SetMouseDefaultPosition(RectTransformUtility.WorldToScreenPoint(null, radialMenu.transform.position));
		initialRadialPosition = radialMenu.transform.position;
		initialRadialLocalPosition = radialMenu.transform.localPosition;

		/*  FIX FOR UNKNOWN ISSUE with Setting Mouse Default Position
         *  - on some machines, finding radialMenu position inside Start() will give the wrong position and will put default pos in wrong spot
         *  - works correctly if it position is found after start
         *  - if all start code is in an Awake(), the default position is correct
         *  - doesn't seem connected to opening camera movement as camera can be anywhere when setting default and it works correctly
         *  - setting is correct when radial is currently deactivated
        */
		locationSet = true;
	}

	private void MoveRadialToMouse() {
		Vector2 mousePos;
		RectTransformUtility.ScreenPointToLocalPointInRectangle(placementRect, Input.mousePosition, null, out mousePos);
		mousePos = new Vector2(Mathf.Clamp(mousePos.x, -placementRect.rect.width / 2 * .7f, placementRect.rect.width / 2 * .7f),
			Mathf.Clamp(mousePos.y, -placementRect.rect.height / 2 * .5f, placementRect.rect.height / 2 * .5f));
		radialMenu.transform.localPosition = mousePos;
		playerInput.SetMouseDefaultPosition(radialMenu.transform.position);
	}

	private void ScaleRadial(bool grow) {
		StartCoroutine(ScaleRadialWorker(grow));
	}

	IEnumerator ScaleRadialWorker(bool grow) {
		if (grow) {
			radialMenu.transform.localScale = Vector3.zero;
		}
		else {
			radialMenu.transform.localScale = originalRadialScale;
		}

		float elapsedTime = 0;

		while(elapsedTime < radialScaleTime) {
			if (grow) {
				radialMenu.transform.localScale = Vector3.Lerp(Vector3.zero, originalRadialScale, Mathf.SmoothStep(0.0f, 1.0f, elapsedTime / radialScaleTime));
			}
			else {
				radialMenu.transform.localScale = Vector3.Lerp(originalRadialScale, Vector3.zero, Mathf.SmoothStep(0.0f, 1.0f, elapsedTime / radialScaleTime));
			}

			elapsedTime += Time.deltaTime;
			yield return null;
		}

		if (grow) {
			radialMenu.transform.localScale = originalRadialScale;
		}
		else {
			radialMenu.transform.localScale = Vector3.zero;
			radialMenu.SetActive(false);
		}
	}
    #endregion

    #region Online Interface Functions
    private void SendUnlockSync(int unlockIndex) {
        PO_PlayerUnlock packet = new PO_PlayerUnlock(rewiredPlayerKey, unlockIndex);

        OnlineManager.Instance.SendPacket(packet);
    }

    public void SyncUnlock(int unlockIndex) {
        meter.Unlock(unlockIndex);
    }

    private void SendUpgradeSync(Constants.expansionType expansion) {
        PO_PlayerUpgrade packet = new PO_PlayerUpgrade(rewiredPlayerKey, expansion);

        OnlineManager.Instance.SendPacket(packet);
    }

    public void SyncUpgrade(Constants.expansionType expansion) {
        if (expansionCount < 2) {
            eSpawner.SpawnExpansion(expansions[(int)expansion], expansionCount);
            stats.recordExpansionSpawned(rewiredPlayerKey, expansion);

            meter.Upgrade();
        }
    }

    public void SyncStats(StatRecording _stats) {
        StatRecording current = stats.GetCurrentRecording();
        current.p1FavorEarned = _stats.p1FavorEarned;
        current.p1GoldEarned = _stats.p1GoldEarned;
        current.p1TowerGold = _stats.p1TowerGold;
        current.p1TowersSpawned = _stats.p1TowersSpawned;
        current.p1TowerTypesSpawned = _stats.p1TowerTypesSpawned;
        current.p1UnitGold = _stats.p1UnitGold;
        current.p1UnitsSpawned = _stats.p1UnitsSpawned;
        current.p1UnitTypesSpawned = _stats.p1UnitTypesSpawned;
        current.p1UnitTypesSpawned = _stats.p1UnitTypesSpawned;

        current.p2FavorEarned = _stats.p2FavorEarned;
        current.p2GoldEarned = _stats.p2GoldEarned;
        current.p2TowerGold = _stats.p2TowerGold;
        current.p2TowersSpawned = _stats.p2TowersSpawned;
        current.p2TowerTypesSpawned = _stats.p2TowerTypesSpawned;
        current.p2UnitGold = _stats.p2UnitGold;
        current.p2UnitsSpawned = _stats.p2UnitsSpawned;
        current.p2UnitTypesSpawned = _stats.p2UnitTypesSpawned;
        current.p2UnitTypesSpawned = _stats.p2UnitTypesSpawned;

        current.gameLength = _stats.gameLength;
        current.winner = _stats.winner;

        stats.saveRecording();
    }
	#endregion
}