using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;

public abstract class PlayerController : MonoBehaviour
{
	#region Declarations
	// ---------------------- Constants ---------------------------
	public string rewiredPlayerKey = "Player0";
	protected const int LOADOUT_COUNT = 4;

	public GameObject reticule;

	// ---------------------- Unit Variables ----------------------
	protected UnitSpawner uSpawner;
	protected List<string> inputList;

	// ---------------------- Tower Variables ----------------------
	protected GameObject[] towers;
	protected List<float> towerGoldCosts;

	// ---------------------- Patron/Blessing Variables ----------------------
	public Constants.patrons patron;
	protected GameObject passive;
	protected GameObject[] blessings;
	protected Blessing[] blessingScripts;
	protected string[] blessingNames;
	protected ActiveBlessing_I activeBlessing;
	protected ActiveBlessing_MultiTarget_I multiTargetActiveBlessing = null;
	protected LaneBlessing_I laneBlessing;
	protected int activeBlessingIndex, unitBlessingIndex, laneBlessingIndex;    // used to record when an active blessing is used for statistics
	protected ParticleSystem activeTargeter, secondaryActiveTargeter;

	// ---------------------- Expansions ----------------------
	protected int expansionCount = 0;
	protected int mines = 0;
	protected int temples = 0;
	protected int trainingGrounds = 0;
	protected GameObject[] expansions;
	protected ExpansionSpawner eSpawner;

	// ---------------------- Currency ----------------------
	public float startingGold;
	protected float gold;
	public float startingFavor;
	protected float favor;

    private int extraGoldGen = 0;       //extra gold for AI in case of hard mode

    // -------------------- UI Variables --------------------
    public bool isStunned = false;          // right now just used during startup and pause
	protected bool canMoveReticule = false;

	// -------------------- UI References -------------------
	[Header("General Player UI")]
	public GameObject healthBar;
	public GameObject blessingDurationsGroup;
	[SerializeField] protected Meter meter;
	[SerializeField] protected GameObject radialMenu;
	[SerializeField] private TMPro.TextMeshProUGUI playerName;
	[SerializeField] private GameObject playerIcon;
	[SerializeField] private UnityEngine.UI.Image playerIconImage;
	private Sprite playerPortrait;

	[Header("Audio")]
	[FMODUnity.EventRef] [SerializeField] private string dropEvent;


	// ----------------------- Scripts ----------------------
	protected RadialVisuals radVis;

	// ------------------ Manager References ----------------
	protected GameManager g;
	protected SoundManager s;
	public LoadoutManager l;
	protected StatCollector stats;

	// ------------------- Misc Variables -------------------
	private string sceneName;
	protected bool canSpawnInMid;             // marked true on maps where there is a mid lane to be spawned in

	#endregion

	#region System Functions
	protected virtual void Start() {
		if (SettingsManager.Instance.GetIsTutorial())
		{
			l = GameObject.Find("TutorialManager(Clone)").GetComponent<TutorialManager>().tutorialLoadout.GetComponent<LoadoutManager>();
		}
		else
		{
			l = GameObject.Find("LoadoutManager").GetComponent<LoadoutManager>();
		}
		g = GameManager.Instance;
		s = SoundManager.Instance;
		stats = StatCollector.Instance;
		uSpawner = GetComponent<UnitSpawner>();
		eSpawner = GetComponent<ExpansionSpawner>();

		Scene currentScene = SceneManager.GetActiveScene();
		sceneName = currentScene.name;

		canSpawnInMid = !Constants.isTwoLaneMap[Constants.sceneCodes[sceneName]];

		gold = startingGold;
		favor = startingFavor;

		// Instantiating each of the blessings
		blessings = new GameObject[LOADOUT_COUNT];
		towers = new GameObject[LOADOUT_COUNT];
		expansions = new GameObject[3];
		retrieveBlessings();
		retrieveTowers();
		retrieveExpansions();

        // instantiate passive unless we're playing single player against an AI enemy that doesn't use blessings (conquest heretics)
		if (!(SettingsManager.Instance.GetIsSinglePlayer() && !GameManager.Instance.AIUseBlessings && rewiredPlayerKey == PlayerIDs.player2)) {
			passive = Instantiate(passive);
			passive.GetComponent<PatronPassive>().Initialize(this);
		}

		// gather tower gold costs
		towerGoldCosts = new List<float>();
		for (int i = 0; i < towers.Length; i++) {
			towerGoldCosts.Add(towers[i].GetComponent<TowerState>().getCost());
		}

		// Set playercontroller values for anything that might need it
		eSpawner.SetPlayerController(this);
		uSpawner.SetPlayerController(this);
		meter.SetPlayerController(this);
		GetComponentInChildren<KeepManager>().SetPlayerController(this);
		GetComponentInChildren<TowerSpawner>().SetPlayerController(this);

		// set player usernames and portraits
		if (SettingsManager.Instance.GetIsOnline()) {
			playerName.gameObject.SetActive(true);
			playerIcon.SetActive(true);
			if (rewiredPlayerKey == PlayerIDs.player1) {	// this isnt great, would be nice to be able to get just host and guest, but lobby stuff is kinda sketchy tbh
				if (OnlineManager.Instance.GetIsHost()) {
					playerName.SetText(SteamWorksManager.Instance.GetUsername());
				}
				else {
					playerName.SetText(OnlineManager.Instance.opponent.Name);
				}
				playerPortrait = LoadoutManager.Instance.p1Portrait;
			}
			else {
				if (OnlineManager.Instance.GetIsHost()) {
					playerName.SetText(OnlineManager.Instance.opponent.Name);	
				}
				else {
					playerName.SetText(SteamWorksManager.Instance.GetUsername());
				}
				playerPortrait = LoadoutManager.Instance.p2Portrait;
			}

			// set portrait
			playerIconImage.sprite = playerPortrait;
		}
		else {
			playerName.gameObject.SetActive(false);
			playerIcon.SetActive(false);
		}
	}
	#endregion

