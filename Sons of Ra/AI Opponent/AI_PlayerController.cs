using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

public class AI_PlayerController : PlayerController
{
	#region Declarations
	[Header("References")]
	public AI_ThreatDetection threatDetector;
	private AI_GridRep gridRep;
	private AI_BlessingHandler blessingHandler;
	private AI_Unlocks unlockHandler;
	[SerializeField] private Difficulty difficulty;
	[SerializeField] private Strategy strategy;
	public GameObject keep;

	[Header("Asset Selection")]
	[SerializeField] private float spearmanWeight;
	[SerializeField] private float catapultWeight;
	[SerializeField] private float archerWeight;
	[SerializeField] private float shieldbearerWeight;
	private List<float> unitWeightList = new List<float>();
	private List<AI_Unlocks.unlockItems> lockedItems = new List<AI_Unlocks.unlockItems> {
		AI_Unlocks.unlockItems.archer,
		AI_Unlocks.unlockItems.shieldbearer,
		AI_Unlocks.unlockItems.stasis,
		AI_Unlocks.unlockItems.obelisk,
		AI_Unlocks.unlockItems.sun,
	};
	private List<Constants.unitType> unlockedUnits = new List<Constants.unitType> {
		Constants.unitType.spearman,
		Constants.unitType.catapult,
	};
	private List<Constants.towerType> unlockedTowers = new List<Constants.towerType> {
		Constants.towerType.archerTower,
	};

	[Header("Analysis Values")]
	[SerializeField] private float[] laneThreats = { 0f, 0f, 0f };
	private int[] towerLaneThreats = { 0, 0, 0 };
	[SerializeField] private int towerThreatActionLevel;
	private Constants.radialCodes currentPriority;
	[SerializeField] private float easyWaitTime;
	bool waiting = true;

	// Action Values moved to Strategy.cs
	#endregion

	#region System Functions
	protected override void Start()
    {
		base.Start();

		// turn off radial menu
		radialMenu.transform.parent.gameObject.SetActive(false);

		// Get references
		gridRep = new AI_GridRep(this, GameObject.Find("InfluenceGrid").GetComponent<Squares>());
		blessingHandler = GetComponent<AI_BlessingHandler>();
		unlockHandler = GetComponent<AI_Unlocks>();

		// set variables
		unitWeightList.Add(spearmanWeight);
		unitWeightList.Add(catapultWeight);

		// Trigger coroutines
		StartCoroutine(AnalyzeUnitThreats());
		StartCoroutine(AnalyzeTowerThreats());
		StartCoroutine(ImmediateResponse());
		StartCoroutine(StartAnalysis());	// this wraps the functions that allow the AI to determine what to do, and also provides a wait time for easy

		SonsOfRa.Events.GameEvents.TowerSpawn += UpdateGridTowerSpawn;
		SonsOfRa.Events.GameEvents.TowerDie += UpdateGridTowerDie;
	}

	private void OnDestroy() {
		SonsOfRa.Events.GameEvents.TowerSpawn -= UpdateGridTowerSpawn;
		SonsOfRa.Events.GameEvents.TowerDie -= UpdateGridTowerDie;
	}
	#endregion

	#region Action Functions
	#region Unit Actions
	private void ChooseUnitSpawnAction() {
		float random = UnityEngine.Random.Range(0f, 1f);

		if (random < strategy.attack) {
			AttackSpawn();
		}
		else if (random < strategy.attack + strategy.defend) {
			DefenseSpawn();
		}
		else if (random < strategy.attack + strategy.defend + strategy.probe) {
			ProbeSpawn();
		}
	}

	private void AttackSpawn() {
		Constants.radialCodes best = (Constants.radialCodes)LaneOfAdvantage(); // get best lane from threat
		Debug.Log("attack spawn");
		int clusterSize = 0;
		if (laneThreats.Max() < strategy.highThreatValue) {
			clusterSize = gold >= strategy.liberalGoldAmount
				? UnityEngine.Random.Range(strategy.AttackSpawnMinClusterSize, strategy.AttackSpawnMaxClusterSize + 1) : strategy.AttackSpawnMinClusterSize / 2;
		}
		else {
			clusterSize = gold >= strategy.liberalGoldAmount ? strategy.AttackSpawnMinClusterSize : strategy.AttackSpawnMinClusterSize / 2;
		}

		// spawn units
		for (int i=0; i < clusterSize; i++) {
			Constants.unitType unitToSpawn = SelectUnit();
			if (gold >= uSpawner.getUnitCost(uSpawner.units[(int)unitToSpawn].GetComponent<UnitAI>().type) && !isStunned) {
				uSpawner.addToSpawnQueue((int)unitToSpawn, best.ToString()); 
			}
			else {
				continue;
			}
		}
	}

