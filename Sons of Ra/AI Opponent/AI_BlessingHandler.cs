//using System;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

public class AI_BlessingHandler : MonoBehaviour
{
	public enum blessingKeywords { allyUnitCluster, enemyUnitCluster, withinDistance, allyTowerCluster, enemyTowerCluster,
		quantityOfAllies, alliesInLane, enemyHasTowers }

	[Header("References")]
	public GameObject[] blessings;
	public Blessing[] blessingScripts;
	private AI_PlayerController p;
	private StatCollector stats;
	private blessingDurationsDisplay blessingDurations;

	[Header("Blessing Condition Variables")]
	private List<blessingKeywords> tagsInLoadout;
	private List<BlessingTagChecker> tagCheckers;
	[SerializeField] private float clusterRadius;
	[SerializeField] private int unitClusterCount;
	[SerializeField] private int towerClusterCount;
	[SerializeField] private float distanceBetweenClusters;
	[SerializeField] private float quantityOfAllies;
	[SerializeField] private float quantityOfAlliesInLane;

	[Header("Blessing Usage Variables")]
	[SerializeField] private float solarFlareMaxDistance;
	[SerializeField] private float graspMaxDistance;

	// Blessing input data
	private List<(LaneGraphNode, int)> allyUnitClusters;
	private List<(LaneGraphNode, int)> enemyUnitClusters;
	private List<(List<Collider>, Vector3)> allyTowerClusters;
	private List<(List<Collider>, Vector3)> enemyTowerClusters;
	private Vector3 positionBetweenClusters;
	private Constants.radialCodes greatestLane;

	private void Start() {
		// Set up references
		p = GetComponent<AI_PlayerController>();
		stats = StatCollector.Instance;

		// Initialize lists
		blessings = new GameObject[4];
		blessingScripts = new Blessing[4];
		tagCheckers = new List<BlessingTagChecker>();
		tagsInLoadout = new List<blessingKeywords>();
		blessingDurations = GetComponent<blessingDurationsDisplay>();

		// Fill out variables
		retrieveBlessings();
	}

	private void retrieveBlessings() { 
		LoadoutManager l = LoadoutManager.Instance;
		for (int i = 0; i < blessingScripts.Length; i++) {
			blessings[i] = Instantiate(l.getBlessingAssignment(i, p.rewiredPlayerKey));	// Get blessing from loadout and instantiate
			blessingScripts[i] = blessings[i].GetComponent<Blessing>();					// Get script of instantiated blessing
			tagCheckers.Add(new BlessingTagChecker(blessingScripts[i].blessingTags));
			for (int j = 0; j < blessingScripts[i].blessingTags.Count; j++) {
				blessingKeywords currentTag = blessingScripts[i].blessingTags[j];
				if (!tagsInLoadout.Contains(currentTag)) {
					tagsInLoadout.Add(currentTag);
				}
			}
		}
	}

