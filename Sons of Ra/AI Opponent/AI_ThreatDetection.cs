using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class AI_ThreatDetection : MonoBehaviour
{
	// Unit Graph
	[SerializeField] private Dictionary<Constants.radialCodes, LaneGraph> laneGraphs;

	// Lane Unit Counts
	public Dictionary<Constants.radialCodes, int> UnitLaneCounts;

	// Threat Detection
	[SerializeField] private float towerSearchRadius;
	[SerializeField] private GameObject triggerZoneObj;
	private AI_TriggerZone topTrigger, midTrigger, botTrigger;

	// Start is called before the first frame update
	void Start()
    {
		laneGraphs = new Dictionary<Constants.radialCodes, LaneGraph> {
			[Constants.radialCodes.top] = new LaneGraph(TopWaypoints.points, Constants.radialCodes.top),
			[Constants.radialCodes.bot] = new LaneGraph(BotWaypoints.points, Constants.radialCodes.bot)
		};
		topTrigger = Instantiate(triggerZoneObj, TopWaypoints.points[TopWaypoints.points.Length - 2].transform.position, Quaternion.identity).GetComponent<AI_TriggerZone>();
		topTrigger.SetUp(GetComponent<AI_PlayerController>(), Constants.radialCodes.top);
		botTrigger = Instantiate(triggerZoneObj, BotWaypoints.points[BotWaypoints.points.Length - 2].transform.position, Quaternion.identity).GetComponent<AI_TriggerZone>();
		botTrigger.SetUp(GetComponent<AI_PlayerController>(), Constants.radialCodes.bot);

		if (canSpawnInMid()) {
			laneGraphs.Add(Constants.radialCodes.mid, new LaneGraph(MidWaypoints.points, Constants.radialCodes.mid));
			midTrigger = Instantiate(triggerZoneObj, MidWaypoints.points[MidWaypoints.points.Length - 2].transform.position, Quaternion.identity).GetComponent<AI_TriggerZone>();
			midTrigger.SetUp(GetComponent<AI_PlayerController>(), Constants.radialCodes.mid);
		}
	}

	public float[] CalculateLaneThreats() {
		float[] threats = { 0f, 0f, 0f };
		UnitLaneCounts = new Dictionary<Constants.radialCodes, int>() {
			{Constants.radialCodes.top, 0},
			{Constants.radialCodes.bot, 0}
		};
		if (canSpawnInMid()) {
			UnitLaneCounts.Add(Constants.radialCodes.mid, 0);
		}

		foreach (GameObject unit in LivingUnitDictionary.dict[PlayerIDs.player1]) {
			UnitLaneCounts[CalculateUnitThreat(unit, ref threats, false)] -= 1;
		}
		foreach (GameObject unit in LivingUnitDictionary.dict[PlayerIDs.player2]) {
			UnitLaneCounts[CalculateUnitThreat(unit, ref threats, true)] += 1;
		}

		return threats;
	} 

	public int[] GetLaneTowerThreats() {
		int[] threats = { 0, 0, 0 };

		threats[(int)Constants.radialCodes.top] = laneGraphs[Constants.radialCodes.top].GetTowerThreat(PlayerIDs.player1, towerSearchRadius);
		threats[(int)Constants.radialCodes.bot] = laneGraphs[Constants.radialCodes.bot].GetTowerThreat(PlayerIDs.player1, towerSearchRadius);
		if (canSpawnInMid()) {
			threats[(int)Constants.radialCodes.mid] = laneGraphs[Constants.radialCodes.mid].GetTowerThreat(PlayerIDs.player1, towerSearchRadius);
		}

		return threats;
	}

	private Constants.radialCodes CalculateUnitThreat(GameObject unit, ref float[] threats, bool ally) {
		UnitAI uAI = unit.GetComponent<UnitAI>();
		UnitMovement uMove = unit.GetComponent<UnitMovement>();
		string lane = uMove.lane; // find unit's lane
		int waypoint = uMove.GetWaypointIndex(); // find unit's target waypoint

		// determine code of unit's lane
		Constants.radialCodes laneCode;
		if (lane == "top") {
			laneCode = Constants.radialCodes.top;
		}
		else if (lane == "mid") {
			laneCode = Constants.radialCodes.mid;
		}
		else {
			laneCode = Constants.radialCodes.bot;
		}

		// fetch lane graph of unit's lane
		LaneGraph unitLaneGraph = laneGraphs[laneCode];

		// calculate distance with graph of unit's lane, starting with unit's target waypoint
		float distance = 0;
		distance = unitLaneGraph.GetDistanceFromEnd(waypoint);

		// multiply unit's threat value by 1 - (distanceFromKeep/laneLengths)
		float unitThreatValue = (uAI.threatValue * uAI.GetThreatModifier()) * (1 - (distance / unitLaneGraph.GetLaneDistance()));

		// increment lane threat by unit's modified threat value
		if (!ally) {
			threats[(int)laneCode] += Mathf.Max(unitThreatValue, 0.25f);
		}
		else {
			threats[(int)laneCode] -= unitThreatValue;
		}

		return laneCode;
	}

	public LaneGraph GetLaneGraph(Constants.radialCodes laneCode) {
		return laneGraphs[laneCode];
	}

	private bool canSpawnInMid() {
		return GameManager.Instance.player2Controller.getCanSpawnInMid();
	}
}