	private void DefenseSpawn() {
		Constants.radialCodes worst = (Constants.radialCodes)LaneOfDisadvantage(); // get worsts lane from threat
		int clusterSize = gold >= strategy.liberalGoldAmount ? strategy.DefenseSpawnClusterSize : strategy.DefenseSpawnClusterSize / 2;

		// spawn units
		for (int i = 0; i < clusterSize; i++) {
			Constants.unitType unitToSpawn = SelectUnit();
			if (gold >= uSpawner.getUnitCost(uSpawner.units[(int)unitToSpawn].GetComponent<UnitAI>().type) && !isStunned) {
				uSpawner.addToSpawnQueue((int)unitToSpawn, worst.ToString());
			}
			else {
				continue;
			}
		}
	}

	private void ProbeSpawn() {
		// choose random lane
		Constants.radialCodes chosenLane = (Constants.radialCodes)UnityEngine.Random.Range(0, 2);

		if (!canSpawnInMid && chosenLane == Constants.radialCodes.mid) {
			while (chosenLane == Constants.radialCodes.mid) {
				chosenLane = (Constants.radialCodes)UnityEngine.Random.Range(0, 2);
			}
		}

		int clusterSize = gold >= strategy.liberalGoldAmount ? strategy.ProbeSpawnMinClusterSize : strategy.ProbeSpawnMinClusterSize / 2;

		for (int i = 0; i < clusterSize; i++) {
			Constants.unitType unitToSpawn = SelectUnit();
			if (gold >= uSpawner.getUnitCost(uSpawner.units[(int)unitToSpawn].GetComponent<UnitAI>().type) && !isStunned) {
				uSpawner.addToSpawnQueue((int)unitToSpawn, chosenLane.ToString()); 
			}
			else {
				continue;
			}
		}
	}

	private void AntiTowerSpawn(Constants.radialCodes lane) {
		if (gold >= uSpawner.getUnitCost(Constants.unitType.catapult) && !isStunned) {
			uSpawner.addToSpawnQueue((int)Constants.unitType.catapult, lane.ToString());
		}
	}

	public void SpawnBlessingUnit(Constants.unitType unit, Constants.radialCodes lane) {
		uSpawner.addToSpawnQueue((int)unit, lane.ToString());
	}

	public bool PanicSpawn(Constants.radialCodes lane, int unitCount, Constants.unitType focusUnit) {
		int panicSpawnCount;

		panicSpawnCount = UnityEngine.Random.Range((int)Mathf.Ceil(unitCount / 2f), (int)Mathf.Ceil(unitCount * 1.5f));

		if (gold < uSpawner.getUnitCost(Constants.unitType.spearman)) {
			return false;
		}

		if (focusUnit == Constants.unitType.archer) {
			float shieldChance = 0.5f;

			for (int i = 0; i < panicSpawnCount; i++) {
				if (UnityEngine.Random.Range(0f, 1f) <= shieldChance && gold >= uSpawner.getUnitCost(Constants.unitType.shieldbearer) && !isStunned && !lockedItems.Contains(AI_Unlocks.unlockItems.shieldbearer)) {
					uSpawner.addToSpawnQueue((int)Constants.unitType.shieldbearer, lane.ToString());
					shieldChance /= 2;
				}
				else if (gold >= uSpawner.getUnitCost(Constants.unitType.spearman) && !isStunned) {
					uSpawner.addToSpawnQueue((int)Constants.unitType.spearman, lane.ToString());
				}
			}
		}
		else if (focusUnit == Constants.unitType.shieldbearer) {
			for (int i = 0; i < panicSpawnCount; i++) {
				if (gold >= uSpawner.getUnitCost(Constants.unitType.spearman) && !isStunned) {
					uSpawner.addToSpawnQueue((int)Constants.unitType.spearman, lane.ToString());
				}
			}
		}
		else {
			float archerChance = 0.33f;

			for (int i = 0; i < panicSpawnCount; i++) {
				if (UnityEngine.Random.Range(0f, 1f) <= archerChance && gold >= uSpawner.getUnitCost(Constants.unitType.archer) && !isStunned && !lockedItems.Contains(AI_Unlocks.unlockItems.archer)) {
					uSpawner.addToSpawnQueue((int)Constants.unitType.archer, lane.ToString());
					archerChance /= 2;
				}
				else if (gold >= uSpawner.getUnitCost(Constants.unitType.spearman) && !isStunned) {
					uSpawner.addToSpawnQueue((int)Constants.unitType.spearman, lane.ToString());
				}
			}
		}

		return true;
	}

