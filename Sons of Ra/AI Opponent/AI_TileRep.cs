using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class AI_TileRep {
	// AI Player ref
	private AI_PlayerController player;
	private AI_GridRep grid;

	// properties
	private int horzPos, vertPos;
	private GameObject gridSquare;
	private STile tileScript;
	private string ownerId;
	private Constants.radialCodes closestLane;
	private bool canPlace;
	private bool canPlaceObelisk;
	private bool canPlaceSunTowerVert, canPlaceSunTowerHoriz;

	public AI_TileRep(AI_PlayerController p, AI_GridRep g, int x, int y, STile tile, AI_TileRep prev) {
		player = p;
		grid = g;
		horzPos = x;
		vertPos = y;
		gridSquare = tile.gameObject;
		tileScript = tile;
		ownerId = tile.GetOwnerID();
		canPlace = tile.isSpawnable(p.rewiredPlayerKey);
		canPlaceObelisk = false;
		canPlaceSunTowerHoriz = false;
		canPlaceSunTowerVert = false;
		if (player.getCanSpawnInMid()) {
			determineClosestLaneOfThree(prev);
		}
		else {
			determineClosestLaneOfTwo();
		}
	}

	public void UpdateTile() {
		canPlace = tileScript.isSpawnable(player.rewiredPlayerKey);
		canPlaceObelisk = CanPlaceObelisk();
		canPlaceSunTowerHoriz = CanPlaceSunTowerHoriz();
		canPlaceSunTowerVert = CanPlaceSunTowerVert();
	}

	#region Capability Functions
	private bool CanPlaceObelisk() {
		if (tileScript.gameObject.activeSelf && tileScript.isSpawnable(player.rewiredPlayerKey)
			&& grid.IsInBounds(horzPos + 1, vertPos)
			&& grid.IsInBounds(horzPos, vertPos + 1)
			&& grid.IsInBounds(horzPos + 1, vertPos + 1)
			&& grid.GetTile(horzPos + 1, vertPos).GetTile().gameObject.activeSelf
			&& grid.GetTile(horzPos + 1, vertPos).GetTile().isSpawnable(player.rewiredPlayerKey)
			&& grid.GetTile(horzPos, vertPos + 1).GetTile().gameObject.activeSelf
			&& grid.GetTile(horzPos, vertPos + 1).GetTile().isSpawnable(player.rewiredPlayerKey)
			&& grid.GetTile(horzPos + 1, vertPos + 1).GetTile().gameObject.activeSelf
			&& grid.GetTile(horzPos + 1, vertPos + 1).GetTile().isSpawnable(player.rewiredPlayerKey)) {
			return true;
		}
		else {
			return false;
		}
	}

	private bool CanPlaceSunTowerHoriz() {
		if (tileScript.gameObject.activeSelf && tileScript.isSpawnable(player.rewiredPlayerKey)
			&& grid.IsInBounds(horzPos + 1, vertPos) 
			&& grid.IsInBounds(horzPos - 1, vertPos)
			&& grid.GetTile(horzPos + 1, vertPos).GetTile().gameObject.activeSelf 
			&& grid.GetTile(horzPos + 1, vertPos).GetTile().isSpawnable(player.rewiredPlayerKey)
			&& grid.GetTile(horzPos - 1, vertPos).GetTile().gameObject.activeSelf
			&& grid.GetTile(horzPos - 1, vertPos).GetTile().isSpawnable(player.rewiredPlayerKey)) {
			return true;
		}
		else {
			return false;
		}
	}

	private bool CanPlaceSunTowerVert() {
		if (tileScript.gameObject.activeSelf && tileScript.isSpawnable(player.rewiredPlayerKey)
			&& grid.IsInBounds(horzPos, vertPos + 1)
			&& grid.IsInBounds(horzPos, vertPos - 1)
			&& grid.GetTile(horzPos, vertPos + 1).GetTile().gameObject.activeSelf
			&& grid.GetTile(horzPos, vertPos + 1).GetTile().isSpawnable(player.rewiredPlayerKey)
			&& grid.GetTile(horzPos, vertPos - 1).GetTile().gameObject.activeSelf
			&& grid.GetTile(horzPos, vertPos - 1).GetTile().isSpawnable(player.rewiredPlayerKey)) {
			return true;
		}
		else {
			return false;
		}
	}
	#endregion

	#region Positional Functions
	/// <summary>
	/// Sets the closestLane value to the enumeration for the lane the tile in question is closest to, on 3 lane maps
	/// </summary>
	/// <param name="prev">The most recently set-up tile, adjacent and above this one</param>
	private void determineClosestLaneOfThree(AI_TileRep prev) {
		if (tileScript.gameObject.activeSelf && !tileScript.isRoad) {
			if (gridSquare.transform.position.x < 0) {	// look at mid and top lanes
				if (prev != null && prev.GetClosestLane() == Constants.radialCodes.mid) {
					//gridSquare.GetComponent<MeshRenderer>().material.color = new Color(0f, 1f, 1f, .15f);
					closestLane = Constants.radialCodes.mid;
				}
				else if (determineClosestWaypointInLane(MidWaypoints.points) < determineClosestWaypointInLane(TopWaypoints.points)) {
					//gridSquare.GetComponent<MeshRenderer>().material.color = new Color(0f, 1f, 1f, .15f);
					closestLane = Constants.radialCodes.mid;
				}
				else {
					//gridSquare.GetComponent<MeshRenderer>().material.color = new Color(1f, 1f, 0f, .15f);
					closestLane = Constants.radialCodes.top;
				}
			}
			else {	// look at mid and bottom lanes
				if (prev != null && prev.GetClosestLane() == Constants.radialCodes.bot) {
					//gridSquare.GetComponent<MeshRenderer>().material.color = new Color(1f, 0f, 1f, .15f);
					closestLane = Constants.radialCodes.bot;
				}
				else if (determineClosestWaypointInLane(BotWaypoints.points) < determineClosestWaypointInLane(MidWaypoints.points)) {
					//gridSquare.GetComponent<MeshRenderer>().material.color = new Color(1f, 0f, 1f, .15f);
					closestLane = Constants.radialCodes.bot;
				}
				else {
					//gridSquare.GetComponent<MeshRenderer>().material.color = new Color(0f, 1f, 1f, .15f);
					closestLane = Constants.radialCodes.mid;
				}
			}
		}
		else {
			closestLane = Constants.radialCodes.none;
		}
	}

	/// <summary>
	/// Sets the closestLane value to the enumeration for the lane the tile in question is closest to, on 2 lane maps
	/// </summary>
	private void determineClosestLaneOfTwo() {
		if (tileScript.gameObject.activeSelf && !tileScript.isRoad) {
			if (determineClosestWaypointInLane(BotWaypoints.points) < determineClosestWaypointInLane(TopWaypoints.points)) {
				closestLane = Constants.radialCodes.bot;
			}
			else {
				closestLane = Constants.radialCodes.top;
			}
		}
		else {
			closestLane = Constants.radialCodes.none;
		}
	}

	private float determineClosestWaypointInLane(Transform[] waypoints) {
		float smallestDist = float.MaxValue;
		for (int i=0; i<waypoints.Length; i++) {
			float calculatedDist = Vector3.Distance(gridSquare.transform.position, waypoints[i].transform.position);
			if (calculatedDist < smallestDist) {
				smallestDist = calculatedDist;
			}
		}
		return smallestDist;
	}
	#endregion

	#region Accessors
	public Constants.radialCodes GetClosestLane() {
		return closestLane;
	}

	public int[] GetGridIndices() {
		int[] location = new int[] { horzPos, vertPos };
		return location;
	}

	public bool GetCanPlace() {
		return canPlace;
	}

	public bool GetCanPlaceObelisk() {
		return canPlaceObelisk;
	}

	public bool GetCanPlaceSunTowerHoriz() {
		return canPlaceSunTowerHoriz;
	}

	public bool GetCanPlaceSunTowerVert() {
		return canPlaceSunTowerVert;
	}

	private STile GetTile() {
		return tileScript;
	}

	public GameObject GetTileObject() {
		return gridSquare;
	}
	#endregion
}
