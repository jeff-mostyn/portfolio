using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.AI;

public class UnitAI_Catapult : UnitAI {

	// Enemy-related variables
	protected List<TowerState> enemiesInRange;
    protected List<UnitAI> catapultsInRange;
	protected TowerState enemyTowerState;

	protected const float towerDetectionWaitTime = 2.0f;

	[Header("Catapult Specific Values")]
	public float catapultDamage;
	[SerializeField] private Transform launchPosition;

	[Header("Spearman Audio")]
	[FMODUnity.EventRef] public string attackEvent;
	[FMODUnity.EventRef] public string getHitEvent;
	[FMODUnity.EventRef] public string reloadEvent;

	// Use this for initialization
	void Start () {
		self = gameObject;

		enemiesInRange = new List<TowerState>();
        catapultsInRange = new List<UnitAI>();

        StartCoroutine(TowerDetection());

		//ANIM accessing animator in model gameobject
		//unitAni = gameObject.GetComponentsInChildren<Animator>()[0]; //delete if catapult anim is removed

		base.start();
	}

	protected override void UpdateAttacking() {
		base.UpdateAttacking();
	}

	protected override void toAttacking() {
		if (state != unitStates.dead) {
			// state change
			state = unitStates.attacking;
		}
	}

	#region Combat Functions
	// Deal damage to enemy tower
	public override void Attack() {
        if (enemyTowerState) {
            if (enemyTowerState.rewiredPlayerKey == teamPlayerKey) {
                toWalking();
            }
        }   

		projectile = PoolManager.Instance.getCatapultProjectileFromPool(launchPosition.position, transform.rotation);

		projectile.GetComponent<Missiles>().catapultAI = gameObject.GetComponent<UnitAI_Catapult>();

		base.Attack();

		projectile.GetComponent<Missiles>().setupProjectileMotion(Vector3.Distance(launchPosition.position, enemy.transform.position));
		projectile.GetComponent<Missiles>().target = enemy;
	}

	// helper function for attacking towers and dealing with the projectile
	public void attackTower_Helper() {
		if (enemy == null) {
			enemiesInRange.RemoveAll(e => e == null || e.gameObject.GetComponent<TowerHealth>().getHealth() <= 0);

			enemyTowerState = null;
		}
		else {
			enemy.GetComponent<TowerHealth>().TakeDamage(damage * damageModifier);
		}
	}

    public void attackUnit_Helper() {
        enemy.GetComponent<UnitAI>().takeDamage(catapultDamage * damageModifier, Constants.damageSource.tower);

        if (enemy == null) {
			catapultsInRange.RemoveAll(c => c == null || c.getHealth() <= 0);
		}
    }

	/// <summary>
	/// Examine lists of nearby targets. If one exists, target it.
	/// </summary>
	protected override void CheckForTarget() {
		if (enemy == null && (enemiesInRange.Count != 0 || catapultsInRange.Count != 0)) {
			enemiesInRange.RemoveAll(e => e == null || e.gameObject.GetComponent<TowerHealth>().getHealth() <= 0);
			catapultsInRange.RemoveAll(c => c == null || c.getHealth() <= 0);
			if (enemiesInRange.Count != 0) {
				enemy = enemiesInRange[0].gameObject;
				enemyTowerState = enemy.GetComponent<TowerState>();
			}
			else if (catapultsInRange.Count != 0) {
				enemy = catapultsInRange[0].gameObject;
			}
		}
	}

	public override void RefreshTargets() {
		catapultsInRange.Clear();
		enemiesInRange.Clear();

		DetectTowerTargets();
		DetectCatapultTargets();
	}
	#endregion

	#region Detection Functions