	private Constants.unitType SelectUnit() {
		float randomUnitValue = UnityEngine.Random.Range(0f, unitWeightList.Sum());
		for (int i=0; i<unlockedUnits.Count; i++) {
			if (randomUnitValue <= unitWeightList.Take(i+1).Sum()) {
				return unlockedUnits[i];
			}
		}

		return Constants.unitType.spearman;
	}
	#endregion

	#region Tower Actions
	private void ChooseTowerSpawnAction() {
		float random = UnityEngine.Random.Range(0f, 1f);
		if (!isStunned)
		{
			if (random < strategy.attack)
			{
				TerritoryExpansionPlaceTower((Constants.radialCodes)LaneOfAdvantage());
			}
			else /*if (random < strategy.attack + strategy.defend)*/
			{
				if (laneThreats[LaneOfDisadvantage()] < strategy.highThreatValue)
				{
					DefensivePlaceTower();
				}
				else
				{
					SupportingPlaceTower();
				}
			}
		}		
	}

	private void UpdateGridTowerSpawn(GameObject spawnedTower = null) {
		gridRep.UpdateTiles();
	}

	private void UpdateGridTowerDie() {
		gridRep.UpdateTiles();
	}

	private void spawnTowerAt(AI_TileRep location) {
		List<Constants.towerType> spawnableAtLocation = new List<Constants.towerType> {
			Constants.towerType.archerTower
		};

		// add towers to secondary list based on if selected location can have them placed there
		if (unlockedTowers.Contains(Constants.towerType.stasisTower)) {
			spawnableAtLocation.Add(Constants.towerType.stasisTower);
		}
		if (unlockedTowers.Contains(Constants.towerType.obelisk) && location.GetCanPlaceObelisk()) {
			spawnableAtLocation.Add(Constants.towerType.obelisk);
		}
		if (unlockedTowers.Contains(Constants.towerType.sunTower) && (location.GetCanPlaceSunTowerHoriz() || location.GetCanPlaceSunTowerVert())) {
			spawnableAtLocation.Add(Constants.towerType.sunTower);
		}

		Constants.towerType tower = spawnableAtLocation[UnityEngine.Random.Range(0, spawnableAtLocation.Count)];

		if (getGold() >= towerGoldCosts[(int)tower]) {
				
			reticule.SetActive(true);
			reticule.GetComponent<TowerSpawner>().holdTower(towers[(int)tower]);
			reticule.GetComponent<TowerSpawner>().PreviewInfluence();

			if (tower == Constants.towerType.sunTower && !location.GetCanPlaceSunTowerHoriz() && location.GetCanPlaceSunTowerVert()) {
				reticule.GetComponent<TowerSpawner>().rotateTowerRight();
			}

			reticule.GetComponent<CursorMovement>().MoveTo(location.GetGridIndices()[0], location.GetGridIndices()[1]);

			reticule.GetComponent<TowerSpawner>().trySpawn();
			reticule.GetComponent<TowerSpawner>().ClearPreview();
			InfluenceTileDictionary.UncolorTiles(rewiredPlayerKey);
			reticule.SetActive(false);
		}
	}

