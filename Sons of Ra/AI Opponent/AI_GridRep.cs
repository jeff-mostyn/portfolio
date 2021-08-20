using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

public class AI_GridRep {
	private AI_PlayerController player;
	private List<List<AI_TileRep>> gridRep;

	public AI_GridRep(AI_PlayerController p, Squares mapGrid) {
		gridRep = new List<List<AI_TileRep>>();
		List<List<STile>> tempGrid = mapGrid.getGrid();

		for (int i=0; i<tempGrid.Count; i++) {
			gridRep.Add(new List<AI_TileRep>());
			for (int j = 0; j<tempGrid[i].Count; j++) {
				if (j > 0) {
					gridRep[i].Add(new AI_TileRep(p, this, i, j, tempGrid[i][j], gridRep[i][gridRep[i].Count - 1]));
				}
				else {
					gridRep[i].Add(new AI_TileRep(p, this, i, j, tempGrid[i][j], null));
				}
			}
		}

		UpdateTiles();

		player = p;
	}

	public void UpdateTiles() {
		for (int i = 0; i < gridRep.Count; i++) {
			for (int j = 0; j < gridRep[i].Count; j++) {
				gridRep[i][j].UpdateTile();
			}
		}
	}

	public bool IsInBounds(int x, int y) {
		if (x >= 0 && x < gridRep.Count
			&& y >= 0 && y < gridRep[x].Count) {
			return true;
		}
		else {
			return false;
		}
	}

	#region Fetching Tiles
	public AI_TileRep GetTile(int x, int y) {
		return gridRep[x][y];
	}
	
	/// <summary>
	/// Assemble list of placeable tiles that are closest to a given lane
	/// </summary>
	/// <param name="laneCode">The lane whose nearest tiles will be returned</param>
	/// <returns></returns>
	public List<AI_TileRep> GetPlaceableTilesInLane(Constants.radialCodes laneCode) {
		List<AI_TileRep> flatGrid = gridRep.SelectMany(t => t).ToList();
		List<AI_TileRep> laneTiles = flatGrid.FindAll(t => t.GetClosestLane() == laneCode && t.GetCanPlace());

		return laneTiles;
	}

	/// <summary>
	/// Returns the closest placeable tile to a given location
	/// </summary>
	/// <param name="location">Position for which a proximate tile is being searched</param>
	/// <param name="laneCode">The lane to be searched near. Reduces number of calculations needed</param>
	/// <returns></returns>
	public AI_TileRep GetClosestPlaceableTile(Vector3 location, Constants.radialCodes laneCode) {
		List<AI_TileRep> tilesInLane = GetPlaceableTilesInLane(laneCode);

		AI_TileRep closestPlaceableTile = null;
		float closestSquaredDistance = Mathf.Infinity;

		foreach (AI_TileRep potentialTarget in tilesInLane) {
			Vector3 directionToTarget = potentialTarget.GetTileObject().transform.position - location;
			float squaredDistance = directionToTarget.sqrMagnitude;

			if (squaredDistance < closestSquaredDistance) {
				closestSquaredDistance = squaredDistance;
				closestPlaceableTile = potentialTarget;
			}
		}

		return closestPlaceableTile;
	}
	#endregion
}
