﻿using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AI;

public class AI_Enemy : ControllerMechanics {

    GameObject overlord_empty;
    AI_Controller overlord;
    ControllerMechanics controller_mechanics;
    NavMeshAgent agent;

    List<GameObject> enemy_spawns;
    List<GameObject> good_spawns;

    public bool active = true;

    public enum state { roam, patrol, chase, investigate, run, idle };
    state currentstate;

    GameObject target;

    float sight_range = 20f;
    float sight_angle = 90f;

    public void Restart()
    {
        //respawn self
        Start();
    }

    void Start()
    {
        overlord_empty = GameObject.Find("EnemyOverlord_Empty");
        overlord = overlord_empty.GetComponent<AI_Controller>();
        agent = GetComponent<NavMeshAgent>();

        currentstate = state.roam;

        target = null;

        overlord.AddToList(gameObject);

        //Get spawns
        switch (currentteam)
        {
            case Team.blue:
                enemy_spawns = overlord.GetSpawns("red");
                good_spawns = overlord.GetSpawns("blue");
                break;
            case Team.red:
                enemy_spawns = overlord.GetSpawns("blue");
                good_spawns = overlord.GetSpawns("red");
                break;
            default:
                enemy_spawns = overlord.GetSpawns("red");
                enemy_spawns = overlord.GetSpawns("blue");
                good_spawns.AddRange(overlord.GetSpawns("red"));
                good_spawns.AddRange(overlord.GetSpawns("blue"));
                break;
        }

        //START Slow Update
        StartCoroutine(FifthUpdate());
    }

    IEnumerator FifthUpdate()
    {
        while (true)
        {
            if (active)
            {
                Vector3 direction = new Vector3(0, 0, 0);

                if (currentstate == state.chase)
                {
                    //rotate towards target if proper state
                    if (target != null)
                    {
                        direction = (target.transform.position - transform.position).normalized;
                    }
                    else
                    {
                        direction = (agent.destination - transform.position).normalized;
                    }
                }
                else //rotate towards running direction
                {
                    direction = (agent.steeringTarget - transform.position).normalized;
                }

                // >>>
                // Insert LERP HERE WOOOOO

                transform.rotation = Quaternion.LookRotation(direction, Vector3.up);
                transform.rotation = Quaternion.Euler(0f, transform.rotation.eulerAngles.y, 0f);

                yield return new WaitForSeconds(0.2f);
            }
        }
    }

    //Done on a QuarterUpdate in AI_Controller
    public void DecideCurrentState()
    {
        if (target != null)
        {
            
        }

        switch (currentstate)
        {
            case state.roam: Roam(); break;
            case state.patrol: Patrol(); break;
            case state.chase: Chase(); break;
            case state.investigate: Investigate(); break;
            case state.run: Run(); break;
            case state.idle: Idle(); break;

            default: currentstate = state.idle; break;
        }
    }

    public void TakeDamage(float amount, Vector3 pos)
    {
        TakeDamageThroughController(amount);

        //Alert enemy to attacker presence
    }

    #region Actions

    //
    // ACTIONS
    //

    // Do things

    void ShootWeapon()
    {
        if (target != null)
        {
            var weapon_mechanics = GetComponent<WeaponMechanics>();
            if (weapon_mechanics)
            {
                var weapon = weapon_mechanics.weapon_type;
                if (weapon)
                {
                    //get aim position
                    var aimPosition = target.transform.position;

                    weapon.Fire(aimPosition);
                }
            }
        }
    }

    List<GameObject> RemoveSameTeam(List<GameObject> list, Team good_team)
    {
        List<GameObject> opponent_enemies = new List<GameObject>();
        foreach (GameObject enemy in list)
        {
            var ai = enemy.GetComponent<AI_Enemy>();
            if (ai)
            {
                if (ai.currentteam != good_team)
                {
                    opponent_enemies.Add(enemy);
                }
            }
            else
            {
                //check for playercontroller

                var pc = enemy.GetComponent<PlayerController>();
                if (pc)
                {
                    if (pc.currentteam != good_team)
                    {
                        opponent_enemies.Add(enemy);
                    }
                }
            }
        }

        return opponent_enemies;
    }

    #endregion

    #region Checks

    //
    // CHECKS
    //

    // The check-all solution!

    bool CanStillSeeTarget(GameObject the_target)
    {
        return CheckRaycast(transform, the_target.transform.position);
    }

