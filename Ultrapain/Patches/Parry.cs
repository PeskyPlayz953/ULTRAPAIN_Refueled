﻿using HarmonyLib;
using UnityEngine;

namespace Ultrapain.Patches
{
    class GrenadeParriedFlag : MonoBehaviour
    {
        public int parryCount = 1;
        public bool registeredStyle = false;
        public bool bigExplosionOverride = false;
        public GameObject temporaryExplosion;
        public GameObject temporaryBigExplosion;
        public GameObject weapon;

        public enum GrenadeType
        {
            Core,
            Rocket,
        }

        public GrenadeType grenadeType;
    }

    class Punch_TryParryProjectile_Patch
    {
        static bool Prefix(Punch __instance, ref Transform target)
        {
            Grenade grn = target.GetComponent<Grenade>();
            if(grn != null)
            {
                if (grn.rocket && !ConfigManager.rocketBoostToggle.value)
                    return true;
                if (!ConfigManager.grenadeBoostToggle.value || __instance.alreadyBoostedProjectile == true)
                    return true;

                MonoSingleton<TimeController>.Instance.ParryFlash();
                grn.transform.LookAt(Camera.main.transform.position + Camera.main.transform.forward * 100.0f);

                Rigidbody rb = grn.GetComponent<Rigidbody>();
                rb.velocity = Vector3.zero;
                rb.AddRelativeForce(Vector3.forward * Mathf.Max(Plugin.MinGrenadeParryVelocity, rb.velocity.magnitude), ForceMode.VelocityChange);
                rb.velocity = grn.transform.forward * Mathf.Max(Plugin.MinGrenadeParryVelocity, rb.velocity.magnitude);

                GrenadeParriedFlag flag = grn.GetComponent<GrenadeParriedFlag>();
                if (flag != null)
                    flag.parryCount += 1;
                else
                {
                    flag = grn.gameObject.AddComponent<GrenadeParriedFlag>();
                    flag.grenadeType = (grn.rocket) ? GrenadeParriedFlag.GrenadeType.Rocket : GrenadeParriedFlag.GrenadeType.Core;
                    flag.weapon = MonoSingleton<GunControl>.Instance.currentWeapon;
                }

                grn.rocketSpeed *= 1f + ConfigManager.rocketBoostSpeedMultiplierPerHit.value;
                __instance.alreadyBoostedProjectile = true;
                return false;
            }

            return true;
        }
    }

    class Grenade_Explode_Patch1
    {
        static bool Prefix(Grenade __instance, ref bool ___exploded)
        {
            GrenadeParriedFlag flag = __instance.GetComponent<GrenadeParriedFlag>();
            if (flag == null)
                return true;

            if (__instance.rocket)
            {
                bool rocketParried = flag != null;

                flag.temporaryBigExplosion = GameObject.Instantiate(__instance.superExplosion, new Vector3(1000000, 1000000, 1000000), Quaternion.identity);
                __instance.superExplosion = flag.temporaryBigExplosion;
                foreach (Explosion e in __instance.superExplosion.GetComponentsInChildren<Explosion>())
                {
                    e.speed *= 1f + ConfigManager.rocketBoostSizeMultiplierPerHit.value * flag.parryCount;
                    e.damage *= (int)(1f + ConfigManager.rocketBoostDamageMultiplierPerHit.value * flag.parryCount);
                    e.maxSize *= 1f + ConfigManager.rocketBoostSizeMultiplierPerHit.value * flag.parryCount;
                }

                flag.temporaryExplosion = GameObject.Instantiate(__instance.explosion, new Vector3(1000000, 1000000, 1000000), Quaternion.identity);
                __instance.explosion = flag.temporaryExplosion;
                if (rocketParried/* && rocketHitGround*/)
                {
                    GrenadeParriedFlag fFlag = __instance.explosion.gameObject.AddComponent<GrenadeParriedFlag>();
                    fFlag.weapon = flag.weapon;
                    fFlag.grenadeType = GrenadeParriedFlag.GrenadeType.Rocket;
                    fFlag.parryCount = flag.parryCount;
                }

                foreach (Explosion e in __instance.explosion.GetComponentsInChildren<Explosion>())
                {
                    e.speed *= 1f + ConfigManager.rocketBoostSizeMultiplierPerHit.value * flag.parryCount;
                    e.damage *= (int)(1f + ConfigManager.rocketBoostDamageMultiplierPerHit.value * flag.parryCount);
                    e.maxSize *= 1f + ConfigManager.rocketBoostSizeMultiplierPerHit.value * flag.parryCount;
                }
            }
            else
            {
                if (flag != null/* && flag.bigExplosionOverride*/)
                {
                    GameObject explosion = GameObject.Instantiate(__instance.superExplosion);
                    foreach(Explosion exp in explosion.GetComponentsInChildren<Explosion>())
                    {
                        exp.damage = (int)(exp.damage * ConfigManager.grenadeBoostDamageMultiplier.value);
                        exp.maxSize *= ConfigManager.grenadeBoostSizeMultiplier.value;
                        exp.speed *= ConfigManager.grenadeBoostSizeMultiplier.value;
                    }
                    __instance.superExplosion = explosion;
                    __instance.explosion = explosion;
                    flag.temporaryBigExplosion = explosion;
                }
            }

            return true;
        }