	public void testBlessingConditions() {
		allyUnitClusters = new List<(LaneGraphNode, int)>();
		enemyUnitClusters = new List<(LaneGraphNode, int)>();
		allyTowerClusters = new List<(List<Collider>, Vector3)>();
		enemyTowerClusters = new List<(List<Collider>, Vector3)>();
		positionBetweenClusters = new Vector3();
		greatestLane = Constants.radialCodes.none;

		resetTags();

		// check for each tag in selected blessings if its condition is filled
		if (tagsInLoadout.Contains(blessingKeywords.alliesInLane)) {
			greatestLane = CheckAlliesInLaneCondition();
		}
		if (tagsInLoadout.Contains(blessingKeywords.allyTowerCluster)) {
			CheckTowerClusterCondition(ref enemyTowerClusters, PlayerIDs.player2);
		}
		if (tagsInLoadout.Contains(blessingKeywords.allyUnitCluster)) {
			CheckUnitClusterCondition(ref allyUnitClusters, PlayerIDs.player2);
		}
		if (tagsInLoadout.Contains(blessingKeywords.enemyTowerCluster)) {
			CheckTowerClusterCondition(ref enemyTowerClusters, PlayerIDs.player1);
		}
		if (tagsInLoadout.Contains(blessingKeywords.enemyUnitCluster)) {
			CheckUnitClusterCondition(ref enemyUnitClusters, PlayerIDs.player1);
		}
		if (tagsInLoadout.Contains(blessingKeywords.quantityOfAllies)) {
			if (LivingUnitDictionary.dict[PlayerIDs.player2].Count > quantityOfAllies) {
				checkOffTags(blessingKeywords.quantityOfAllies);
			}
		}
		if (tagsInLoadout.Contains(blessingKeywords.withinDistance)) {
			CheckUnitsWithinDistanceCondition(allyUnitClusters, enemyUnitClusters, ref positionBetweenClusters);
		}
		if (tagsInLoadout.Contains(blessingKeywords.enemyHasTowers)) {
			if (LivingTowerDictionary.dict[PlayerIDs.player1].Count > 0) {
				checkOffTags(blessingKeywords.enemyHasTowers);
			}
		}

		int blessingRatingSum = 0;
		List<Blessing> usableBlessings = new List<Blessing>();

		for (int i = 0; i < blessingScripts.Length; i++) {
			if (tagCheckers[i].CheckConditions()) {
				blessingRatingSum += (int)blessingScripts[i].effectiveness;
				usableBlessings.Add(blessingScripts[i]);
			}
		}

		Debug.Log("usable blessings count = " + usableBlessings.Count);

		if (usableBlessings.Count > 0) {
			Blessing blessingToUse = DetermineBlessingToUse(blessingRatingSum, usableBlessings);
			Debug.Log(blessingToUse.blessingName);
			UseBlessing(blessingToUse);
		}
	}

	#region Blessing Usage
	private Blessing DetermineBlessingToUse(int ratingSum, List<Blessing> usableBlessings) {
		List<float> percentages = new List<float>();
		float random = UnityEngine.Random.Range(0f, 1f);
		float runningPercentTally = 0;

		// generate percentage tiers
		for (int i=0; i<usableBlessings.Count; i++) {
			float blessingPercent = usableBlessings[i].effectiveness / ratingSum;
			percentages.Add(blessingPercent + runningPercentTally);
			runningPercentTally += blessingPercent;
		}

		// based on percentage tiers, compare the random number and select blessing
		for (int i=0; i<percentages.Count; i++) {
			if (random < percentages[i]) {
				return usableBlessings[i];
			}
		}

		return usableBlessings[usableBlessings.Count - 1];
	}

	private void UseBlessing(Blessing blessingToUse) {
		if (p.isStunned == false) {
			if (blessingToUse.blessingName == "Betrayal") {
				UseBetrayal(blessingToUse);
			}
			else if (blessingToUse.blessingName == "Battle Rage") {
				UseBattleRage(blessingToUse);
			}
			else if (blessingToUse.blessingName == "Cyclone") {
				UseCyclone(blessingToUse);
			}
			else if (blessingToUse.blessingName == "Decay") {
				UseDecay(blessingToUse);
			}
			else if (blessingToUse.blessingName == "Earthquake") {
				UseEarthquake(blessingToUse);
			}
			else if (blessingToUse.blessingName == "Embalming Priest") {
				UseEmbalming(blessingToUse);
			}
			else if (blessingToUse.blessingName == "Empower") {
				UseEmpower(blessingToUse);
			}
			else if (blessingToUse.blessingName == "Grasp") {
				UseGrasp(blessingToUse);
			}
			else if (blessingToUse.blessingName == "Haste") {
				UseHaste(blessingToUse);
			}
			else if (blessingToUse.blessingName == "The Huntress") {
				UseHuntress(blessingToUse);
			}
			else if (blessingToUse.blessingName == "Ignite") {
				UseIgnite(blessingToUse);
			}
			else if (blessingToUse.blessingName == "Immunity") {
				UseImmunity(blessingToUse);
			}
			else if (blessingToUse.blessingName == "Recovery") {
				UseRecovery(blessingToUse);
			}
			else if (blessingToUse.blessingName == "Sandstorm") {
				UseSandstorm(blessingToUse);
			}
			else if (blessingToUse.blessingName == "Siphon") {
				UseSiphon(blessingToUse);
			}
			else if (blessingToUse.blessingName == "Solar Flare") {
				UseSolarFlare(blessingToUse);
			}
		}
	}
	