	/// <summary>
	/// Place a tower randomly near lane of highest threat
	/// </summary>
	private void DefensivePlaceTower() {
		Constants.radialCodes worst = (Constants.radialCodes)LaneOfDisadvantage(); // get worsts lane from threat
		Debug.Log("defensive place");

		List<AI_TileRep> possibleTiles = gridRep.GetPlaceableTilesInLane(worst);
		AI_TileRep chosenTile = possibleTiles[UnityEngine.Random.Range(0, possibleTiles.Count - 1)];

		spawnTowerAt(chosenTile);
	}

	/// <summary>
	/// More intensive DefensivePlaceTower() that searches for an optimal location at which
	/// to place a tower by looking for areas near large clusters of enemy units
	/// </summary>
	private void SupportingPlaceTower() {
		Constants.radialCodes worst = (Constants.radialCodes)LaneOfDisadvantage(); // get worsts lane from threat
		Debug.Log("supporting place");

		AI_TileRep chosenTile = gridRep.GetClosestPlaceableTile(
			threatDetector.GetLaneGraph(worst).FindUnitCluster(PlayerIDs.player1, 5f).waypoint.transform.position,
			worst);

		spawnTowerAt(chosenTile);
	}

	/// <summary>
	/// Pick a lane and place a tower close to the edge of owned territory to expand influence
	/// </summary>
	/// <param name="laneCode">A lane near which to place a tower. No lane passed will cause a random one to be chosen</param>
	public void TerritoryExpansionPlaceTower(Constants.radialCodes laneCode = Constants.radialCodes.none) {
		if (laneCode == Constants.radialCodes.none) {
			laneCode = (Constants.radialCodes)SelectRandomLane();
		}
		Debug.Log("expansion place");

		List<AI_TileRep> selectedLaneTiles = gridRep.GetPlaceableTilesInLane(laneCode);
		IEnumerable<AI_TileRep> orderedLaneTiles = selectedLaneTiles.OrderBy(tile => tile.GetTileObject().transform.position.z);

		List<AI_TileRep> furthestTiles = new List<AI_TileRep>();

		foreach (AI_TileRep tile in orderedLaneTiles) {
			furthestTiles.Add(tile);
			if (furthestTiles.Count >= strategy.territoryExpansionTileListMax) {
				break;
			}
		}

		AI_TileRep chosenTile = furthestTiles[UnityEngine.Random.Range(0, furthestTiles.Count - 1)];
		spawnTowerAt(chosenTile);
	}
	#endregion
	#endregion

	#region State Analysis
	/// <summary>
	/// Gets lane of greatest disadvantage, or a random lane if all lanes are equal in threat
	/// </summary>
	/// <returns>Index of lane of greatest disadvantage</returns>
	private int LaneOfDisadvantage() {
		if (!canSpawnInMid) {
			laneThreats[(int)Constants.radialCodes.mid] = float.MinValue;
		}

		if (laneThreats.Distinct().Count() == 1) {
			return SelectRandomLane();
		}
		else {
			return Array.IndexOf(laneThreats, laneThreats.Max());
		}
	}

	/// <summary>
	/// Get the greatest advantage in unit count the opponent has in a lane
	/// </summary>
	/// <returns>The number of units more than the player the opponent has in the disadvantaged lane</returns>
	private int WorstLaneUnitDifferential() {
		int worstCount = int.MinValue;

		foreach (Constants.radialCodes key in threatDetector.UnitLaneCounts.Keys) {
			if (threatDetector.UnitLaneCounts[key] > worstCount) {
				if (key != Constants.radialCodes.mid || canSpawnInMid) {
					worstCount = threatDetector.UnitLaneCounts[key];
				}
			}
		}

		return worstCount;
	}

	/// <summary>
	/// Gets lane of greatest advantage, or a random lane if all lanes are equal in threat
	/// </summary>
	/// <returns>Index of lane of greatest advantage</returns>
	private int LaneOfAdvantage() {
		if (!canSpawnInMid) {
			laneThreats[(int)Constants.radialCodes.mid] = float.MaxValue;
		}

		if (laneThreats.Distinct().Count() == 1) {
			return SelectRandomLane();
		}
		else {
			return Array.IndexOf(laneThreats, laneThreats.Min());
		}
	}
	#endregion

