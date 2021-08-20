using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.AI;

public class UnitAI_Archer : UnitAI_Infantry {
	[Header("Archer References")]
    public GameObject arrow;

	[Header("Archer Audio")]
	[FMODUnity.EventRef] public string attackEvent;
	[FMODUnity.EventRef] public string impactEvent;
	[FMODUnity.EventRef] public string drawEvent;
	[FMODUnity.EventRef] public string whistleEvent;

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
                enemiesInRange.RemoveAll(e => e.GetComponent<UnitAI>().GetTeamPlayerKey() == teamPlayerKey);
                enemy = null;
				enemyAI = null;
			}
			else {
				GameObject arrowShot = Instantiate(arrow, new Vector3(transform.position.x, transform.position.y + 0.7f, transform.position.z), Quaternion.LookRotation(transform.forward));
				arrowShot.GetComponent<ArcherArrow>().startArrow(teamPlayerKey, damage * damageModifier, this);

				if (enemyAI.getHealth() <= 0 || enemy == null) {
					ClearEnemies();
				}

				base.Attack();

				sound_whistle();
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

	public void sound_draw() {
		FMOD.Studio.EventInstance draw = FMODUnity.RuntimeManager.CreateInstance(drawEvent);
		draw.set3DAttributes(FMODUnity.RuntimeUtils.To3DAttributes(gameObject));
		draw.setProperty(FMOD.Studio.EVENT_PROPERTY.MAXIMUM_DISTANCE, 1000f);
		draw.start();
		draw.release();
	}

	public void sound_whistle() {
		FMOD.Studio.EventInstance whistle = FMODUnity.RuntimeManager.CreateInstance(whistleEvent);
		whistle.set3DAttributes(FMODUnity.RuntimeUtils.To3DAttributes(gameObject));
		whistle.setProperty(FMOD.Studio.EVENT_PROPERTY.MAXIMUM_DISTANCE, 1000f);
		whistle.start();
		whistle.release();
	}

	public override void sound_impact() {
		FMOD.Studio.EventInstance attack = FMODUnity.RuntimeManager.CreateInstance(impactEvent);
		attack.set3DAttributes(FMODUnity.RuntimeUtils.To3DAttributes(gameObject));
		attack.setProperty(FMOD.Studio.EVENT_PROPERTY.MAXIMUM_DISTANCE, 1000f);
		attack.start();
		attack.release();
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
