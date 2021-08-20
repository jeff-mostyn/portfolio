using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AI;
using Rewired;
using System.Linq;

public abstract class UnitAI : MonoBehaviour {
	public enum unitStates { walking, attacking, stunned, suspended, dead };
	protected const int TowerLayer = 8;

	// ------------- state variables ---------------
	public Constants.unitType type;
	public unitStates state;

	// -------------- player references ---------------
	//[Header ("Player References")]
	//public GameObject p1;
	//public GameObject p2;

	// ------------- assorted flags ----------------
	[Header ("Status Flags")]
	public bool damageImmune;
	protected bool beingAttacked;
	public int isSlow;
	public bool aggroed;

	// ------------- starting attribute values -----------------
	[Header ("Base Attribute Values")]
	public float startingHealth;
	public float maxShield;
	public float damage;
	public float attackSpeed; // number of attacks per second
	public float baseMovementSpeed;
	public float range;
	public float cost;
	public int favor;
	public UnitAI_Shieldbearer aggroTarget;
    public UnitMovement uMovement;

    // ------------------ attribute values ---------------------
	[Header ("Attributes")]
    public float armor; //from 0 to 1 with 0 being taking no damage
	public int blockStacks = 0;
	[SerializeField] protected float movementSpeedModifier = 1;
	protected float attackSpeedModifier = 1;
	public float damageModifier = 1;
	[SerializeField]
	protected float health;
    [SerializeField]
	protected float attackCountDown;
	protected float stunDuration;
    public float shield;
	public float threatValue;

	// ------------------ effects -------------------
	[Header ("Effects")]
	public ParticleSystem activePowerBoostFX;
	public GameObject favorBurst; //switched to GameObject instead of Particle System
	protected SoundManager s;
	//protected AudioSource soundPlayer;

	public GameObject self;
	protected GameObject enemy;
	protected GameObject projectile;
    [SerializeField] protected NavMeshAgent agent;

	protected const string p1UnitTag = "P1Unit";
	protected const string p2UnitTag = "P2Unit";
	protected string enemyUnitTag;

	public string ownerPlayerKey;
	protected string teamPlayerKey;
	protected Player rewiredPlayer;
    [SerializeField] protected UnitDelegate unitDel;

    protected bool gameEnded = false;

	[Header("References")]
	[SerializeField] protected Animator unitAni;
	[SerializeField] private AnimationClip attackAnimation;
	public Transform baseLocator;

	// ---------- Buffs and Debuffs -----------
	[Header ("Buffs and Debuffs")]
	public List<BuffDebuff> activeEffects;
	public bool is_debuffEmbalm;

	[Header("General Audio")]
	[FMODUnity.EventRef] [SerializeField] protected string deathEvent;

	// ---------- Abstract Methods ----------
	protected abstract void OnTriggerEnter(Collider other);
	protected abstract void OnTriggerExit(Collider other);
	protected abstract void CheckForTarget();
	public abstract void MyTeamWon();
	public abstract void MyTeamLost();
	protected abstract void sound_death();
	public abstract void sound_attack();
	public abstract void sound_impact();
	public abstract void RefreshTargets();

	#region "System" Functions
	// Use this for initialization
	public void start () {
		activeEffects = new List<BuffDebuff>();

		health = startingHealth;

		GetComponentInChildren<CapsuleCollider>().radius = range;

		attackCountDown = 1 / attackSpeed; // 1 second / attacks per second gives time between attacks		

		//agent = gameObject.GetComponent<NavMeshAgent>();

		toWalking();

		s = SoundManager.Instance;
		//soundPlayer = GetComponentInChildren<AudioSource>();

        //unitDel = GetComponentInChildren<UnitDelegate>();

		AttackAnimScale();

		SonsOfRa.Events.GameEvents.InvokeUnitSpawn(this);

		if (SettingsManager.Instance.GetIsOnline() && OnlineManager.Instance.GetIsHost()) {
			StartCoroutine(RegularSync());
        }
	}

	protected virtual void Update() {
		switch (state) {
			case unitStates.attacking:
				UpdateAttacking();
				break;
			case unitStates.stunned:
				UpdateStunned();
				break;
			case unitStates.suspended:
				break;
			case unitStates.walking:
				UpdateWalking();
				break;
            case unitStates.dead:
                break;
            default:
				break;
		}

        if (health <= 0) {
            Die();
        }
    }

	protected virtual void UpdateAttacking() {
		if (enemy != null) { // attack only if unit is engaged and its target exists
			transform.LookAt(enemy.transform);
			attackCountDown -= Time.deltaTime;  // countdown time until next attack

			if (attackCountDown <= 0) {
				//ANIM - attackAnimation
				unitAni.SetTrigger("attack");

				attackCountDown = 1 / (attackSpeed * attackSpeedModifier);  // reset attack countdown
			}
		}
		else {
			CheckForTarget();
			if (!enemy) {
				toWalking();
			}
		}
	}