	// if enemy unit enters range of unit and unit is not engaged with it, act upon that input
	protected override void OnTriggerEnter(Collider other) {
		if (other.gameObject.layer == TowerLayer) {
			TowerState detectedTowerState = other.gameObject.GetComponent<TowerState>();
			if (detectedTowerState.rewiredPlayerKey != teamPlayerKey && detectedTowerState.state == TowerState.tStates.placed) {
				enemiesInRange.Add(enemyTowerState);
				if (enemy == null) {
					enemy = other.gameObject;
					enemyTowerState = detectedTowerState;
				}
			}
		}
        else if(other.gameObject.tag == enemyUnitTag && other.gameObject.GetComponent<UnitAI>().getType() == Constants.unitType.catapult) {
            UnitAI ai = other.gameObject.GetComponent<UnitAI>();
            catapultsInRange.Add(ai);
			if (enemy == null) {
				enemy = other.gameObject;
			}
		}
	}

	// needs to be reworked specifically for towers
	protected override void OnTriggerExit(Collider other) {
		enemiesInRange.Remove(other.gameObject.GetComponent<TowerState>());
		if (other.gameObject == enemy) {
			toWalking();

			enemyTowerState = null;

			unitAni.SetTrigger("reload");
		}
	}

	// Active tower detection for catapults to find towers placed in locations already in their range
	IEnumerator TowerDetection() {
		while (gameObject) {
			// wait specified amount of time and until catapult is not attacking
			yield return new WaitForSecondsRealtime(towerDetectionWaitTime);
			yield return new WaitUntil(delegate { return state == unitStates.walking; });

			DetectTowerTargets();
			DetectCatapultTargets();
		}
	}

	private void DetectTowerTargets() {
		int towerLayer = 1 << LayerMask.NameToLayer("Tower");
		List<Collider> towersList = new List<Collider>();
		Collider[] towers;
		GameObject tower;

		// find towers in range, get list of all enemy towers
		towers = Physics.OverlapSphere(transform.position, range, towerLayer);
		towersList = towers.ToList();
		towersList.RemoveAll(x => x.gameObject.GetComponent<TowerState>().rewiredPlayerKey == teamPlayerKey);

		// go through towers in list, make sure to add new ones to enemy towers in range
		for (int i = 0; i < towersList.Count; i++) {
			tower = towersList[i].gameObject;
			TowerState towerStateScript = tower.GetComponent<TowerState>();
			if (!enemiesInRange.Exists(x => x == tower) && towerStateScript.state == TowerState.tStates.placed) {
				enemiesInRange.Add(towerStateScript);
			}
		}
	}

	private void DetectCatapultTargets() {
		int unitLayer = 1 << LayerMask.NameToLayer("Unit");
		List<Collider> catapultsList = new List<Collider>();
		Collider[] catapults;
		GameObject catapult;

		// find towers in range, get list of all enemy towers
		catapults = Physics.OverlapSphere(transform.position, range, unitLayer);
		catapultsList = catapults.ToList();
		catapultsList.RemoveAll(x => x.gameObject.GetComponent<UnitAI>().GetTeamPlayerKey() == teamPlayerKey);

		// go through towers in list, make sure to add new ones to enemy towers in range
		for (int i = 0; i < catapultsList.Count; i++) {
			catapult = catapultsList[i].gameObject;
			UnitAI ai = catapult.GetComponent<UnitAI>();
			if (ai.getType() == Constants.unitType.catapult && !catapultsInRange.Exists(x => x == catapult)) {
				catapultsInRange.Add(ai);
			}
		}
	}

	#endregion

	#region Death Functions

	// Unit death sequence: give favor to opposing player and destroy game object. Could also initiate animation here
	public override void Die(Constants.damageSource source) {
		if (state != unitStates.dead) {
			state = unitStates.dead;

			baseMovementSpeed = 0;

			SonsOfRa.Events.GameEvents.InvokeUnitDie(this, ownerPlayerKey, source);

			unitAni.SetTrigger("die");

			// play favor burst effect
			Instantiate(favorBurst, transform.position, Quaternion.identity);

			// Play Death Sound
			sound_death();

			GetComponentInChildren<Canvas>().enabled = false;

			Destroy(gameObject, 1);
		}
	}