	#region Coroutines
	private IEnumerator StartAnalysis() {
		yield return new WaitUntil(() => GameManager.Instance.gameStarted);

		if (SettingsManager.Instance.GetDifficulty() == 0) {
			Coroutine wait = StartCoroutine(Waiter());
			yield return new WaitUntil(() => !waiting || Array.Exists(laneThreats, x => x > 0f && x != float.MaxValue));
			if (wait != null) {
				StopCoroutine(wait);
			}
		}

		StartCoroutine(MakeDecisions());
		if (GameManager.Instance.AIUseBlessings) {
			StartCoroutine(ScanMapForBlessingUse());
		}
		StartCoroutine(EvaluateUnlocks());
	}

	private IEnumerator Waiter() {
		yield return new WaitForSeconds(easyWaitTime);
		waiting = false;
	}

	public void Stop() {
		StopAllCoroutines();
	}

	IEnumerator AnalyzeUnitThreats() {
		yield return new WaitUntil(() => GameManager.Instance.gameStarted);

		while (true) {
			laneThreats = threatDetector.CalculateLaneThreats();

			float highThreat = LaneOfDisadvantage();
			float highAdvantage = LaneOfAdvantage();
			
			yield return new WaitForSeconds(difficulty.threatAnalysisRefresh);
		}
	}

	IEnumerator AnalyzeTowerThreats() {
		yield return new WaitUntil(() => GameManager.Instance.gameStarted);

		while (true) {
			towerLaneThreats = threatDetector.GetLaneTowerThreats();

			if (!canSpawnInMid) {
				towerLaneThreats[(int)Constants.radialCodes.mid] = int.MinValue;
			}

			int highThreat = towerLaneThreats.Max();
			Constants.radialCodes highThreatLane = (Constants.radialCodes)Array.IndexOf(towerLaneThreats, highThreat);


			if (highThreat > towerThreatActionLevel) {
				AntiTowerSpawn(highThreatLane);
			}

			yield return new WaitForSeconds(difficulty.towerAnalysisRefresh);
		}
	}

	IEnumerator MakeDecisions() {
		yield return new WaitUntil(() => GameManager.Instance.gameStarted);

		while (true) {
			float random = UnityEngine.Random.Range(0f, 1f);

			// if no lane is a big threat, and no lane has a big advantage to push, give a chance to save for gold
			if (random < GetGoldSaveChance() 
				&& laneThreats[LaneOfDisadvantage()] < strategy.immediateThreatValue
				&& laneThreats[LaneOfAdvantage()] > strategy.immediateAttackValue) {
			}
			else {
				random = UnityEngine.Random.Range(0f, 1f);
				if (random < strategy.unitPreference) {
					ChooseUnitSpawnAction();
				}
				else {
					ChooseTowerSpawnAction();
				}
			}

			yield return new WaitForSeconds(UnityEngine.Random.Range(difficulty.actionIntervalMin, difficulty.actionIntervalMax));
		}
	}

	IEnumerator ImmediateResponse() {
		yield return new WaitUntil(() => GameManager.Instance.gameStarted);

		while (true) {
			if (laneThreats[LaneOfDisadvantage()] > strategy.immediateThreatValue 
				|| WorstLaneUnitDifferential() > strategy.immediateThreatUnitDifferential) {
				float random = UnityEngine.Random.Range(0f, 1f);

				if (random < strategy.unitPreference) {
					DefenseSpawn();
				}
				else if (random < strategy.unitPreference + strategy.towerPreference) {
					DefensivePlaceTower();
				}

				yield return new WaitForSeconds(strategy.immediateResponseCooldown);
			}

			yield return null;
		}
	}

	IEnumerator ScanMapForBlessingUse() {
		yield return new WaitUntil(() => GameManager.Instance.gameStarted);

		float minimumBlessingCost = blessingHandler.blessingScripts.ToList().OrderBy(blessing => blessing.cost).First().cost;

		while (true) {
			if (favor >= minimumBlessingCost && 
				(UnityEngine.Random.Range(0f, 1f) <= GetBlessingUseChance(minimumBlessingCost) 
				|| laneThreats[LaneOfDisadvantage()] > strategy.immediateThreatValue
				|| laneThreats[LaneOfAdvantage()] < strategy.immediateAttackValue)) {
				blessingHandler.testBlessingConditions();

				yield return new WaitForSeconds(difficulty.blessingScanFrequency);
			}

			yield return null;
		}
	}