	private void UseBetrayal(Blessing blessingToUse) {
		ActiveBlessing_I activeBlessing = (ActiveBlessing_I)blessingToUse;

		Vector3 position = enemyUnitClusters.OrderByDescending(cluster => cluster.Item2).First().Item1.waypoint.transform.position;

		if (activeBlessing.canFire(p.rewiredPlayerKey, position, p.getFavor())) {
			p.spendFavor(activeBlessing.cost);
			stats.recordBlessingUse(PlayerIDs.player2, Array.IndexOf(blessingScripts, blessingToUse));

			QuipsManager.Instance.PlayBlessingUseQuip(Blessing.blessingID.betrayal, PlayerIDs.player2);

			if (activeBlessing.duration > 0) {
				blessingDurations.AddBlessingToQueue(activeBlessing.duration, activeBlessing.icon);
			}
		}
	}

	private void UseBattleRage(Blessing blessingToUse) {
		ActiveBlessing_I activeBlessing = (ActiveBlessing_I)blessingToUse;

		Vector3 position = allyUnitClusters.OrderByDescending(cluster => cluster.Item2).First().Item1.waypoint.transform.position;

		if (activeBlessing.canFire(p.rewiredPlayerKey, position, p.getFavor())) {
			p.spendFavor(activeBlessing.cost);
			stats.recordBlessingUse(PlayerIDs.player2, Array.IndexOf(blessingScripts, blessingToUse));

			QuipsManager.Instance.PlayBlessingUseQuip(Blessing.blessingID.battleRage, PlayerIDs.player2);

			if (activeBlessing.duration > 0) {
				blessingDurations.AddBlessingToQueue(activeBlessing.duration, activeBlessing.icon);
			}
		}
	}

	private void UseCyclone(Blessing blessingToUse) {
		ActiveBlessing_I activeBlessing = (ActiveBlessing_I)blessingToUse;

		Vector3 position = enemyUnitClusters.OrderByDescending(cluster => cluster.Item2).First().Item1.waypoint.transform.position;

		if (activeBlessing.canFire(p.rewiredPlayerKey, position, p.getFavor())) {
			p.spendFavor(activeBlessing.cost);
			stats.recordBlessingUse(PlayerIDs.player2, Array.IndexOf(blessingScripts, blessingToUse));

			if (activeBlessing.duration > 0) {
				blessingDurations.AddBlessingToQueue(activeBlessing.duration, activeBlessing.icon);
			}
		}
	}

	private void UseDecay(Blessing blessingToUse) {
		ActiveBlessing_I activeBlessing = (ActiveBlessing_I)blessingToUse;

		// use on position of enemy units closest to a group of allied units
		Vector3 position = enemyUnitClusters[0].Item1.waypoint.transform.position;
		float minDistance = float.MaxValue;
		for (int i = 0; i < allyUnitClusters.Count; i++) {
			for (int j = 0; j < enemyUnitClusters.Count; j++) {
				if (Vector3.Distance(allyUnitClusters[i].Item1.waypoint.transform.position,
						enemyUnitClusters[j].Item1.waypoint.transform.position) < minDistance) {
					minDistance = Vector3.Distance(allyUnitClusters[i].Item1.waypoint.transform.position,
						enemyUnitClusters[j].Item1.waypoint.transform.position);
					position = enemyUnitClusters[j].Item1.waypoint.transform.position;
				}
			}
		}

		//Vector3 position = positionBetweenClusters;

		if (activeBlessing.canFire(p.rewiredPlayerKey, position, p.getFavor())) {
			p.spendFavor(activeBlessing.cost);
			stats.recordBlessingUse(PlayerIDs.player2, Array.IndexOf(blessingScripts, blessingToUse));

			QuipsManager.Instance.PlayBlessingUseQuip(Blessing.blessingID.decay, PlayerIDs.player2);

			if (activeBlessing.duration > 0) {
				blessingDurations.AddBlessingToQueue(activeBlessing.duration, activeBlessing.icon);
			}
		}
	}