    bool CheckForEnemies()
    {
        List<GameObject> ai_enemies = overlord.GetAllEnemies();
        ai_enemies = RemoveSameTeam(ai_enemies, currentteam);
        
        //If there are enemies
        if (ai_enemies.Count > 0)
        {
            List<GameObject> range_enemies = new List<GameObject>();

            foreach (GameObject enemy in ai_enemies)
            {
                if (enemy != this)
                {
                    if (CheckRange(transform.position, enemy.transform.position, sight_range))
                    {
                        range_enemies.Add(enemy);
                    }
                }
            }

            //If there are enemies in range
            if (range_enemies.Count > 0)
            {
                foreach (GameObject enemy in range_enemies)
                {
                    if (enemy != this)
                    {
                        //If they are in vision angle
                        var temp_target = GetClosestEnemy(range_enemies);
                        if (CheckVisionAngle(gameObject, temp_target.gameObject, sight_angle))
                        {
                            //If they are in line of sight
                            if (CheckRaycast(transform, temp_target.transform.position))
                            {
                                target = temp_target;
                                ChangeState(state.chase);
                                print(target.name + " HAS BEEN FOUND BY " + name);
                                return true;
                            }
                            else
                            {
                                print(name + " failed Raycast check");
                            }
                        }
                        else
                        {
                            print(name + " failed VisionAngle check");
                        }
                    }
                }
            }
        }

        //if you get this far, you didn't find a target
        return false;
    }

    bool CheckRange(Vector3 a, Vector3 b, float distance) //magnitude between two points is <= c distance
    {
        if ((a - b).magnitude <= distance)
        {
            return true;
        }
        else
        {
            return false;
        }
    }

    bool CheckVisionAngle(GameObject a, GameObject b, float angle) //line of sight from object a to object b at c angle
    {
        Vector3 direction = (b.transform.position - a.transform.position);
        float arc_angle = Vector3.Angle(direction, a.transform.forward);

        //print(direction.ToString() + " is direction");
        if (arc_angle <= angle)
        {
            return true;
        }
        else
        {
            return false;
        }
        
    }

    bool CheckRaycast(Transform a, Vector3 b) //raycast check from a to b
    {
        var ReachesTarget = false;

        var direction = (b - a.transform.position).normalized;
        //var direction = (b - a).normalized;
        //direction = direction / direction.magnitude;

        Debug.DrawRay(a.transform.position, direction, Color.white, 0.2f);
        RaycastHit hit;

        Color color = Color.green;

        if (Physics.Raycast(a.transform.position + direction, direction, out hit, sight_range))
        {
            if (hit.collider.tag == "Player")
            {
                ReachesTarget = true;
            }
            else if (hit.collider.tag == "Enemy")
            {
                ReachesTarget = true;
            }
            else
            {
                ReachesTarget = false;
                color = Color.red;
                //wrong thing was hit on path
            }
            
            //something got hit
        }
        else
        {
            ReachesTarget = false;
            color = Color.red;
            //nothing was hit on path
        }
        Debug.DrawRay(a.transform.position, direction * sight_range, color, 0.1f);

        return ReachesTarget;
    }

    GameObject GetClosestEnemy(List<GameObject> enemies)
    {
        var range_to_beat = sight_range + 1;
        var enemy_to_beat = enemies[0];
        foreach (GameObject enemy in enemies)
        {
            var distance = (enemy.transform.position - transform.position).magnitude;
            if (distance < range_to_beat)
            {
                range_to_beat = distance;
                enemy_to_beat = enemy;
            }
        }
        return enemy_to_beat;
    }

    #endregion

    #region States

    //
    // STATES
    //

    // Follow systems and algorithms

    void Roam() //running around looking for battle
    {
        //If you can't find any enemies to target
        if (!CheckForEnemies())
        {
            if ((agent.destination - transform.position).magnitude < 5f)
            {
                agent.SetDestination(enemy_spawns[Random.Range(0, enemy_spawns.Count)].transform.position);
            }
        }
    }

    void Patrol() //defending and watching out for enemies
    {
        if (!CheckForEnemies())
        {
            if ((agent.destination - transform.position).magnitude < 5f)
            {
                agent.SetDestination(good_spawns[Random.Range(0, good_spawns.Count)].transform.position);
            }
        }
    }
	
    void Chase() //chasing an enemy they've spotted
    {
        //make sure we actually see player still
        if (target != null)
        {
            if (CanStillSeeTarget(target))
            {
                //reset destination to follow them.
                var direction = (target.transform.position - transform.position).normalized;

                agent.SetDestination(target.transform.position - (direction * 5));
                ShootWeapon();
            }
            else
            {
                ChangeState(state.investigate);
                return;
            }
        }
        else
        {
            ChangeState(state.roam);
            return;
        }
    }

    void Investigate() //investigate around last known target area
    {
        if (!CheckForEnemies())
        {
            if ((agent.destination - transform.position).magnitude < 5f)
            {
                ChangeState(state.roam);
            }
        }
    }

    void Run() //running away from battle
    {

    }

    void Idle()
    {

    }

    void ChangeState(state newstate)
    {
        print("Changing state from " + currentstate.ToString() + " to " + newstate.ToString());
        currentstate = newstate;
    }

    #endregion

    public string ReturnStateString()
    {
        return currentstate.ToString();
    }

}