	#region Economy Functions
	public virtual void addGold(float amountToAdd) {
		stats.recordGoldEarned(rewiredPlayerKey, amountToAdd);
		gold += amountToAdd;
		meter.AddGoldMeter(amountToAdd);
	}

	public void spendGold(float amountToSpend) {
		float currentGold = gold;
		gold -= amountToSpend;
		GameManager.Instance.SpinCurrency(rewiredPlayerKey, true, amountToSpend, currentGold);
	}

	public virtual void addFavor(float amountToAdd) {
		stats.recordFavorEarned(rewiredPlayerKey, amountToAdd);
		favor += amountToAdd;
		meter.AddFavorMeter(amountToAdd);
	}

	public void spendFavor(float amountToSpend) {
		GetComponentInChildren<KeepManager>().PulseEmission();
		float currentFavor = favor;
		favor = Mathf.Max(favor - amountToSpend, 0);
		GameManager.Instance.SpinCurrency(rewiredPlayerKey, false, Mathf.Min(amountToSpend, currentFavor), currentFavor);
	}

	public void setGold(float newAmount)
	{
		gold = newAmount;
	}
	#endregion

	#region Effect and Feedback Functions
	public virtual void purchaseErrorFeedback(string errorType) {
	}

	public virtual void ControllerVibration(int motorIndex, float fullMotorSpeed, float duration) {
	}

	public void sound_wallDrops() {
		FMOD.Studio.EventInstance drop = FMODUnity.RuntimeManager.CreateInstance(dropEvent);
		drop.set3DAttributes(FMODUnity.RuntimeUtils.To3DAttributes(gameObject.transform.Find("Keep")));
		drop.setProperty(FMOD.Studio.EVENT_PROPERTY.MAXIMUM_DISTANCE, 1000f);
		drop.start();
		drop.release();
	}
	#endregion

	#region Loadout Setup
	public void retrieveBlessings() {
		patron = l.getPatronAssignment(rewiredPlayerKey).patronID;
		passive = l.getPassiveAssignment(rewiredPlayerKey).gameObject;
		for (int i = 0; i < blessings.Length; i++) {
			blessings[i] = l.getBlessingAssignment(i, rewiredPlayerKey);
		}
	}

	public void retrieveTowers() {
		for (int i = 0; i < towers.Length; i++) {
			towers[i] = l.getTowerAssignment(i, rewiredPlayerKey);
		}
	}

	private void retrieveExpansions() {
		for (int i = 0; i < expansions.Length; i++) {
			expansions[i] = l.expansions[i].gameObject;
		}
	}
	#endregion

	#region Getters and Setters
	public Meter GetMeter() {
		return meter;
	}

	public float getGold() {
		return gold;
	}

	public float getFavor() {
		return favor;
	}

	public int GetExpansionCount() {
		return expansionCount;
	}

	public bool GetCanPurchaseExpansion() {
		return meter.CanUpgrade();
	}

	public bool GetCanUnlock() {
		return meter.CanUnlock();
	}

	public int GetMineCount() {
		return mines;
	}

	public void AddMine() {
		mines++;
	}

	public void RemoveMine() {
		if (mines > 0) {
			mines--;
		}
	}

	public int GetTempleCount() {
		return temples;
	}

	public void AddTemple() {
		temples++;
	}

	public void RemoveTemple() {
		if (temples > 0) {
			temples--;
		}
	}

	public int GetTrainingGroundCount() {
		return trainingGrounds;
	}

	public void AddTrainingGround() {
		trainingGrounds++;
		uSpawner.IncrementTrainingGroundCount();
	}

	public bool GetCanMoveReticule() {
		return canMoveReticule;
	}

	public virtual void levelUp() {
		if (expansionCount < 2) {
			expansionCount += 1;
		}
	}

	public bool getCanSpawnInMid() {
		return canSpawnInMid;
	}

	public abstract bool UnlockedAll();

    //AI specific Getters and Setters
    public int GetExtraGold(){
        return extraGoldGen;
    }

    public void SetExtraGold(int extraGold)
    {
        extraGoldGen = extraGold;
    }

    public void SetFavor(int _favor) {
        favor = _favor;
    }
    #endregion
}