	IEnumerator EvaluateUnlocks() {
		yield return new WaitUntil(() => GameManager.Instance.gameStarted);
		bool savingForExpansion = false;

		// if it wants to unlock something right off the bat, choose randomly
		if (meter.CanUnlock() && UnityEngine.Random.Range(0f, 1f) <= strategy.startingUnlockChance) {
			Debug.Log("first if unlock");
			int unlockedItemIndex = UnityEngine.Random.Range(0, lockedItems.Count);

			meter.Unlock((int)lockedItems[unlockedItemIndex]);
			if (lockedItems[unlockedItemIndex] == AI_Unlocks.unlockItems.archer) {
				Debug.Log("unlocking");
				UnlockArcher();
			}
			else if (lockedItems[unlockedItemIndex] == AI_Unlocks.unlockItems.shieldbearer) {
				Debug.Log("unlocking");
				UnlockShieldbearer();
			}
			else {
				if (lockedItems[unlockedItemIndex] == AI_Unlocks.unlockItems.stasis) {
					Debug.Log("unlocking");
					unlockedTowers.Add(Constants.towerType.stasisTower);
				}
				else if (lockedItems[unlockedItemIndex] == AI_Unlocks.unlockItems.obelisk) {
					Debug.Log("unlocking");
					unlockedTowers.Add(Constants.towerType.obelisk);
				}
				else {
					Debug.Log("unlocking");
					unlockedTowers.Add(Constants.towerType.sunTower);
				}
				lockedItems.RemoveAt(unlockedItemIndex);
			}
		}

		// loop unlock checks
		while (expansionCount < 2 || lockedItems.Count > 0) {
			yield return new WaitForSeconds(difficulty.meterCheckFrequency);

			float rand = UnityEngine.Random.Range(0f, 1f);

			// decide if we're going to save for expansion
			if (rand < strategy.expansionPreferenceBase && !savingForExpansion && expansionCount < 2) {
				savingForExpansion = true;
			}

			if (!savingForExpansion) {
				// unlock a unit
				if (lockedItems.Contains(AI_Unlocks.unlockItems.archer) && unlockHandler.GetArcherEvaluation() >= .75f && meter.CanUnlock()) {
					Debug.Log("unlocking");
					UnlockArcher();
				}
				if (lockedItems.Contains(AI_Unlocks.unlockItems.shieldbearer) && unlockHandler.GetShieldEvaluation() >= .75f && meter.CanUnlock()) {
					Debug.Log("unlocking");
					UnlockShieldbearer();
				}

				// unlock random tower
				List<AI_Unlocks.unlockItems> nonUnitUnlocks = new List<AI_Unlocks.unlockItems>(lockedItems);
				nonUnitUnlocks.Remove(AI_Unlocks.unlockItems.archer);
				nonUnitUnlocks.Remove(AI_Unlocks.unlockItems.shieldbearer);
				if (nonUnitUnlocks.Count > 0 && meter.CanUnlock()) {
					int randNonUnit = UnityEngine.Random.Range(0, nonUnitUnlocks.Count - 1);
					Debug.Log("unlocking");
					meter.Unlock((int)nonUnitUnlocks[randNonUnit]);
					lockedItems.Remove(nonUnitUnlocks[randNonUnit]);

					// add to list of unlocked towers
					if (nonUnitUnlocks[randNonUnit] == AI_Unlocks.unlockItems.stasis) {
						unlockedTowers.Add(Constants.towerType.stasisTower);
					}
					else if (nonUnitUnlocks[randNonUnit] == AI_Unlocks.unlockItems.obelisk) {
						unlockedTowers.Add(Constants.towerType.obelisk);
					}
					else if (nonUnitUnlocks[randNonUnit] == AI_Unlocks.unlockItems.sun) {
						unlockedTowers.Add(Constants.towerType.sunTower);
					}
				}
			}
			else {	// "emergency" unit spawns
				if (lockedItems.Contains(AI_Unlocks.unlockItems.archer) && unlockHandler.GetArcherEvaluation() >= 1.5f && meter.CanUnlock()) {
					UnlockArcher();
				}
				if (lockedItems.Contains(AI_Unlocks.unlockItems.shieldbearer) && unlockHandler.GetShieldEvaluation() >= 1.5f && meter.CanUnlock()) {
					UnlockShieldbearer();
				}
			}

			// make expansions
			if (meter.CanUpgrade()) {
				float randExpansion = UnityEngine.Random.Range(0f, 1f);

				if (randExpansion <= strategy.minePreference) {
					meter.Upgrade();
					Debug.Log("expanding");
					eSpawner.SpawnExpansion(expansions[(int)Constants.expansionType.mine], expansionCount);
					stats.recordExpansionSpawned(rewiredPlayerKey, Constants.expansionType.mine);
					sound_wallDrops();
					savingForExpansion = false;
				}
				else if (randExpansion <= strategy.minePreference + strategy.templePreference && GameManager.Instance.AIUseBlessings) {
					meter.Upgrade();
					Debug.Log("expanding");
					eSpawner.SpawnExpansion(expansions[(int)Constants.expansionType.temple], expansionCount);
					stats.recordExpansionSpawned(rewiredPlayerKey, Constants.expansionType.temple);
					sound_wallDrops();
					savingForExpansion = false;
				}
				else if (randExpansion <= strategy.minePreference + strategy.templePreference + strategy.barracksPreference) {
					meter.Upgrade();
					Debug.Log("expanding");
					eSpawner.SpawnExpansion(expansions[(int)Constants.expansionType.barracks], expansionCount);
					stats.recordExpansionSpawned(rewiredPlayerKey, Constants.expansionType.barracks);
					sound_wallDrops();
					savingForExpansion = false;
				}
			}
		}
	}
	#endregion

