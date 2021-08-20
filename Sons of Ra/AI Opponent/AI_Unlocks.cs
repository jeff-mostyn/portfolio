using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class AI_Unlocks : MonoBehaviour
{
	public enum unlockItems { archer = 2, shieldbearer = 3, stasis = 9, obelisk = 10, sun = 11 };

	[Header("Tracker Values")]
	[SerializeField] private float enemyArcherPercent;
	[SerializeField] private float enemySpearmanPercent;
	[SerializeField] private float enemyShieldbearerPercent;
	[SerializeField] private float enemyCatapultPercent;
	[SerializeField] private float enemyTowerPercent;

	[Header("Trackers")]
	private float archerUnlockTracker = 0;
	private float shieldUnlockTracker = 0;

	private void Awake() {
		SonsOfRa.Events.GameEvents.UnitSpawn += UpdateTrackerOnUnitSpawn;
		SonsOfRa.Events.GameEvents.TowerSpawn += UpdateTrackerOnTowerSpawn;
	}

	public void UpdateTrackerOnUnitSpawn(UnitAI ai) {
		if (ai.GetTeamPlayerKey() == PlayerIDs.player1) {
			if (ai.type == Constants.unitType.archer && shieldUnlockTracker < float.MaxValue) {
				shieldUnlockTracker += enemyArcherPercent;
			}
			else if (ai.type == Constants.unitType.catapult && archerUnlockTracker < float.MaxValue) {
				archerUnlockTracker += enemyCatapultPercent;
			}
			else if (ai.type == Constants.unitType.spearman && archerUnlockTracker < float.MaxValue) {
				archerUnlockTracker += enemySpearmanPercent;
			}
			else if (ai.type == Constants.unitType.shieldbearer && archerUnlockTracker > float.MinValue) {
				archerUnlockTracker -= enemyShieldbearerPercent;
			}
		}
	}

	public void UpdateTrackerOnTowerSpawn(GameObject tower) {
		if (tower.GetComponent<TowerState>().rewiredPlayerKey == PlayerIDs.player1 && shieldUnlockTracker < float.MaxValue) {
			shieldUnlockTracker += enemyTowerPercent;
		}
	}

	public float GetArcherEvaluation() {
		return archerUnlockTracker;
	}

	public float GetShieldEvaluation() {
		return shieldUnlockTracker;
	}

	private void OnDestroy() {
		SonsOfRa.Events.GameEvents.UnitSpawn += UpdateTrackerOnUnitSpawn;
		SonsOfRa.Events.GameEvents.TowerSpawn += UpdateTrackerOnTowerSpawn;
	}
}
