using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.AI;

public class UnitAI_Shieldbearer : UnitAI_Infantry {
	// unique
	[Header("Shieldbearer Unique Attributes")]
	public List<UnitAI> aggroedUnits;
	public int maxAggro;
	public float flatReducedArcherDamage;

	[Header("Shieldbearer Audio")]
	[FMODUnity.EventRef] public string attackEvent;
	[FMODUnity.EventRef] public string impactEvent;
	[FMODUnity.EventRef] public string blockEvent;

	// Use this for initialization
	void Start () {
		self = gameObject;

		enemiesInRange = new List<UnitAI>();
		aggroedUnits = new List<UnitAI>();

		//ANIM accessing animator in model gameobject
		//unitAni = gameObject.GetComponentsInChildren<Animator>()[0];

		base.start();
	}

	#region Combat Functions
	// Deal damage to enemy unit, check if it's health is at or below 0, adjust engagement status accordingly
	public override void Attack() {
		if (enemyAI) {
			if (enemyAI.GetTeamPlayerKey() == teamPlayerKey) {
                enemiesInRange.RemoveAll(e => e.GetComponent<UnitAI>().GetTeamPlayerKey() == teamPlayerKey);
                enemy = null;
				enemyAI = null;
			}
			else {
                if (enemyAI.takeDamage(damage * damageModifier, this, Constants.damageSource.unit)) {
					SonsOfRa.Events.GameEvents.InvokeUnitDealDamage(this, enemyAI, damage * damageModifier);
				}
                base.Attack();

				if (enemyAI.getHealth() <= 0) {
					ClearEnemies();
				}
			}
		}
		else {
			ClearEnemies();
		}
	}

	public bool canAggro() {
		if (aggroedUnits.Count < maxAggro)
			return true;
		else
			return false;
	}

	public void drawAggro(UnitAI u) {
		u.aggroed = true;
		aggroedUnits.Add(u);
	}

	public void loseAggro(UnitAI u) {
		aggroedUnits.Remove(u);
	}

	// Take damage and subtract it from health. If health <= 0 initiate unit death sequence
	public override bool takeDamage(float damageDone, UnitAI attacker, Constants.damageSource source) {
		if (attacker.type == Constants.unitType.archer) {
			damageDone = Mathf.Max(damageDone - flatReducedArcherDamage, 0);
		}

		return base.takeDamage(damageDone, attacker, source);
	}
	#endregion

	#region Death Functions

	// Unit death sequence: give favor to opposing player and destroy game object. Could also initiate animation here
	public override void Die(Constants.damageSource source) {
		if (aggroed) {
			aggroTarget.loseAggro(this);
		}

		if (aggroedUnits.Count != 0) {
			for (int i=0; i<aggroedUnits.Count; i++) {
				aggroedUnits[i].aggroed = false;
				aggroedUnits[i].aggroTarget = null;
			}
		}

		base.Die(source);
	}

	#endregion

	#region Effect Functions
	public override void sound_attack() {
		FMOD.Studio.EventInstance attack = FMODUnity.RuntimeManager.CreateInstance(attackEvent);
		attack.set3DAttributes(FMODUnity.RuntimeUtils.To3DAttributes(gameObject));
		attack.setProperty(FMOD.Studio.EVENT_PROPERTY.MAXIMUM_DISTANCE, 1000f);
		attack.start();
		attack.release();
	}

	public void sound_blockArrow() {
		FMOD.Studio.EventInstance block = FMODUnity.RuntimeManager.CreateInstance(blockEvent);
		block.set3DAttributes(FMODUnity.RuntimeUtils.To3DAttributes(gameObject));
		block.setProperty(FMOD.Studio.EVENT_PROPERTY.MAXIMUM_DISTANCE, 1000f);
		block.start();
		block.release();
	}

	public override void sound_impact() {
		FMOD.Studio.EventInstance impact = FMODUnity.RuntimeManager.CreateInstance(impactEvent);
		impact.set3DAttributes(FMODUnity.RuntimeUtils.To3DAttributes(gameObject));
		impact.setProperty(FMOD.Studio.EVENT_PROPERTY.MAXIMUM_DISTANCE, 1000f);
		impact.start();
		impact.release();
	}

	protected override void sound_death() {
		FMOD.Studio.EventInstance death = FMODUnity.RuntimeManager.CreateInstance(deathEvent);
		death.set3DAttributes(FMODUnity.RuntimeUtils.To3DAttributes(gameObject));
		death.setProperty(FMOD.Studio.EVENT_PROPERTY.MAXIMUM_DISTANCE, 1000f);
		death.start();
		death.release();
	}
	#endregion
}