	private void UseEarthquake(Blessing blessingToUse) {
		GlobalBlessing_I globalBlessing = (GlobalBlessing_I)blessingToUse;

		if (p.getFavor() >= globalBlessing.cost) {
			p.spendFavor(globalBlessing.cost);
			globalBlessing.Fire();
			stats.recordBlessingUse(PlayerIDs.player2, Array.IndexOf(blessingScripts, blessingToUse));

			if (globalBlessing.duration > 0) {
				blessingDurations.AddBlessingToQueue(globalBlessing.duration, globalBlessing.icon);
			}
		}
	}

	private void UseEmbalming(Blessing blessingToUse) {
		UnitBlessing_I unitBlessing = (UnitBlessing_I)blessingToUse;

		if (unitBlessing.canFire(p.getFavor())) {
			p.spendFavor(unitBlessing.cost);
			p.SpawnBlessingUnit(unitBlessing.unitTypeToSpawn, allyUnitClusters.OrderByDescending(cluster => cluster.Item2).First().Item1.lane);
			stats.recordBlessingUse(PlayerIDs.player2, Array.IndexOf(blessingScripts, blessingToUse));

			QuipsManager.Instance.PlayBlessingUseQuip(Blessing.blessingID.embalming, PlayerIDs.player2);

			if (unitBlessing.duration > 0) {
				blessingDurations.AddBlessingToQueue(unitBlessing.duration, unitBlessing.icon);
			}
		}
	}

	private void UseEmpower(Blessing blessingToUse) {
		ActiveBlessing_I activeBlessing = (ActiveBlessing_I)blessingToUse;

		// use on position of allied units closest to a group of allied units
		Vector3 position = allyUnitClusters[0].Item1.waypoint.transform.position;
		float minDistance = float.MaxValue;
		for (int i = 0; i < allyUnitClusters.Count; i++) {
			for (int j = 0; j < enemyUnitClusters.Count; j++) {
				if (Vector3.Distance(allyUnitClusters[i].Item1.waypoint.transform.position,
						enemyUnitClusters[j].Item1.waypoint.transform.position) < minDistance) {
					minDistance = Vector3.Distance(allyUnitClusters[i].Item1.waypoint.transform.position,
						enemyUnitClusters[j].Item1.waypoint.transform.position);
					position = allyUnitClusters[j].Item1.waypoint.transform.position;
				}
			}
		}

		// Vector3 position = positionBetweenClusters;

		if (activeBlessing.canFire(p.rewiredPlayerKey, position, p.getFavor())) {
			p.spendFavor(activeBlessing.cost);
			stats.recordBlessingUse(PlayerIDs.player2, Array.IndexOf(blessingScripts, blessingToUse));

			QuipsManager.Instance.PlayBlessingUseQuip(Blessing.blessingID.empower, PlayerIDs.player2);

			if (activeBlessing.duration > 0) {
				blessingDurations.AddBlessingToQueue(activeBlessing.duration, activeBlessing.icon);
			}
		}
	}

	private void UseGrasp(Blessing blessingToUse) {
		ActiveBlessing_I activeBlessing = (ActiveBlessing_I)blessingToUse;

		// use on position of enemy towers closest to a group of allied units
		Vector3 position = enemyTowerClusters[0].Item2;
		float minDistance = graspMaxDistance;
		for (int i = 0; i < allyUnitClusters.Count; i++) {
			for (int j = 0; j < enemyTowerClusters.Count; j++) {
				if (Vector3.Distance(allyUnitClusters[i].Item1.waypoint.transform.position, enemyTowerClusters[j].Item2) < minDistance) {
					minDistance = Vector3.Distance(allyUnitClusters[i].Item1.waypoint.transform.position, enemyTowerClusters[j].Item2);
					position = enemyTowerClusters[j].Item2;
				}
			}
		}

		if (activeBlessing.canFire(p.rewiredPlayerKey, position, p.getFavor())) {
			p.spendFavor(activeBlessing.cost);
			stats.recordBlessingUse(PlayerIDs.player2, Array.IndexOf(blessingScripts, blessingToUse));

			QuipsManager.Instance.PlayBlessingUseQuip(Blessing.blessingID.grasp, PlayerIDs.player2);

			if (activeBlessing.duration > 0) {
				blessingDurations.AddBlessingToQueue(activeBlessing.duration, activeBlessing.icon);
			}
		}
	}