	#endregion

	#region Effect Functions
	protected override void sound_death() {
		FMOD.Studio.EventInstance death = FMODUnity.RuntimeManager.CreateInstance(deathEvent);
		death.set3DAttributes(FMODUnity.RuntimeUtils.To3DAttributes(gameObject));
		death.setProperty(FMOD.Studio.EVENT_PROPERTY.MAXIMUM_DISTANCE, 1000f);
		death.start();
		death.release();
	}

	public override void sound_attack() {
		FMOD.Studio.EventInstance attack = FMODUnity.RuntimeManager.CreateInstance(attackEvent);
		attack.set3DAttributes(FMODUnity.RuntimeUtils.To3DAttributes(gameObject));
		attack.setProperty(FMOD.Studio.EVENT_PROPERTY.MAXIMUM_DISTANCE, 1000f);
		attack.start();
		attack.release();
	}

	public override void sound_impact() {
		
	}

	public void sound_getHit() {
		FMOD.Studio.EventInstance getHit = FMODUnity.RuntimeManager.CreateInstance(getHitEvent);
		getHit.set3DAttributes(FMODUnity.RuntimeUtils.To3DAttributes(gameObject));
		getHit.setProperty(FMOD.Studio.EVENT_PROPERTY.MAXIMUM_DISTANCE, 1000f);
		getHit.start();
		getHit.release();
	}

	public void sound_reload() {
		FMOD.Studio.EventInstance reload = FMODUnity.RuntimeManager.CreateInstance(reloadEvent);
		reload.set3DAttributes(FMODUnity.RuntimeUtils.To3DAttributes(gameObject));
		reload.setProperty(FMOD.Studio.EVENT_PROPERTY.MAXIMUM_DISTANCE, 1000f);
		reload.start();
		reload.release();
	}

	// win/lose animations at the end of the game
	public override void MyTeamWon() {
		state = unitStates.stunned;
		agent.enabled = false;
		gameObject.GetComponent<NavMeshObstacle>().enabled = true;
		stunDuration = 30f;
	}

	public override void MyTeamLost() {
		state = unitStates.stunned;
		agent.enabled = false;
		gameObject.GetComponent<NavMeshObstacle>().enabled = true;
		stunDuration = 30f;
	}

	#endregion

	#region Getters and Setters
	public override void SetTeamPlayerKey(string ID) {
		base.SetTeamPlayerKey(ID);

		if (state == unitStates.attacking) {
			if (enemyTowerState != null && enemyTowerState.rewiredPlayerKey == teamPlayerKey) {
				toWalking();
			}
		}
	}
    #endregion

    public override void Sync(int _enemyEntityID, float[] _position, float _armor, int _blockStacks, float _moveSpeedMod, float _attackSpeedMod, float _damageMod, float _health, float _shield, int _waypointIndex) {
        TargetSync(_enemyEntityID);

        base.Sync(_enemyEntityID, _position, _armor, blockStacks, _moveSpeedMod, _attackSpeedMod, _damageMod, _health, _shield, _waypointIndex);
    }

    public void TargetSync(int _enemyEntityID) {
        if (_enemyEntityID != -1) {
			try {
				enemy = LivingTowerDictionary.dict[teamPlayerKey == PlayerIDs.player1 ? PlayerIDs.player2 : PlayerIDs.player1].Find(e => e.GetComponent<EntityIdentifier>().ID == _enemyEntityID);

				if (enemy != null) {
					enemyTowerState = enemy.GetComponent<TowerState>();
				}
				else {
					enemy = LivingUnitDictionary.dict[teamPlayerKey == PlayerIDs.player1 ? PlayerIDs.player2 : PlayerIDs.player1].Find(e => e.GetComponent<EntityIdentifier>().ID == _enemyEntityID);
				}
			}
			catch {
				enemy = null;
				enemyTowerState = null;
			}
        }
        else {
            enemy = null;
            enemyTowerState = null;
        }
    }
}