	protected virtual void UpdateStunned() {
		if (!gameEnded) {
			stunDuration -= Time.deltaTime;
		}
		if (stunDuration <= 0) {
			unitAni.SetBool("isDizzy", false);

			if (enemy) toAttacking();
			else toWalking();
		}
	}

	protected virtual void UpdateWalking() {
		// Remove any null references and check for a new target
		CheckForTarget();
		if (enemy != null) {
			toAttacking();
		}
	}
	#endregion

	#region State Change Functions
	protected void toWalking() {
		if (state != unitStates.dead) {
			if (type == Constants.unitType.huntress) Debug.Log("walking");
			// remove from combat if currently in combat
			if (state == unitStates.attacking) {
				RemoveFromCombat();
			}

			// state change
			state = unitStates.walking;

			// animation change
			if (type != Constants.unitType.catapult) {
				unitAni.SetBool("walking", true);
			}

			// navmesh
			gameObject.GetComponent<NavMeshObstacle>().enabled = false;
			agent.enabled = true;
			agent.SetDestination(uMovement.target.position); //seems a bit contrived.
		}
    }

	protected virtual void toAttacking() {
		if (state != unitStates.dead) {
			// state change
			state = unitStates.attacking;

			// animation change
			unitAni.SetBool("walking", false);
			unitAni.SetBool("attackState", true);

			// navmesh
			if (enemy.GetComponent<UnitAI>().getType() != Constants.unitType.catapult) {
				agent.enabled = false;
				gameObject.GetComponent<NavMeshObstacle>().enabled = true;
			}
		}
    }

	protected void toStunned(float duration) {
		if (state != unitStates.suspended && state != unitStates.dead) {
			stunDuration = duration;

			// state change
			state = unitStates.stunned;

			// animation change
			unitAni.SetBool("isDizzy", true);

			// navmesh
			agent.enabled = false;
			gameObject.GetComponent<NavMeshObstacle>().enabled = true;
		}
	}

	protected void toSuspended() {
		if (state != unitStates.dead && type != Constants.unitType.catapult && type != Constants.unitType.huntress) {
			// state change
			state = unitStates.suspended;

			// animation change
			unitAni.SetBool("suspended", true);

			// navmesh
			agent.enabled = false;
			gameObject.GetComponent<NavMeshObstacle>().enabled = false;
		}
	}

	public void knockDown() {
		Debug.Log("fall");
		unitAni.SetTrigger("unSuspendFall");
	}

	public void unSuspend() {
		Debug.Log("unsuspending");
		unitAni.SetBool("suspended", false);
		if (enemy != null) {
			toAttacking();
		}
		else {
			toWalking();
		}
	}

	#endregion

	#region Combat Functions
	public virtual void AttackChain(int attackNum) { }

	public virtual void Attack() { }

	protected virtual void ClearEnemies() {
		enemy = null;
	}

	// transition unit to walking and unset combat booleans
	protected virtual void RemoveFromCombat() {
		if(type == Constants.unitType.huntress) Debug.Log("remove from combat");

		// reset combat stuff
		ClearEnemies();
		attackCountDown = 1 / (attackSpeed * attackSpeedModifier);

		// animation change
		if (type != Constants.unitType.catapult) {
			unitAni.SetBool("attackState", false);
		}
	}

	// Take damage and subtract it from health. If health <= 0 initiate unit death sequence
	public virtual bool takeDamage(float damageDone, UnitAI attacker, Constants.damageSource source) {
		if (type != Constants.unitType.catapult) {
			attacker.sound_impact();
		}

		return takeDamage(damageDone, source);
    }

	public bool takeDamage(float damageDone, Constants.damageSource source) {
		bool tookDamage;

		if (source != Constants.damageSource.blessing) {
			damageDone = Mathf.Floor(damageDone * armor);
		}
		else if (type == Constants.unitType.catapult) {
			// catapult damage sounds
			((UnitAI_Catapult)this).sound_getHit();
		}


		if (health > 0 
            && !damageImmune
			&& blockStacks <= 0
            && (!SettingsManager.Instance.GetIsOnline() || (SettingsManager.Instance.GetIsOnline() && OnlineManager.Instance.GetIsHost()))) {

			if ((shield - damageDone) > 0) {
				shield -= damageDone;
			}
			else {
				damageDone -= shield;
				shield = 0;
				health -= damageDone;
			}

			SendSync();

			if (health <= 0) {
                stunDuration = 5;
				agent.enabled = false;
                gameObject.GetComponent<NavMeshObstacle>().enabled = false;
				GetComponent<BoxCollider>().enabled = false;
				Die(source);
				unitDel.ActivateUnitDel(); // deleting unit from tower target arrays
			}

            tookDamage = true;
		}
        else {
            tookDamage = false;
        }

		SonsOfRa.Events.GameEvents.InvokeUnitTakeDamage(this, (int)damageDone);
		return tookDamage;
	}

