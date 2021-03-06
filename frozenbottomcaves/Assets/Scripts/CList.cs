using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class CList
{
    public GameObject entity;
    public bool move;
    public Vector3 movTar;
    public List<Vector3> atkTar;
    public int dir, attack, attackDmg, hp;
    public int gridX, gridY;

    public CList(GameObject newEntity)
    {
        entity = newEntity;
        move = false;
        movTar = new Vector3(0, 0, 0);
        dir = 0;
        attack = -1; // stand in before AI is choosing attack or move. attack will be a number based on attack type. not attacking = -1
        hp = 10; // stand in before calling entity hp
        attackDmg = 5;
        atkTar = new List<Vector3>();
        gridX = 0;
        gridY = 0;
    }

    public void Print()
    {
        Debug.Log("entity: " + this.entity);
        Debug.Log("gridX: " + this.gridX);
        Debug.Log("gridY: " + this.gridY);
        Debug.Log("move: " + this.move);
        Debug.Log("movTar: " + this.movTar);
        Debug.Log("atkTar: " + this.atkTar);
        Debug.Log("dir: " + this.dir);
        Debug.Log("attack: " + this.attack);
        Debug.Log("attackDmg: " + this.attackDmg);
        Debug.Log("hp: " + this.hp);
    }
}