	private void UseHaste(Blessing blessingToUse) {
		BuffBlessing_I buffBlessing = (BuffBlessing_I)blessingToUse;

		if (buffBlessing.canFire(p.rewiredPlayerKey, p.getFavor())) {
			//buffBlessing.Fire();
			p.spendFavor(buffBlessing.cost);
			stats.recordBlessingUse(PlayerIDs.player2, Array.IndexOf(blessingScripts, blessingToUse));

			if (buffBlessing.duration > 0) {
				blessingDurations.AddBlessingToQueue(buffBlessing.duration, buffBlessing.icon);
			}
		}
	}

	private void UseHuntress(Blessing blessingToUse) {
		UnitBlessing_I unitBlessing = (UnitBlessing_I)blessingToUse;

		if (unitBlessing.canFire(p.getFavor())) {
			p.spendFavor(unitBlessing.cost);
			p.SpawnBlessingUnit(unitBlessing.unitTypeToSpawn, allyUnitClusters.OrderByDescending(cluster => cluster.Item2).First().Item1.lane);
			stats.recordBlessingUse(PlayerIDs.player2, Array.IndexOf(blessingScripts, blessingToUse));

			QuipsManager.Instance.PlayBlessingUseQuip(Blessing.blessingID.huntressSpawn, PlayerIDs.player2);

			if (unitBlessing.duration > 0) {
				blessingDurations.AddBlessingToQueue(unitBlessing.duration, unitBlessing.icon);
			}
		}
	}

	private void UseIgnite(Blessing blessingToUse) {
		ActiveBlessing_I activeBlessing = (ActiveBlessing_I)blessingToUse;

		Vector3 position = LivingTowerDictionary.dict[PlayerIDs.player1][UnityEngine.Random.Range(0, LivingTowerDictionary.dict[PlayerIDs.player1].Count)].transform.position;

		if (activeBlessing.canFire(p.rewiredPlayerKey, position, p.getFavor())) {
			p.spendFavor(activeBlessing.cost);
			stats.recordBlessingUse(PlayerIDs.player2, Array.IndexOf(blessingScripts, blessingToUse));

			if (activeBlessing.duration > 0) {
				blessingDurations.AddBlessingToQueue(activeBlessing.duration, activeBlessing.icon);
			}
		}
	}

	private void UseImmunity(Blessing blessingToUse) {
		BuffBlessing_I buffBlessing = (BuffBlessing_I)blessingToUse;

		if (buffBlessing.canFire(p.rewiredPlayerKey, p.getFavor())) {
			p.spendFavor(buffBlessing.cost);
			stats.recordBlessingUse(PlayerIDs.player2, Array.IndexOf(blessingScripts, blessingToUse));

			QuipsManager.Instance.PlayBlessingUseQuip(Blessing.blessingID.immunity, PlayerIDs.player2);

			if (buffBlessing.duration > 0) {
				blessingDurations.AddBlessingToQueue(buffBlessing.duration, buffBlessing.icon);
			}
		}
	}

	private void UseRecovery(Blessing blessingToUse) {
		BuffBlessing_I buffBlessing = (BuffBlessing_I)blessingToUse;

		if (buffBlessing.canFire(p.rewiredPlayerKey, p.getFavor())) {
			//buffBlessing.Fire();
			p.spendFavor(buffBlessing.cost);
			stats.recordBlessingUse(PlayerIDs.player2, Array.IndexOf(blessingScripts, blessingToUse));

			if (buffBlessing.duration > 0) {
				blessingDurations.AddBlessingToQueue(buffBlessing.duration, buffBlessing.icon);
			}
		}
	}

	private void UseSandstorm(Blessing blessingToUse) {
		LaneBlessing_I laneBlessing = (LaneBlessing_I)blessingToUse;

		laneBlessing.SetLane(greatestLane.ToString());
		laneBlessing.playerID = p.rewiredPlayerKey;

		if (laneBlessing.CanFire(p.getFavor())) {
			p.spendFavor(laneBlessing.cost);
			laneBlessing.Fire();
			stats.recordBlessingUse(PlayerIDs.player2, Array.IndexOf(blessingScripts, blessingToUse));

			QuipsManager.Instance.PlayBlessingUseQuip(Blessing.blessingID.sandstorm, PlayerIDs.player2);

			if (laneBlessing.duration > 0) {
				blessingDurations.AddBlessingToQueue(laneBlessing.duration, laneBlessing.icon);
			}
		}
	}