	#endregion

	#region Death Functions
	// Unit death sequence: give favor to opposing player and destroy game object. Could also initiate animation here
	public virtual void Die(Constants.damageSource source = Constants.damageSource.sync) {
		if (state != unitStates.dead) {
			state = unitStates.dead;

			baseMovementSpeed = 0;

			SonsOfRa.Events.GameEvents.InvokeUnitDie(this, ownerPlayerKey, source);

			//ANIM - death animation
			float randomVal = Random.Range(0.0f, 3.0f);
			if (randomVal < 1f) {
				unitAni.SetTrigger("death1");
				unitAni.SetBool("walking", false);

			}
			else if (randomVal >= 1f && randomVal < 2) {
				unitAni.SetTrigger("death2");
				unitAni.SetBool("walking", false);

			}
			else {
				unitAni.SetTrigger("death3");
				unitAni.SetBool("walking", false);
			}

			Instantiate(favorBurst, transform.position, Quaternion.identity);
			GetComponentInChildren<Canvas>().enabled = false;

			// Play Death Sound
			sound_death();

			Destroy(gameObject, 1);
		}
	}

	private void OnDestroy() {
		if (SettingsManager.Instance.GetIsOnline() && OnlineManager.Instance.GetIsHost()) {
			OnlineManager.Instance.SendPacket(new PO_Entity_Destroy(PacketObject.packetType.unitDestroy, GetComponent<EntityIdentifier>().ID, ownerPlayerKey));
		}

		if (LivingUnitDictionary.dict[PlayerIDs.player1].Contains(gameObject)) {
			LivingUnitDictionary.dict[PlayerIDs.player1].Remove(gameObject);
		}
		else if (LivingUnitDictionary.dict[PlayerIDs.player2].Contains(gameObject)) {
			LivingUnitDictionary.dict[PlayerIDs.player2].Remove(gameObject);
		}
	}

	#endregion

	#region Effect Functions

	#endregion

	#region Buff and Debuff Functions
	public void RemoveEffectFromList(BuffDebuff effect) {
		activeEffects.Remove(effect);
	}

	public void RemoveAllEffects() {
		activeEffects.ForEach(effect => effect.Cleanse());
	}

	public void heal(float amount) {
		if (health + amount < startingHealth) {
			health += amount;
		}
		else if (health < startingHealth) {
			BuffDebuff overhealShield = activeEffects.Find(buff => buff.type == BuffDebuff.BuffsAndDebuffs.overheal);
			if (overhealShield) {
				float diff = startingHealth - health;
				addShield((amount - diff) * ((OverhealShield)overhealShield).GetPercentHeal());
			}
			health = startingHealth;
		}
		else if (health == startingHealth && activeEffects.Find(buff => buff.type == BuffDebuff.BuffsAndDebuffs.overheal)) {
			BuffDebuff overhealShield = activeEffects.Find(buff => buff.type == BuffDebuff.BuffsAndDebuffs.overheal);
			addShield(amount * ((OverhealShield)overhealShield).GetPercentHeal());
		}
	}

	public void addShield(float shieldAmount) {
		if (shield + shieldAmount < maxShield) {
			shield += shieldAmount;
		}
		else if (shield < maxShield) {
			shield = maxShield;
		}
	}
	#endregion

	#region Helper Functions
	public void AttackAnimScale() {
		float timeBetweenAttacks = 1 / (attackSpeed * attackSpeedModifier);

		if (type != Constants.unitType.catapult && type != Constants.unitType.huntress) {
			if (attackAnimation.length > timeBetweenAttacks) {
				float scale = attackAnimation.length / timeBetweenAttacks;
				if (unitAni) {
					unitAni.SetFloat("attackSpeedModifier", scale);
				}
			}
			else {
				if (unitAni) {
					unitAni.SetFloat("attackSpeedModifier", 1.0f);
				}
			}
		}
	}
	#endregion

	#region Getters and Setters
	public float getHealth() {
		return health;
	}

	public float getDamage() {
		return damage * damageModifier;
	}

	public float getMovementSpeed() {
		if (state == unitStates.walking) {
			return baseMovementSpeed * movementSpeedModifier;
		}
		else if (state == unitStates.attacking) {    
            return 0.0f;
        }
        else {
            return 0.0f;
        }			
	}