        static void Postfix(Grenade __instance, ref bool ___exploded)
        {
            GrenadeParriedFlag flag = __instance.GetComponent<GrenadeParriedFlag>();
            if (flag == null)
                return;

            if (__instance.rocket)
            {
                if (flag.temporaryExplosion != null)
                {
                    GameObject.Destroy(flag.temporaryExplosion);
                    flag.temporaryExplosion = null;
                }
                if (flag.temporaryBigExplosion != null)
                {
                    GameObject.Destroy(flag.temporaryBigExplosion);
                    flag.temporaryBigExplosion = null;
                }
            }
            else
            {
                if (flag.temporaryBigExplosion != null)
                {
                    GameObject.Destroy(flag.temporaryBigExplosion);
                    flag.temporaryBigExplosion = null;
                }
            }
        }
    }

    class Grenade_Collision_Patch
    {
        static float lastTime = 0;

        static bool Prefix(Grenade __instance, ref Collider other)
        {
            GrenadeParriedFlag flag = __instance.GetComponent<GrenadeParriedFlag>();
            if (flag == null)
                return true;

            //if (!Plugin.ultrapainDifficulty || !ConfigManager.playerTweakToggle.value || !ConfigManager.grenadeBoostToggle.value)
            //    return true;

            if (other.gameObject.layer != 14 && other.gameObject.layer != 20)
            {
                EnemyIdentifierIdentifier enemyIdentifierIdentifier;
                if ((other.gameObject.layer == 11 || other.gameObject.layer == 10) && other.TryGetComponent<EnemyIdentifierIdentifier>(out enemyIdentifierIdentifier) && enemyIdentifierIdentifier.eid)
                {
                    if (enemyIdentifierIdentifier.eid.enemyType != EnemyType.MaliciousFace && flag.grenadeType == GrenadeParriedFlag.GrenadeType.Core && (Time.time - lastTime >= 0.25f || lastTime < 0))
                    {
                        lastTime = Time.time;
                        flag.bigExplosionOverride = true;

                        MonoSingleton<StyleHUD>.Instance.AddPoints(ConfigManager.grenadeBoostStylePoints.value, ConfigManager.grenadeBoostStyleText.guid, MonoSingleton<GunControl>.Instance.currentWeapon, null);
                    }
                }
            }

            return true;
        }
    }

    class Explosion_Collide_Patch
    {
        static float lastTime = 0;

        static bool Prefix(Explosion __instance, ref Collider other)
        {
            GrenadeParriedFlag flag = __instance.gameObject.GetComponent<GrenadeParriedFlag>();
            if (flag == null || flag.registeredStyle)
                return true;

            if (!flag.registeredStyle && other.gameObject.tag != "Player" && (other.gameObject.layer == 10 || other.gameObject.layer == 11))
            {
                EnemyIdentifierIdentifier componentInParent = other.GetComponentInParent<EnemyIdentifierIdentifier>();
                if(flag.grenadeType == GrenadeParriedFlag.GrenadeType.Rocket && componentInParent != null && componentInParent.eid != null && !componentInParent.eid.blessed && !componentInParent.eid.dead && (Time.time - lastTime >= 0.25f || lastTime < 0))
                {
                    flag.registeredStyle = true;
                    lastTime = Time.time;
                    MonoSingleton<StyleHUD>.Instance.AddPoints(ConfigManager.rocketBoostStylePoints.value, ConfigManager.rocketBoostStyleText.guid, flag.weapon, null, flag.parryCount);
                }
            }

            return true;
        }
    }
}