	#region Helpers
	private int SelectRandomLane() {
		return UnityEngine.Random.Range(0, 3);
	}

	private float GetBlessingUseChance(float minimumBlessingCost) {
		float baseLiberalFavorDifference = strategy.liberalBlessingFavorMinimum - minimumBlessingCost;
		float percentChanceDifference = 1 - strategy.liberalBlessingChance;
		float percentChanceChangePerFavor = percentChanceDifference / baseLiberalFavorDifference;

		return strategy.liberalBlessingChance + (((favor - minimumBlessingCost) / baseLiberalFavorDifference) * percentChanceChangePerFavor * 100);
	}

	private float GetGoldSaveChance() {
		// we're using 2500 gold to be "liberal/carefree" amount
		float percentOfLiberalGold = gold / strategy.liberalGoldAmount;
		float percentOfGoldSaveChanceDiff = (strategy.goldSaveChanceMinimum - strategy.goldSaveChanceBase) * percentOfLiberalGold;
		return strategy.goldSaveChanceMinimum + percentOfGoldSaveChanceDiff;
	}

	private void UnlockArcher() {
		meter.Unlock((int)AI_Unlocks.unlockItems.archer);
		lockedItems.Remove(AI_Unlocks.unlockItems.archer);
		unlockedUnits.Add(Constants.unitType.archer);
		unitWeightList.Add(archerWeight);
	}

	private void UnlockShieldbearer() {
		meter.Unlock((int)AI_Unlocks.unlockItems.shieldbearer);
		lockedItems.Remove(AI_Unlocks.unlockItems.shieldbearer);
		unlockedUnits.Add(Constants.unitType.shieldbearer);
		unitWeightList.Add(shieldbearerWeight);
	}

	public void UnlockEverything() {
		UnlockArcher();
		UnlockShieldbearer();
		unlockedTowers.Add(Constants.towerType.stasisTower);
		unlockedTowers.Add(Constants.towerType.obelisk);
		unlockedTowers.Add(Constants.towerType.sunTower);
		lockedItems.Clear();
	}
	#endregion

	public override bool UnlockedAll() {
		return lockedItems.Count == 0;
	}

	//AI specific Getters and Setters
	public void SetAiDifficulty(Difficulty diff, Strategy strat)
    {
        difficulty = diff;

		if (!SettingsManager.Instance.GetIsConquest()) {
			strategy = strat;
		}
    }

	public void SetAIStrategy(Strategy strat) {
		strategy = strat;
	}
}
