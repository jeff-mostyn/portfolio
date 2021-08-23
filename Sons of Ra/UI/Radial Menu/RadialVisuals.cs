using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

public class RadialVisuals : MonoBehaviour {
	private const int SCALING_ICON_COUNT = 18;
	private const int TOP = 15;
	private const int MID = 16;
	private const int BOT = 17;
	private const int LAST_TOWER_ZONE = 12;
	private const int FIRST_UNIT_ZONE = 1;
	// ------------- nonpublic variables ----------------
	private GameManager g;
	private Human_PlayerController pController;

	// button icons
	[Header("Controller Button Icons")]
	[SerializeField] private GameObject selectIcon;
	[SerializeField] private GameObject backIcon;
	[SerializeField] private GameObject rotateIcon;
	[SerializeField] private GameObject unlockIcon;
	[SerializeField] private GameObject upgradeIcon;

	// radial icons
	[Header("Radial Menu")]
	[SerializeField] private List<iconScaler> radialIconScalers = new List<iconScaler>();
	public Color UnmaskedColor, UnmaskedGrey, MaskedWhite, MaskedGray;
	[SerializeField] private Image errorBackgroundFade;
	[SerializeField] private Image selectorImage;
	[SerializeField] private Sprite selectorSprite12th, selectorSprite3rd, selectorSpriteHalf;
	[SerializeField] private iconScaler topArrow2lane, botArrow2lane, topArrow2laneMasked, botArrow2laneMasked;

	private float playerGold, playerFavor;
	private float upgradeGoldCost, upgradeFavorCost;
	private int unlocksRemaining = 5;

	//UI Feedback Variables
	public float uiErrorFadeTime;
	private float colorTimer = 0f;

	private void Start() {
		g = GameManager.Instance;
	}

	public void SetUp(Human_PlayerController p) {
		pController = p;
		if (!pController.getCanSpawnInMid()) {
			radialIconScalers[TOP] = topArrow2lane;
			radialIconScalers[TOP + SCALING_ICON_COUNT] = topArrow2laneMasked;
			radialIconScalers[BOT] = botArrow2lane;
			radialIconScalers[BOT + SCALING_ICON_COUNT] = botArrow2laneMasked;
		}
	}

	public void viewButtonPrompts(bool selectOn, bool backOn, bool rotateOn) {
		selectIcon.SetActive(selectOn);
		backIcon.SetActive(backOn);
		rotateIcon.SetActive(rotateOn);
	}

	public void toggleUnlockButtonPrompt(bool on) {
		unlockIcon.SetActive(on);
	}

	public void toggleUpgradeButtonPrompt(bool on) {
		upgradeIcon.SetActive(on);
	}

	public void RadialSelectorSnapRotation(ref GameObject selector, int zone, Human_PlayerController.radialStates state) {
		if (state == Human_PlayerController.radialStates.main) {
			selectorImage.sprite = selectorSprite12th;
			selector.transform.eulerAngles = new Vector3(selector.transform.rotation.x, selector.transform.rotation.y, -Constants.RADIAL_ZONE_ANGLES[zone]);
		}
		else if (!pController.getCanSpawnInMid() && (state == Human_PlayerController.radialStates.chooseBlessingLane || state == Human_PlayerController.radialStates.chooseUnitLane)) {
			selectorImage.sprite = selectorSpriteHalf;
			if (pController.rewiredPlayerKey == PlayerIDs.player1) {
				if (zone <= 6) {
					selector.transform.eulerAngles = new Vector3(selector.transform.rotation.x, selector.transform.rotation.y, 90f);
				}
				else {
					selector.transform.eulerAngles = new Vector3(selector.transform.rotation.x, selector.transform.rotation.y, -90f);
				}
			}
			else {
				if (zone <= 6) {
					selector.transform.eulerAngles = new Vector3(selector.transform.rotation.x, selector.transform.rotation.y, 90f);
				}
				else {
					selector.transform.eulerAngles = new Vector3(selector.transform.rotation.x, selector.transform.rotation.y, -90f);
				}
			}
		}
		else {
			selectorImage.sprite = selectorSprite3rd;
			if (pController.rewiredPlayerKey == PlayerIDs.player1 || state == Human_PlayerController.radialStates.upgrades) {
				if (zone <= 4) {
					selector.transform.eulerAngles = new Vector3(selector.transform.rotation.x, selector.transform.rotation.y,
						Constants.RADIAL_P1_TOP);
				}
				else if (zone <= 8) {
					selector.transform.eulerAngles = new Vector3(selector.transform.rotation.x, selector.transform.rotation.y,
						Constants.RADIAL_P1_MID);
				}
				else {
					selector.transform.eulerAngles = new Vector3(selector.transform.rotation.x, selector.transform.rotation.y,
						Constants.RADIAL_P1_BOT);
				}
			}
			else {	// player 2 lane selection
				if (zone >= 3 && zone <= 6) {
					selector.transform.eulerAngles = new Vector3(selector.transform.rotation.x, selector.transform.rotation.y,
						Constants.RADIAL_P2_TOP);
				}
				else if (zone >= 7 && zone <= 10) {
					selector.transform.eulerAngles = new Vector3(selector.transform.rotation.x, selector.transform.rotation.y,
						Constants.RADIAL_P2_BOT);
				}
				else {
					selector.transform.eulerAngles = new Vector3(selector.transform.rotation.x, selector.transform.rotation.y,
						Constants.RADIAL_P2_MID);
				}
			}
		}
	}