	private void UseSiphon(Blessing blessingToUse) {
		ActiveBlessing_I activeBlessing = (ActiveBlessing_I)blessingToUse;

		Vector3 position = allyUnitClusters.OrderByDescending(cluster => cluster.Item2).First().Item1.waypoint.transform.position;

		if (activeBlessing.canFire(p.rewiredPlayerKey, position, p.getFavor())) {
			p.spendFavor(activeBlessing.cost);
			stats.recordBlessingUse(PlayerIDs.player2, Array.IndexOf(blessingScripts, blessingToUse));

			if (activeBlessing.duration > 0) {
				blessingDurations.AddBlessingToQueue(activeBlessing.duration, activeBlessing.icon);
			}
		}
	}

	private void UseSolarFlare(Blessing blessingToUse) {
		ActiveBlessing_MultiTarget_I multiTargetActiveBlessing = (ActiveBlessing_MultiTarget_I)blessingToUse;

		if (p.getFavor() >= multiTargetActiveBlessing.cost && !multiTargetActiveBlessing.isOnCd) {
			Vector3 position1 = enemyUnitClusters.OrderByDescending(cluster => cluster.Item2).First().Item1.waypoint.transform.position;
			Vector3 position2;

			enemyUnitClusters.RemoveAt(0);
			if (enemyUnitClusters.Count > 0 || enemyTowerClusters.Count > 0) {
				enemyUnitClusters.OrderByDescending(cluster => Vector3.Distance(position1, cluster.Item1.waypoint.transform.position));
				enemyTowerClusters.OrderByDescending(cluster => Vector3.Distance(position1, cluster.Item2));

				if (Vector3.Distance(enemyUnitClusters[0].Item1.waypoint.transform.position, position1) <= solarFlareMaxDistance) {
					position2 = enemyUnitClusters[0].Item1.waypoint.transform.position;
				}
				else if (enemyTowerClusters.Count > 0 && Vector3.Distance(enemyTowerClusters[0].Item2, position1) <= solarFlareMaxDistance) {
					position2 = enemyTowerClusters[0].Item2;
				}
				else {
					position2 = position1;
				}
			}
			else {
				position2 = position1;
			}

			multiTargetActiveBlessing.AddTarget(position1);
			multiTargetActiveBlessing.AddTarget(position2);

			p.spendFavor(multiTargetActiveBlessing.cost);
			multiTargetActiveBlessing.Fire();
			stats.recordBlessingUse(PlayerIDs.player2, Array.IndexOf(blessingScripts, blessingToUse));

			QuipsManager.Instance.PlayBlessingUseQuip(Blessing.blessingID.solarFlare, PlayerIDs.player2);

			if (multiTargetActiveBlessing.duration > 0) {
				blessingDurations.AddBlessingToQueue(multiTargetActiveBlessing.duration, multiTargetActiveBlessing.icon);
			}
		}
	}
	#endregion

	#region Condition Checks
	private Constants.radialCodes CheckAlliesInLaneCondition() {
		Dictionary<Constants.radialCodes, int> alliesInLane = new Dictionary<Constants.radialCodes, int>();
		alliesInLane.Add(Constants.radialCodes.top,
			LivingUnitDictionary.dict[PlayerIDs.player2].FindAll(u => u.GetComponent<UnitMovement>().lane == "top").Count);
		alliesInLane.Add(Constants.radialCodes.mid,
			LivingUnitDictionary.dict[PlayerIDs.player2].FindAll(u => u.GetComponent<UnitMovement>().lane == "mid").Count);
		alliesInLane.Add(Constants.radialCodes.bot,
			LivingUnitDictionary.dict[PlayerIDs.player2].FindAll(u => u.GetComponent<UnitMovement>().lane == "bot").Count);

		if (alliesInLane[Constants.radialCodes.top] > 0 ||
			alliesInLane[Constants.radialCodes.mid] > 0 ||
			alliesInLane[Constants.radialCodes.bot] > 0) {

			checkOffTags(blessingKeywords.alliesInLane);
			// This is some black magic shit, Idk how it works but I found it on stackoverflow so its gotta be right
			return alliesInLane.Aggregate((x, y) => x.Value > y.Value ? x : y).Key;
		}

		return Constants.radialCodes.none;
	}

