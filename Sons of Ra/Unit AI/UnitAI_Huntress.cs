using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.AI;

public class UnitAI_Huntress : UnitAI_Infantry {
	public enum attackMove { first, second_hit1, second_hit2, third_launch, third_air, third_aoe };
	[Header("Attack Chain Management")]
	public List<int> attackDamageAmount;
	public List<float> attackLengths = new List<float>() {1.5f, 1.5f, 2f};
	public float AOERadius;

    [Header("Inspiration Buff")]
    public float inspirationRadius;

	[Header("Animation References")]
	[SerializeField] private AnimationClip attackAnimation1;
	[SerializeField] private AnimationClip attackAnimation2;
	[SerializeField] private AnimationClip attackAnimation3;

	[Header("FX References")]
	[SerializeField] private ParticleSystem groundSmashFX;

	[Header("Huntress Audio")]
	[FMODUnity.EventRef] [SerializeField] private string attackEvent;
	[FMODUnity.EventRef] [SerializeField] private string slamEvent;

	// Use this for initialization
	void Start () {
		self = gameObject;

		enemiesInRange = new List<UnitAI>();

		Debug.Log("grabbing animator");
		//ANIM accessing animator in model gameobject
		//unitAni = gameObject.GetComponentsInChildren<Animator>()[0];

		// scale attack animation lengths
		float scale = attackAnimation1.length / attackLengths[0];
		unitAni.SetFloat("attack1Speed", scale);
		scale = attackAnimation2.length / attackLengths[1];
		unitAni.SetFloat("attack2Speed", scale);
		scale = attackAnimation3.length / attackLengths[2];
		unitAni.SetFloat("attack3Speed", scale);

		base.start();
	}

	#region Combat Functions
	public override void AttackChain(int attackNum) {
		//Debug.Log(attackNum);
		if (attackNum <= (int)attackMove.second_hit2) {
			// normal attack
			if (attackNum == 0) {
				sound_attack();
			}
			SingleTargetAttack(attackNum);
		}
		else if (attackNum == (int)attackMove.third_launch) {
			// launch attack
			sound_slam();
			SingleTargetAttack(attackNum);
			if (enemy != null) {
				enemyAI.suspend();
			}
		}
		else if (attackNum == (int)attackMove.third_air) {
			// normal attack
			//sound_attack();
			SingleTargetConditionalAttack(attackNum);
			if (enemy != null && enemyAI.state == unitStates.suspended) {
				enemyAI.knockDown();
			}
		}
		else {
			//sound_slam();
			AOEAttack(attackNum);
			if (enemy != null && enemyAI.state == unitStates.suspended) {
				enemyAI.knockDown();
				enemyAI.unSuspend();
			}
		}
	}

	private void SingleTargetAttack(int attackNum) {
		if (enemyAI) {
			if (enemyAI.GetTeamPlayerKey() == teamPlayerKey) {
				enemiesInRange.Remove(enemy.GetComponent<UnitAI>());
				enemy = null;
				enemyAI = null;
			}
			else {
                if (enemyAI.takeDamage(attackDamageAmount[attackNum] * damageModifier, this, Constants.damageSource.unit)) {
					SonsOfRa.Events.GameEvents.InvokeUnitDealDamage(this, enemyAI, attackDamageAmount[attackNum] * damageModifier);
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

	// if enemy is dead, just don't deal damage, proceed with attack anyway
	private void SingleTargetConditionalAttack(int attackNum) {
		if (enemyAI) {
			if (enemyAI.GetTeamPlayerKey() == teamPlayerKey) {
				enemiesInRange.Remove(enemy.GetComponent<UnitAI>());
				enemy = null;
				enemyAI = null;
			}
			else if (enemyAI.getHealth() > 0 && enemyAI.state == unitStates.suspended) {
                if (enemyAI.takeDamage(attackDamageAmount[attackNum] * damageModifier, this, Constants.damageSource.unit)) {
					SonsOfRa.Events.GameEvents.InvokeUnitDealDamage(this, enemyAI, attackDamageAmount[attackNum] * damageModifier);
				}

                if (enemyAI.getHealth() <= 0) {
					ClearEnemies();
				}

				base.Attack();
			}
		}
		else {
			ClearEnemies();
		}
	}

	private void AOEAttack(int attackNum) {
		LayerMask unitMask = 1 << LayerMask.NameToLayer("Unit");
		Collider[] units = Physics.OverlapSphere(transform.position, AOERadius, unitMask);
		List<Collider> enemies = units.ToList();
		enemies.RemoveAll(u => u.gameObject.GetComponent<UnitAI>().GetTeamPlayerKey() == teamPlayerKey || u.gameObject.GetComponent<UnitAI>().getHealth() <= 0);

		GameObject groundPoundFXInstance = Instantiate(groundSmashFX.gameObject, transform.position, Quaternion.identity);
		groundPoundFXInstance.transform.localScale *= AOERadius;

		foreach(Collider c in enemies) {
			UnitAI ai = c.gameObject.GetComponent<UnitAI>();
            if (ai.takeDamage(attackDamageAmount[(int)attackMove.third_aoe], Constants.damageSource.unit)) {
				SonsOfRa.Events.GameEvents.InvokeUnitDealDamage(this, enemyAI, attackDamageAmount[attackNum] * damageModifier);
			}
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

	private void sound_slam() {
		FMOD.Studio.EventInstance slam = FMODUnity.RuntimeManager.CreateInstance(slamEvent);
		slam.set3DAttributes(FMODUnity.RuntimeUtils.To3DAttributes(gameObject));
		slam.setProperty(FMOD.Studio.EVENT_PROPERTY.MAXIMUM_DISTANCE, 1000f);
		slam.start();
		slam.release();
	}

	public override void sound_impact() {
		//FMOD.Studio.EventInstance attack = FMODUnity.RuntimeManager.CreateInstance(attackEvent);
		//attack.set3DAttributes(FMODUnity.RuntimeUtils.To3DAttributes(gameObject));
		//attack.setProperty(FMOD.Studio.EVENT_PROPERTY.MAXIMUM_DISTANCE, 1000f);
		//attack.start();
		//attack.release();
	}

	protected override void sound_death() {
		FMOD.Studio.EventInstance death = FMODUnity.RuntimeManager.CreateInstance(deathEvent);
		death.set3DAttributes(FMODUnity.RuntimeUtils.To3DAttributes(gameObject));
		death.setProperty(FMOD.Studio.EVENT_PROPERTY.MAXIMUM_DISTANCE, 1000f);
		death.start();
		death.release();
	}
	#endregion

	#region Death Functions
	public override void Die(Constants.damageSource source)
    {
        LayerMask unitMask = 1 << LayerMask.NameToLayer("Unit");
        Collider[] units = Physics.OverlapSphere(transform.position, inspirationRadius, unitMask);
        List<Collider> allies = units.ToList();
        allies.RemoveAll(u => u.gameObject.GetComponent<UnitAI>().GetTeamPlayerKey() != teamPlayerKey || u.gameObject.GetComponent<UnitAI>().getHealth() <= 0);

        foreach (Collider c in allies) {
			UnitAI ai = c.gameObject.GetComponent<UnitAI>();

			BattleHardened buff = (BattleHardened)ai.activeEffects.Find(x => x.type == BuffDebuff.BuffsAndDebuffs.battleHardened);
			if (buff) {
				buff.TriggerBoost();
			}
		}

        base.Die(source);
    }

    #endregion
}
