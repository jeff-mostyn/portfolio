using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

public class LaneGraph
{
	private LaneGraphNode first;
	private LaneGraphNode last;
	public int length { get; }

    public LaneGraph(Transform[] waypoints, Constants.radialCodes _lane) {
		first = new LaneGraphNode(waypoints[waypoints.Length - 1].gameObject, null, _lane);
		length = 1;

		LaneGraphNode current = first;
		for (int i = waypoints.Length - 2; i >= 0; i--) {
			current.next = new LaneGraphNode(waypoints[i].gameObject, current, _lane);
			current = current.next;
			length++;
		}

		last = current;
	}

	public LaneGraphNode GetAt(int index) {
		LaneGraphNode current = first;

		if (index >= 0 && index < length) {
			for (int i = 0; i <= index; i++) {
				if (i == index) {
					return current;
				}
				else {
					current = current.next;
				}
			}
		}

		return null;
	}

	/// <summary>
	/// Return lane graph node closest to the largest cluster of units. Between
	/// groups of equal size, the closest is returned.
	/// </summary>
	/// <param name="targetId">Which player's units are being searched for</param>
	/// <param name="searchRadius">How far from a lane graph node units should be considered</param>
	/// <returns></returns>
	public LaneGraphNode FindUnitCluster(string targetId, float searchRadius, float minimumCount = 0) {
		LaneGraphNode closestToCluster = first;
		int highestCount = 0;
		LayerMask unitLayer = 1 << LayerMask.NameToLayer("Unit");

		for (LaneGraphNode current = first; current.next != null; current = current.next) {
			List<Collider> units = Physics.OverlapSphere(current.waypoint.transform.position, searchRadius, unitLayer).ToList();
			units.RemoveAll(u => u.gameObject.GetComponent<UnitAI>().GetTeamPlayerKey() != targetId);
			int newCount = units.Count;
			if (newCount > highestCount && newCount > minimumCount) {
				highestCount = newCount;
				closestToCluster = current;
			}
		}

		return closestToCluster;
	}

	public List<(LaneGraphNode, int)> FindUnitClusters(string targetId, float searchRadius, float minimumCount = 0) {
		List<(LaneGraphNode, int)> clusterLocations = new List<(LaneGraphNode, int)>();
		LayerMask unitLayer = 1 << LayerMask.NameToLayer("Unit");

		for (LaneGraphNode current = first; current.next != null; current = current.next) {
			List<Collider> units = Physics.OverlapSphere(current.waypoint.transform.position, searchRadius, unitLayer).ToList();
			units.RemoveAll(u => u.gameObject.GetComponent<UnitAI>().GetTeamPlayerKey() != targetId);
			int unitCount = units.Count;
			if (unitCount >= minimumCount) {
				clusterLocations.Add((current, unitCount));
			}
		}

		return clusterLocations;
	}

	public int GetTowerThreat(string targetId, float searchRadius) {
		int hitCount = 0;
		LayerMask towerLayer = 1 << LayerMask.NameToLayer("Tower");
		List<Collider> allTowersSoFar = new List<Collider>();

		for (LaneGraphNode current = first; current.next != null; current = current.next) {
			List<Collider> towers = Physics.OverlapSphere(current.waypoint.transform.position, searchRadius, towerLayer).ToList();
			towers.RemoveAll(t => t.gameObject.GetComponent<TowerState>().rewiredPlayerKey != targetId);
			towers.RemoveAll(t => allTowersSoFar.Contains(t));
			foreach(Collider col in towers) {
				allTowersSoFar.Add(col);
			}
			hitCount += towers.Count;
		}

		return hitCount;
	}

	#region Distance Calculation
	public float GetLaneDistance() {
		float distance = 0;

		for (LaneGraphNode current = first; current.next != null; current = current.next) {
			distance += current.GetDistanceToNext();
		}

		return distance;
	}

	public float GetDistanceFromEnd(int unitIndex) {
		int graphIndex = length - unitIndex - 1;
		LaneGraphNode nodeAtIndex = GetAt(graphIndex);
		float distance = 0;

		if (nodeAtIndex != null) {
			for (LaneGraphNode current = nodeAtIndex; current.previous != null; current = current.previous) {
				distance += current.GetDistanceToPrevious();
			}
		}

		return distance;
	}
	#endregion
}