	private void CheckUnitClusterCondition(ref List<(LaneGraphNode, int)> unitClusterList, string playerId) {
		unitClusterList.AddRange(
			p.threatDetector.GetLaneGraph(Constants.radialCodes.top).FindUnitClusters(playerId, clusterRadius, unitClusterCount));
		unitClusterList.AddRange(
			p.threatDetector.GetLaneGraph(Constants.radialCodes.bot).FindUnitClusters(playerId, clusterRadius, unitClusterCount));

		if (p.getCanSpawnInMid()) {
			unitClusterList.AddRange(
				p.threatDetector.GetLaneGraph(Constants.radialCodes.mid).FindUnitClusters(playerId, clusterRadius, unitClusterCount));
		}

		if (unitClusterList.Count != 0) {
			if (playerId == PlayerIDs.player2) {
				checkOffTags(blessingKeywords.allyUnitCluster);
			}
			else {
				checkOffTags(blessingKeywords.enemyUnitCluster);
			}
		}
	}

	private void CheckUnitsWithinDistanceCondition(List<(LaneGraphNode, int)> allyUnitClusters, List<(LaneGraphNode, int)> enemyUnitClusters, ref Vector3 positionBetweenClusters) {
		List<(Vector3, int)> validLocations = new List<(Vector3, int)>();
		for (int i = 0; i < allyUnitClusters.Count; i++) {
			for (int j = 0; j < enemyUnitClusters.Count; j++) {
				if (Vector3.Distance(allyUnitClusters[i].Item1.waypoint.transform.position,
						enemyUnitClusters[j].Item1.waypoint.transform.position) <= distanceBetweenClusters) {
					Vector3 averagePosition = (allyUnitClusters[i].Item1.waypoint.transform.position
						+ enemyUnitClusters[j].Item1.waypoint.transform.position) / 2;
					validLocations.Add((averagePosition, allyUnitClusters[i].Item2 + enemyUnitClusters[j].Item2));
				}
			}
		}

		if (validLocations.Count > 0) {
			checkOffTags(blessingKeywords.withinDistance);

			int greatestNumber = 0;
			int indexOfGreatest = 0;
			for (int i = 0; i < validLocations.Count; i++) {
				if (validLocations[i].Item2 > greatestNumber) {
					indexOfGreatest = i;
					greatestNumber = validLocations[i].Item2;
				}
			}
			positionBetweenClusters = validLocations[indexOfGreatest].Item1;
		}
	}
	
	private void CheckTowerClusterCondition(ref List<(List<Collider>, Vector3)> towerClusterList, string playerId) {
		LayerMask towerLayer = 1 << LayerMask.NameToLayer("Tower");

		foreach (GameObject t in LivingTowerDictionary.dict[playerId]) {
			List<Collider> towers = Physics.OverlapSphere(t.transform.position, clusterRadius, towerLayer).ToList();
			towers.RemoveAll(tower => tower.gameObject.GetComponent<TowerState>().rewiredPlayerKey != playerId);
			int newCount = towers.Count;
			if (newCount >= towerClusterCount) {
				towerClusterList.Add((towers, t.transform.position));
			}
		}

		if (towerClusterList.Count > 0) {
			if (playerId == PlayerIDs.player2) {
				checkOffTags(blessingKeywords.allyTowerCluster);
			}
			else {
				checkOffTags(blessingKeywords.enemyTowerCluster);
			}
		}
	}
	#endregion

	#region Tag Management
	private void checkOffTags(blessingKeywords tag) {
		foreach (BlessingTagChecker tagChecker in tagCheckers) {
			tagChecker.MarkCondition(tag);
		}
	}

	private void resetTags() {
		foreach (BlessingTagChecker tagChecker in tagCheckers) {
			tagChecker.ResetDict();
		}
	}
	#endregion
}
