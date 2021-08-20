using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

public class UnitAI_EmbalmingPriest : UnitAI_Infantry {
	[Header("Embalming Priest References and Values")]
	public BuffDebuff embalmDebuff;
	private List<UnitAI> debuffedEnemies;
	private GameObject embalmDebuffObj;
	[SerializeField] private GameObject attackFx;

	[Header("Audio")]
	[FMODUnity.EventRef] [SerializeField] private string attackEvent;

	// Use this for initialization
	void Start() {
		self = gameObject;

		enemiesInRange = new List<UnitAI>();

		embalmDebuffObj = embalmDebuff.gameObject;

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
				if (!enemyAI.is_debuffEmbalm) {
					BuffDebuff debuff = Instantiate(embalmDebuff);
					debuff.ApplyEffect(teamPlayerKey, enemyAI);
				}
				else {
					enemyAI.activeEffects.RemoveAll(x => x == null);
					for (int i = 0; i < enemyAI.activeEffects.Count; i++) {
						if (enemyAI.activeEffects[i].type == BuffDebuff.BuffsAndDebuffs.embalm) {
							enemyAI.activeEffects[i].GetComponent<Embalm>().RefreshDuration();
						}
					}
				}

				Instantiate(attackFx, enemy.transform.position, Quaternion.identity, enemy.transform);
				if (enemyAI.takeDamage(damage * damageModifier, this, Constants.damageSource.unit)) {
					SonsOfRa.Events.GameEvents.InvokeUnitDealDamage(this, enemyAI, damage * damageModifier);
				}

				sound_attack();

				if (enemyAI.getHealth() <= 0) {
					ClearEnemies();
				}
			}
		}
		else {
			ClearEnemies();
		}
	}

	// Apply embalm debuff effect to melee attackers
	public override bool takeDamage(float _damageDone, UnitAI _attacker, Constants.damageSource source) {
		// only apply embalm to melee attackers
		if (_attacker.type != Constants.unitType.archer && _attacker.type != Constants.unitType.embalmPriest) {
			if (enemyAI && !enemyAI.is_debuffEmbalm) {
				BuffDebuff debuff = Instantiate(embalmDebuff);
				debuff.ApplyEffect(teamPlayerKey, _attacker);
			}
		}

		return base.takeDamage(_damageDone, _attacker, source);
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
		//s.archerDraw(soundPlayer, transform.position.z);
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