	public void RadialPointerRotation(ref GameObject pointer, float angle) {
		pointer.transform.eulerAngles = new Vector3(pointer.transform.rotation.x, pointer.transform.rotation.y, -angle);
	}

	#region Info Text
	// 0 = name
	// 1 = gold cost
	// 2 = favor cost
	// 3 = tooltip text
	public string[] GetUnitInfoText(Constants.unitType unitType, UnitSpawner uSpanwer) {
		string[] text = new string[4];
		text[0] = Lang.unitNames[unitType][SettingsManager.Instance.language];
		text[1] = uSpanwer.getUnitCost(uSpanwer.units[(int)unitType].GetComponent<UnitAI>().type).ToString();
		text[2] = "0";
		text[3] = Lang.unitTooltips[unitType][SettingsManager.Instance.language];

		return text;
	}

	public string[] GetBlessingInfoText(Constants.blessingType blessingType, string[] blessingNames, GameObject[] blessings) {
		string[] text = new string[4];
		text[0] = blessingNames[(int)blessingType];
		text[1] = "0";
		text[2] = blessings[(int)blessingType].GetComponent<Blessing>().cost.ToString();
		text[3] = Lang.blessingTooltips[blessings[(int)blessingType].GetComponent<Blessing>().bID][SettingsManager.Instance.language];

		return text;
	}

	public string[] GetTowerInfoText(Constants.towerType towerType, GameObject[] towers) {
		string[] text = new string[4];
		text[0] = Lang.towerNames[towerType][SettingsManager.Instance.language];
		text[1] = towers[(int)towerType].GetComponent<TowerState>().cost.ToString();
		text[2] = "0";
		text[3] = Lang.towerTooltips[towerType][SettingsManager.Instance.language];

		return text;
	}
	#endregion

	#region Radial Icon Scaling
	public void scaleIconOnSelect(int index) {
		radialIconScalers[index].scaleUp();
		if (index + SCALING_ICON_COUNT < radialIconScalers.Count) {
			radialIconScalers[index + SCALING_ICON_COUNT].scaleUp();
		}
	}

	public void scaleIconOnDeselect(int index) {
		radialIconScalers[index].scaleDown();
		if (index + SCALING_ICON_COUNT < radialIconScalers.Count) {
			radialIconScalers[index + SCALING_ICON_COUNT].scaleDown();
		}
	}

	// this sucks bad
	public void ScaleLaneIcon(int zone, int oldZone, bool canSpawnInMid) {
		if (zone == TOP) {
			if (oldZone != zone) {
				scaleIconOnSelect(TOP);
			}

			if (oldZone == MID && canSpawnInMid) {
				scaleIconOnDeselect(MID);
			}
			else if (oldZone == BOT) {
				scaleIconOnDeselect(BOT);
			}
		}
		else if (zone == MID) {
			if (oldZone != zone && canSpawnInMid) {
				scaleIconOnSelect(MID);
			}

			if (oldZone == TOP) {
				scaleIconOnDeselect(TOP);
			}
			else if (oldZone == BOT) {
				scaleIconOnDeselect(BOT);
			}
		}
		else if (zone == BOT) {
			if (oldZone != zone) {
				scaleIconOnSelect(BOT);
			}

			if (oldZone == MID && canSpawnInMid) {
				scaleIconOnDeselect(MID);
			}
			else if (oldZone == TOP) {
				scaleIconOnDeselect(TOP);
			}
		}
	}

