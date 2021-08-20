using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.AI;

public class UnitAI_Spearman : UnitAI_Infantry {
	[Header("Spearman Audio")]
	[FMODUnity.EventRef] public string attackEvent;
	[FMODUnity.EventRef] public string impactEvent;

	// Use this for initialization
	void Start () {
		self = gameObject;

		enemiesInRange = new List<UnitAI>();

		//ANIM accessing animator in model gameobject
		//unitAni = gameObject.GetComponentsInChildren<Animator>()[0];

		base.start();
	}

	#region Combat Functions
	// Deal damage to enemy unit, check if it's health is at or below 0, adjust engagement status accordingly
	public override void Attack() {
		if (enemyAI) {
			if (enemyAI.GetTeamPlayerKey() == teamPlayerKey) {
				enemiesInRange.RemoveAll(e => e == null || e.GetComponent<UnitAI>().GetTeamPlayerKey() == teamPlayerKey);
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
	#endregion

	#region Effect Functions
	public override void sound_attack() {
		FMOD.Studio.EventInstance attack = FMODUnity.RuntimeManager.CreateInstance(attackEvent);
		attack.set3DAttributes(FMODUnity.RuntimeUtils.To3DAttributes(gameObject));
		attack.setProperty(FMOD.Studio.EVENT_PROPERTY.MAXIMUM_DISTANCE, 1000f);
		attack.start();
		attack.release();
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