	public string GetOwnerPlayerKey() {
		return ownerPlayerKey;
	}

	public string GetTeamPlayerKey() {
		return teamPlayerKey;
	}

	public virtual void SetTeamPlayerKey(string ID) {
		teamPlayerKey = ID;
		if (teamPlayerKey == PlayerIDs.player1) {
			gameObject.tag = p1UnitTag;
			enemyUnitTag = p2UnitTag;
		}
		else {
			gameObject.tag = p2UnitTag;
			enemyUnitTag = p1UnitTag;
		}

        uMovement.setRewiredPlayerKey(ID);
	}

	public float getCost() {
		return cost;
	}

	public bool checkIfEngaged() {
		if (state == unitStates.attacking)
			return true;
		else
			return false;
	}

	public GameObject getTarget() {
		return enemy;
	}

	public Constants.unitType getType() {
		return type;
	}

	public int getIsSlow() {
		return isSlow;
	}

	public void setIsSlow(bool tf) {
		if (tf) {
			isSlow += 1;
		}
		else if (isSlow > 0) {
			isSlow -= 1;
		}
	}

	public void setIsDamageImmune(bool tf) {
		damageImmune = tf;
	}

	public bool getIsDamageImmune() {
		return damageImmune;
	}

	public void adjustMoveSpeedModifier(float mod) {
		movementSpeedModifier += mod;
	}

	public void adjustAttackSpeedModifier(float mod) {
		attackSpeedModifier += mod;
	}

	public void adjustDamageModifier(float mod) {
		damageModifier += mod;
	}

	public float GetThreatModifier() {
		float modifier = 0;
		float healthPercent = (health + shield) / startingHealth;

		return modifier + healthPercent;
	}

	public void stun(float duration) {
		toStunned(duration);
	}

	public void suspend() {
		Debug.Log("suspending");
		toSuspended();
	}
    #endregion

    #region Online Functions
	public void SendSync() {
		if (SettingsManager.Instance.GetIsOnline() && OnlineManager.Instance.GetIsHost()) {
			OnlineManager.Instance.SendPacket(GetSyncPacket());
		}
	}

	public void SendBattleHardenedSync() {
		BattleHardened buff = (BattleHardened)activeEffects.Find(x => x.type == BuffDebuff.BuffsAndDebuffs.battleHardened);
		if (buff) {
			PO_BattleHardenedSync packet = new PO_BattleHardenedSync(GetComponent<EntityIdentifier>().ID, ownerPlayerKey, teamPlayerKey, buff.GetStacks());
			OnlineManager.Instance.SendPacket(packet);
		}
	}

    public PacketObject GetSyncPacket() {
        int enemyID = enemy != null ? enemy.GetComponent<EntityIdentifier>().ID : -1;
		//Debug.Log(ownerPlayerKey + "for sync");

		if (type == Constants.unitType.catapult) {
			return new PO_CatapultSync(GetComponent<EntityIdentifier>().ID, ownerPlayerKey, teamPlayerKey, enemyID, transform.position, armor, blockStacks, movementSpeedModifier, attackSpeedModifier, damageModifier, health, shield, uMovement.GetWaypointIndex());
		}
        else {
			return new PO_UnitSync(GetComponent<EntityIdentifier>().ID, ownerPlayerKey, teamPlayerKey, enemyID, transform.position, armor, blockStacks, movementSpeedModifier, attackSpeedModifier, damageModifier, health, shield, uMovement.GetWaypointIndex());
		}
    }

    private IEnumerator RegularSync() {
        while (true) {
			SendBattleHardenedSync();
            yield return new WaitForSeconds(OnlineManager.Instance.unitPollRate);
        }
    }

    public virtual void Sync(int _enemyEntityID, float[] _position, float _armor, int _blockStacks, float _moveSpeedMod, float _attackSpeedMod, float _damageMod, float _health, float _shield, int _waypointIndex) {
        Vector3 inputPos = new Vector3(_position[0], _position[1], _position[2]);
        Vector3 diff = inputPos - transform.position;

        // this should give some smoothing to any differences in positions
        if (diff.magnitude > 1.0f) {
            transform.position = inputPos;
        }
        else if (diff.magnitude > 0.1f) {
            transform.position += diff * 0.1f;
        }

        armor = _armor;
		blockStacks = _blockStacks;
        movementSpeedModifier = _moveSpeedMod;
        attackSpeedModifier = _attackSpeedMod;
        damageModifier = _damageMod;
        health = _health;
        shield = _shield;

        uMovement.SetWaypoint(_waypointIndex);
    }
    #endregion
}