	public void scaleDownLaneIcons() {
		scaleIconOnDeselect(TOP);
		scaleIconOnDeselect(MID);
		scaleIconOnDeselect(BOT);
	}
	#endregion

	#region Icon Grey Out
	public void GreyOutUnitIcons(List<Image> unitIcons, List<Image> unitIconsMasked, UnitSpawner uSpawner) {
		for (int i = 0; i < 4; i++) {
			if (pController.getGold() > uSpawner.getUnitCost((Constants.unitType)i) && !pController.baseZoneLocks[FIRST_UNIT_ZONE+i]) {  // not the best way, I know
				unitIcons[i].color = UnmaskedColor;
				unitIconsMasked[i].color = MaskedWhite;
			}
			else {
				unitIcons[i].color = UnmaskedGrey;
				unitIconsMasked[i].color = MaskedGray;
			}
		}
	}

	public void GreyOutTowerIcons(List<Image> towerIcons, List<Image> towerIconsMasked, List<float> towerGoldCosts) {
		for (int i = 0; i < 4; i++) {
			if (pController.getGold() > towerGoldCosts[i] && !pController.baseZoneLocks[LAST_TOWER_ZONE-i]) {
				towerIcons[3-i].color = UnmaskedColor;
				towerIconsMasked[3-i].color = MaskedWhite;
			}
			else {
				towerIcons[3-i].color = UnmaskedGrey;
				towerIconsMasked[3-i].color = MaskedGray;
			}
		}
	}

	public void ApplyLockIcon(Dictionary<int, bool> zoneLock) {
		for (int i=1; i<=zoneLock.Count; i++) {
			if (radialIconScalers[i - 1].gameObject.transform.Find("Lock")) {
				if (zoneLock[i]) {
					radialIconScalers[i - 1].gameObject.transform.Find("Lock").gameObject.SetActive(true);
					radialIconScalers[i - 1 + 18].gameObject.transform.Find("Lock").gameObject.SetActive(true);
				}
				else {
					radialIconScalers[i - 1].gameObject.transform.Find("Lock").gameObject.SetActive(false);
					radialIconScalers[i - 1 + 18].gameObject.transform.Find("Lock").gameObject.SetActive(false);
				}
			}
			if (radialIconScalers[i - 1].gameObject.transform.Find("UnlockPrompt")) {
				if (zoneLock[i] && pController.GetCanUnlock()) {
					radialIconScalers[i - 1].gameObject.transform.Find("UnlockPrompt").gameObject.SetActive(true);
					radialIconScalers[i - 1 + 18].gameObject.transform.Find("UnlockPrompt").gameObject.SetActive(true);
				}
				else {
					radialIconScalers[i - 1].gameObject.transform.Find("UnlockPrompt").gameObject.SetActive(false);
					radialIconScalers[i - 1 + 18].gameObject.transform.Find("UnlockPrompt").gameObject.SetActive(false);
				}
			}
		}
	}

	#endregion

	#region Error Feedback
	public void ErrorBackgroundFlash() {
		StopCoroutine("ErrorBackgroundFlashHelper");
		StartCoroutine("ErrorBackgroundFlashHelper");
	}

	private IEnumerator ErrorBackgroundFlashHelper() {
		Color errorRed = new Color(1f, 0f, 0f, 0.85f);
		float errorFadeCounter = 0f;

		errorBackgroundFade.color = errorRed;

		while (errorBackgroundFade.color.a > 0) {
			errorFadeCounter += Time.deltaTime;
			errorBackgroundFade.color = Color.Lerp(errorRed, Color.clear, errorFadeCounter / uiErrorFadeTime);

			yield return null;
		}
	}
	#endregion

	#region Upgrade Bars
	public void setCurrencyValues(float _gold, float _favor) {
		playerGold = _gold;
		playerFavor = _favor;
	}
	#endregion
}
