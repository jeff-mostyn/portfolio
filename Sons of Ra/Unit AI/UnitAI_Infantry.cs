using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using System.Linq;

public abstract class UnitAI_Infantry : UnitAI
{
	[Header("Infantry FX Locators")]
	public Transform headLocator;
	public List<Transform> weaponLocators; 
	// enemy variables
	protected UnitAI enemyAI;
	protected List<UnitAI> enemiesInRange;

    protected override void UpdateAttacking() {
        if (enemy != null && SettingsManager.Instance.GetIsOnline() && teamPlayerKey == OnlineManager.Instance.fakePlayerKey) {
            TargetSync(enemy.GetComponent<EntityIdentifier>().ID);
        }

        base.UpdateAttacking();
    }

    #region Detection Functions
    // if enemy unit enters range of unit and unit is not engaged with it, act upon that input
    protected override void OnTriggerEnter(Collider other) {
		if (other.gameObject.tag == enemyUnitTag) {
			UnitAI ai = other.gameObject.GetComponent<UnitAI>();

			// detect shieldbearer and attack it if it has aggro space
			if (ai.getType() == Constants.unitType.shieldbearer && !aggroed) {
				UnitAI_Shieldbearer shield = other.gameObject.GetComponent<UnitAI_Shieldbearer>();
				if (shield.canAggro()) {
					enemiesInRange.Insert(0, ai);
					enemyAI = ai;
					enemy = ai.self;
					shield.drawAggro(this);
					aggroTarget = shield;
				}
				else {
					enemiesInRange.Add(ai);
				}
			}
			else {
				enemiesInRange.Add(ai);
			}
		}
	}

	// If the "enemy" unit leaves unit's range, do not remain engaged with it
	protected override void OnTriggerExit(Collider other) {
		enemiesInRange.Remove(other.gameObject.GetComponent<UnitAI>());
		if (other.gameObject == enemy && state != unitStates.stunned && state != unitStates.suspended) {
			toWalking();
		}
	}

	protected override void CheckForTarget() {
		if (enemiesInRange.Count != 0 && enemy == null) {
			enemiesInRange.RemoveAll(e => e == null);
			for (int i = 0; i < enemiesInRange.Count; i++) {
				if (enemiesInRange[i] != null) {
					enemy = enemiesInRange[i].self;
					enemyAI = enemiesInRange[i];

					// next enemy is a shieldbearer
					if (enemyAI.type == Constants.unitType.shieldbearer) {
						UnitAI_Shieldbearer shield = enemy.GetComponent<UnitAI_Shieldbearer>();
						// this shieldbearer has room to draw aggro
						if (shield.canAggro()) {
							shield.drawAggro(this);
							aggroTarget = shield;
							aggroed = true;
						}
					}
				}
			}
		}
	}

	public override void RefreshTargets() {
		enemiesInRange.Clear();

		DetectTargets();
	}

	private void DetectTargets() {
		int unitLayer = 1 << LayerMask.NameToLayer("Unit");
		List<Collider> unitsList = new List<Collider>();
		Collider[] units;
		GameObject unit;

		// find towers in range, get list of all enemy towers
		units = Physics.OverlapSphere(transform.position, range/* / 10*/, unitLayer);
		unitsList = units.ToList();
		unitsList.RemoveAll(x => x.gameObject.GetComponent<UnitAI>().GetTeamPlayerKey() == teamPlayerKey);

		// go through towers in list, make sure to add new ones to enemy towers in range
		for (int i = 0; i < unitsList.Count; i++) {
			unit = unitsList[i].gameObject;
			UnitAI ai = unit.GetComponent<UnitAI>();
			if (!enemiesInRange.Exists(x => x == unit)) {
				enemiesInRange.Add(ai);
			}
		}
	}
	#endregion

	#region Effect Functions
	// win/lose animations at the end of the game
	public override void MyTeamWon() {
		unitAni.SetTrigger("winGame"); //doesn't work on catapults but doesn't throw an error
		state = unitStates.stunned;
		agent.enabled = false;
		gameObject.GetComponent<UnityEngine.AI.NavMeshObstacle>().enabled = true;
		stunDuration = 5f;
		gameEnded = true;
	}

	public override void MyTeamLost() {
		unitAni.SetTrigger("loseGame"); //doesn't work on catapults but doesn't throw an error
		state = unitStates.stunned;
		agent.enabled = false;
		gameObject.GetComponent<UnityEngine.AI.NavMeshObstacle>().enabled = true;
		stunDuration = 5f;
		gameEnded = true;
	}

	#endregion

	#region Getters and Setters
	public override void SetTeamPlayerKey(string ID) {
		base.SetTeamPlayerKey(ID);

		if (state == unitStates.attacking && enemyAI.GetTeamPlayerKey() == teamPlayerKey) {
			toWalking();
		}
	}
	#endregion

    public override void Sync(int _enemyEntityID, float[] _position, float _armor, int _blockStacks, float _moveSpeedMod, float _attackSpeedMod, float _damageMod, float _health, float _shield, int _waypointIndex) {
        TargetSync(_enemyEntityID);

        base.Sync(_enemyEntityID, _position, _armor, _blockStacks, _moveSpeedMod, _attackSpeedMod, _damageMod, _health, _shield, _waypointIndex);
    }

    public void TargetSync(int _enemyEntityID) {
        if (_enemyEntityID != -1) {
			try {
				enemy = LivingUnitDictionary.dict[teamPlayerKey == PlayerIDs.player1 ? PlayerIDs.player2 : PlayerIDs.player1].Find(e => e.GetComponent<EntityIdentifier>().ID == _enemyEntityID);

				if (enemy != null) {
					enemyAI = enemy.GetComponent<UnitAI>();
				}
			}
			catch {
				enemy = null;
				enemyAI = null;
			}
        }
        else {
            enemy = null;
            enemyAI = null;
        }
    }
}
