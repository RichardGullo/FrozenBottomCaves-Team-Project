using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;
// using UnityEngine.UI;
using UnityEngine.UIElements;
using UnityEngine.Tilemaps;
using System.Drawing;

public class OverworldManager : MonoBehaviour
{
    private struct nodeInfo
    {
        public int count, physNodeIndex;
        public WorldNode curNode;
        public GameObject physNode;
    }

    public GameManager gm;
    public GameObject player;
    public DialogueManager dm;
    public bool playerSpawned, initialSave;
    public GameObject rollParchment;
    public RollMaster rollScript;
    public GameObject die1, die2;
    public DiceRoller dr1, dr2;
    public bool updating;
    private overworldAnimations oa;
    public bool[,] pathingGrid;
    public Dictionary<Vector3, List<int>> nodeMap; // V3 associated with list of nodes there
    public Dictionary<int, Point> pathMap;  // NodeID associated with x,y coord in pathfinding array
    public Dictionary<Point, Vector3> pointToVector; // Each point gets a Vector3
    public int playerX, playerY;
    public GameObject cam, OWEntities, OWEntitiesSave;
    public Transform EntitiesParent;

    public int startingNode;
    public List<GameObject> nodes;
    public int playerNodeId;
    public DialogueNode curDialogueNode;
    public List<FlagType> overworldEvent;
    public List<int> overworldEventEffect;
    public List<Item.ItemType> overworldEventItemGained;
    public List<int> overworldEventItemGainedAmount;
    public List<Item.ItemType> overworldEventItemLost;
    public List<int> overworldEventItemLostAmount;
    public int overworldEventSkillCheckDifficulty;
    public int nodeTypeCount;
    public bool load;
    public List<int> nodeSavedAt;
    public bool dontKillBMYet = false;

    public int saveNode, saveNodeTypeCount, saveCurrentNode, facing, saveX, saveY;

    private Vector3 destPos;
    //private float speed = .20f, startTime, journeyLength;
    //private bool destReached;

    WorldNode curNode;

    private void Awake()
    {
        DontDestroyOnLoad(this.gameObject);
    }

    // Start is called before the first frame update
    void Start()
    {
        this.oa = this.gameObject.AddComponent<overworldAnimations>();
        this.oa.om = this;
        playerSpawned = false;
        this.gm = GameObject.Find("GameManager").GetComponent<GameManager>();
        //destReached = true;
        startingNode = this.gm.startingNode;
        nodeSavedAt = new List<int>();
        nodeMap = new Dictionary<Vector3, List<int>>();
        pathMap = new Dictionary<int, Point>();
        pointToVector = new Dictionary<Point, Vector3>();
        this.overworldEvent = new List<FlagType>();
        this.overworldEventEffect = new List<int>();
        this.overworldEventItemGained = new List<Item.ItemType>();
        this.overworldEventItemGainedAmount = new List<int>();
        this.overworldEventItemLost = new List<Item.ItemType>();
        this.overworldEventItemLostAmount = new List<int>();
    }

    // Update is called once per frame
    void Update()
    {
        // If we're in the overworld for the first time, plop the player character in
        if (SceneManager.GetActiveScene().name == "Overworld" && !playerSpawned)
        {
        	this.dm = GameObject.Find("DialogueManager").GetComponent<DialogueManager>();
            this.dm.om = this;
	        this.playerNodeId = this.startingNode;
	        //Debug.Log("OverworldManager sees the player at " + this.playerNodeId);

        	nodes = new List<GameObject>();

	        foreach (GameObject n in GameObject.FindGameObjectsWithTag("OWNode"))
	        {
	        	nodes.Add(n);
                nodeMap.Add(n.transform.position, n.GetComponent<WorldNode>().NodeIDs);
	        }

            GameObject tile = GameObject.Find("OverworldTilemap");
            Tilemap tilemap = tile.GetComponent<Tilemap>();
            //Tilemap obstaclesMap = tilemap.transform.GetChild(0).GetComponent<Tilemap>();
            BoundsInt bounds = tilemap.cellBounds;
            //print("SIZE: " + tilemap.size);
            //print("x: " + (Mathf.Abs(bounds.x) + bounds.xMax) + " y: " + (Mathf.Abs(bounds.y) + bounds.yMax));

            spawnPlayer();
            generatePathingGrid();
            OWEntities = GameObject.Find("OWEntities");
            EntitiesParent = OWEntities.transform;
            OWEntitiesSave = Instantiate(OWEntities);
            OWEntitiesSave.transform.SetParent(OWEntities.transform.parent);
            OWEntitiesSave.SetActive(false);

        }

        // Creates save at node 0
        if (playerSpawned && !initialSave)
        {
            if (this.dm.currentNode == 0)
            {
                nodeSavedAt.Add(0);

                // Tell the pm to create a deep copy of the current player and inventory
                this.gm.pm.createSave();

                // Remember the node information
                this.saveNode = 0;
                this.saveNodeTypeCount = 0;
                this.saveCurrentNode = 0;
                this.saveX = playerX;
                this.saveY = playerY;

                // Save direction they're facing
                if (player.transform.GetChild(0).gameObject.activeSelf)
                    facing = 0;
                else if (player.transform.GetChild(1).gameObject.activeSelf)
                    facing = 1;
                else if (player.transform.GetChild(2).gameObject.activeSelf)
                    facing = 2;
                else if (player.transform.GetChild(3).gameObject.activeSelf)
                    facing = 3;
                initialSave = true;
            }
        }

        if (!updating && playerSpawned && !this.gm.pm.PLAYERDEAD)
        {
            updating = true;
            StartCoroutine(overworldUpdate());
        }
    }

    private nodeInfo getCurrentNode(int id)
    {
        nodeInfo node = new nodeInfo();
        for (int i = 0; i < nodes.Count; i++)
        {
            this.nodeTypeCount = 0;
            GameObject n = nodes[i];
            WorldNode tempNode = n.GetComponent<WorldNode>();
            foreach (int ID in tempNode.NodeIDs)
            {
                if (ID == this.dm.currentNode)
                {
                    node.count = nodeTypeCount;
                    node.curNode = tempNode;
                    node.physNode = n;
                    node.physNodeIndex = i;
                }
                nodeTypeCount++;
            }
        }
        return node;
    }

    IEnumerator overworldUpdate()
    {
        // Wait until the new node has been selected
        while (this.playerNodeId == this.dm.currentNode)
        {
            yield return null;
        }

        // dm current node has changed, grab it's information
        nodeInfo n = getCurrentNode(this.dm.currentNode);

        // If there is some sort of animation/sfx to play BEFORE pathfinding, do it here
        yield return StartCoroutine(this.oa.EarlyEvents(this.dm.currentNode));

        if (this.dm.currentNode == -1)
        { 
            this.HPEvent(-(this.gm.pm.pc.health));
            yield break;
        }

        // If the node position is not where the player is, move to it
        if (player.transform.position != n.physNode.transform.position)
        {
            //print(playerX + " " + playerY);
            List<Point> movementPoints = BFS.bfsPath(this.pathingGrid, new Point(playerX, playerY), pathMap[this.dm.currentNode]);
            //foreach (Point p in movementPoints)
            //{
            //    print(p);
            //}

            for (int i = movementPoints.Count - 1; i >= 0; i--)
            {
                yield return StartCoroutine(slerpTest(pointToVector[movementPoints[i]]));

                playerX = movementPoints[i].X;
                playerY = movementPoints[i].Y;
            }
            yield return new WaitForSeconds(.7f);
        }

        // Update player's node id after moving there
        this.playerNodeId = this.dm.currentNode;
        this.curDialogueNode = this.dm.dialogue.nodes[this.playerNodeId];
        
        // If there is some sort of animation/sfx to play AFTER pathfinding, do it here
        yield return StartCoroutine(this.oa.LateEvents(this.dm.currentNode));

        // Event conversion from string to FlagType
        //print("This event list: " + this.curDialogueNode.overworldEvent);

        if (this.curDialogueNode.overworldEvent.Count > 0)
            for (int i = 0; i < this.curDialogueNode.overworldEvent.Count; i++)
                if (this.curDialogueNode.overworldEvent[i] != "")
                {
                    //print("This event: " + this.curDialogueNode.overworldEvent[i]);
                    this.overworldEvent.Add((FlagType)FlagType.Parse(typeof(FlagType), this.curDialogueNode.overworldEvent[i], true));
                }
                else
                    this.overworldEvent.Add(FlagType.NONE);

        // Items Gained conversion from string to Item.ItemType Enum
        if (this.curDialogueNode.itemGained.Count > 0)
            for (int i = 0; i < this.curDialogueNode.itemGained.Count; i++)
                if (this.curDialogueNode.itemGained[i] != "")
                {
                    //print("This Item Gained: " + this.curDialogueNode.itemGained[i]);
                    this.overworldEventItemGained.Add((Item.ItemType)Item.ItemType.Parse(typeof(Item.ItemType), this.curDialogueNode.itemGained[i], true));
                }
                else
                    this.overworldEventItemGained.Add(Item.ItemType.NONE);

        // Item Gained Amount
        if (this.curDialogueNode.itemGained.Count > 0)
            for (int i = 0; i < this.curDialogueNode.itemGained.Count; i++)
            {
                    //print("This Item Gained Amount: " + this.curDialogueNode.itemGainedAmount[i]);
                    this.overworldEventItemGainedAmount.Add(this.curDialogueNode.itemGainedAmount[i]);
            }

        // Items Lost conversion from string to Item.ItemType Enum
        if (this.curDialogueNode.itemLost.Count > 0)
            for (int i = 0; i < this.curDialogueNode.itemLost.Count; i++)
                if (this.curDialogueNode.itemLost[i] != "")
                {
                    //print("This Item Lost: " + this.curDialogueNode.itemLost[i]);
                    this.overworldEventItemLost.Add((Item.ItemType)Item.ItemType.Parse(typeof(Item.ItemType), this.curDialogueNode.itemLost[i], true));
                }
                else
                    this.overworldEventItemLost.Add(Item.ItemType.NONE);

        // Item Lost Amount
        if (this.curDialogueNode.itemLostAmount.Count > 0)
            for (int i = 0; i < this.curDialogueNode.itemLostAmount.Count; i++)
            {
                //print("This Item Gained Amount: " + this.curDialogueNode.itemLostAmount[i]);
                this.overworldEventItemLostAmount.Add(this.curDialogueNode.itemLostAmount[i]);
            }

        this.overworldEventEffect = this.curDialogueNode.effect;
        this.overworldEventSkillCheckDifficulty = this.curDialogueNode.skillCheckDifficulty;


        this.dm.Panel.SetActive(true);
        this.dm.setInitialSelection();

        //print("There are " + this.overworldEvent.Count + " event(s)");
        // If no events
        if (this.overworldEvent.Count == 0)
        {
            this.overworldEvent.Clear();
            this.overworldEventEffect.Clear();
            this.overworldEventItemGained.Clear();
            this.overworldEventItemGainedAmount.Clear();
            this.overworldEventItemLost.Clear();
            this.overworldEventItemLostAmount.Clear();
            updating = false;
            yield break;
        }


        for (int i = 0; i < this.overworldEvent.Count; i++)
        {
            //print("Current event: " + this.overworldEvent[i]);

            if (this.overworldEvent[i] == FlagType.NONE)
                continue;

            if (this.overworldEvent[i] == FlagType.BATTLE)
            {
                //print("entered combat");
                if (!this.gm.debug)
                {
                    yield return StartCoroutine(BattleEvent());
                    break;
                }
            }

            if (this.overworldEvent[i] == FlagType.STRSKILL)
            {
                //print("entered str event");
                if (!this.gm.debug)
                {
                    this.dm.putCanvasBehind();
                    yield return StartCoroutine(SkillSaveEventCR("STR", n.physNode, this.overworldEventSkillCheckDifficulty));
                    this.dm.putCanvasInFront();
                }
            }
            if (this.overworldEvent[i] == FlagType.INTSKILL)
            {
                //print("entered int event");
                if (!this.gm.debug)
                {
                    this.dm.putCanvasBehind();
                    yield return StartCoroutine(SkillSaveEventCR("INT", n.physNode, this.overworldEventSkillCheckDifficulty));
                    this.dm.putCanvasInFront();
                }
            }
            if (this.overworldEvent[i] == FlagType.AGISKILL)
            {
                //print("entered agi event");
                if (!this.gm.debug)
                {
                    this.dm.putCanvasBehind();
                    yield return StartCoroutine(SkillSaveEventCR("AGI", n.physNode, this.overworldEventSkillCheckDifficulty));
                    this.dm.putCanvasInFront();
                }
            }

            if (this.overworldEvent[i] == FlagType.HPCHANGE)
            {
                //print("Hp event");
                this.HPEvent(this.overworldEventEffect[i]);
            }

            if (this.overworldEvent[i] == FlagType.ITEMGAINED)
            {
                //print("Getting item(s)");
                this.ItemGet(this.overworldEventItemGained[i], this.overworldEventItemGainedAmount[i]);
            }

            if (this.overworldEvent[i] == FlagType.ITEMLOST)
            {
                //print("Losing item(s)");
                this.ItemRemove(this.overworldEventItemLost[i], this.overworldEventItemLostAmount[i]);
                //this.ItemRemove(n.curNode.NodeItemsLose[n.count].item, n.curNode.NodeItemsLose[n.count].count);
            }

            if (this.overworldEvent[i] == FlagType.HPMAXCHANGE)
            {
                //print("HP MAX event");
                this.HPMaxEvent(this.overworldEventEffect[i]);
            }

            if (this.overworldEvent[i] == FlagType.SAVE)
            {
                if (!nodeSavedAt.Contains(n.curNode.NodeIDs[n.count]))
                {
                    nodeSavedAt.Add(n.curNode.NodeIDs[n.count]);

                    // Tell the pm to create a deep copy of the current player and inventory
                    this.gm.pm.createSave();

                    // Remember the node information
                    this.saveNode = n.physNodeIndex;
                    this.saveNodeTypeCount = n.count;
                    this.saveCurrentNode = this.dm.currentNode;
                    this.saveX = playerX;
                    this.saveY = playerY;

                    // Save direction they're facing
                    if (player.transform.GetChild(0).gameObject.activeSelf)
                        facing = 0;
                    else if (player.transform.GetChild(1).gameObject.activeSelf)
                        facing = 1;
                    else if (player.transform.GetChild(2).gameObject.activeSelf)
                        facing = 2;
                    else if (player.transform.GetChild(3).gameObject.activeSelf)
                        facing = 3;

                    DestroyImmediate(OWEntitiesSave);
                    OWEntitiesSave = Instantiate(OWEntities);
                    OWEntitiesSave.transform.SetParent(OWEntities.transform.parent);
                    OWEntitiesSave.SetActive(false);
                }
            }
        }

        this.overworldEvent.Clear();
        this.overworldEventEffect.Clear();
        this.overworldEventItemGained.Clear();
        this.overworldEventItemGainedAmount.Clear();
        this.overworldEventItemLost.Clear();
        this.overworldEventItemLostAmount.Clear();

        updating = false;

        yield break;
    }

    // Slides to destination node at constant speed
    IEnumerator moveToDest(Vector3 dest)
    {
        this.dm.Panel.SetActive(false);
        TurnPlayer(player, dest);
        while (player.transform.position != dest)
        {
            player.transform.position = Vector3.MoveTowards(player.transform.position, dest, 5f * Time.deltaTime);
            yield return null;
        }
        yield break;
    }

    // Hops on over to the destination node
    IEnumerator slerpTest(Vector3 dest)
    {
        TurnPlayer(player, dest);
        Vector3 start = player.transform.position;
        float startTime = Time.time;
        float journeyTime = .275f;

        //print(start + " end " + dest);

        while (player.transform.position != dest)
        {
            Vector3 center = (start + dest) * 0.5f;

            center -= new Vector3(0, .35f, 0);

            Vector3 playerRelCenter = start - center;
            Vector3 endRelCenter = dest - center;

            float fracComplete = (Time.time - startTime) / journeyTime;

            player.transform.position = Vector3.Slerp(playerRelCenter, endRelCenter, fracComplete);
            player.transform.position += center;

            yield return null;
        }

        float randPieceSound = Random.value;
        if (randPieceSound < 0.25f)
            this.gm.sm.effectChannel.PlayOneShot(this.gm.sm.pieceLanding1, this.gm.sm.effectsVolume);
        else if (randPieceSound < 0.5f)
            this.gm.sm.effectChannel.PlayOneShot(this.gm.sm.pieceLanding2, this.gm.sm.effectsVolume);
        else if (randPieceSound < 0.75f)
            this.gm.sm.effectChannel.PlayOneShot(this.gm.sm.pieceLanding3, this.gm.sm.effectsVolume);
        else
            this.gm.sm.effectChannel.PlayOneShot(this.gm.sm.pieceLanding4, this.gm.sm.effectsVolume);

        yield break;
    }

    public void loadSave()
    {
        this.overworldEvent.Clear();
        this.overworldEventEffect.Clear();
        this.overworldEventItemGained.Clear();
        this.overworldEventItemGainedAmount.Clear();
        this.overworldEventItemLost.Clear();
        this.overworldEventItemLostAmount.Clear();

        this.gm.pm.loadSave();
        this.dm.currentNode = this.saveCurrentNode;
        this.startingNode = saveNode;
        this.nodeTypeCount = saveNodeTypeCount;
        this.playerX = saveX;
        this.playerY = saveY;
        this.playerNodeId = saveCurrentNode;
        this.dm.Panel.SetActive(true);
        this.dm.init();

        DestroyImmediate(OWEntities);
        OWEntitiesSave.SetActive(true);
        OWEntities = Instantiate(OWEntitiesSave);
        OWEntitiesSave.SetActive(false);
        OWEntities.name = "OWEntities";
        OWEntities.transform.SetParent(OWEntitiesSave.transform.parent);

        //print("Loaded player at DM node: " + this.dm.currentNode);
        //print("PlayerNodeID is now: " + this.playerNodeId);
        //print("Player (x,y): " + "(" + playerX + "," + playerY + ")");

        //destPos = new Vector3(nodes[startingNode].transform.position.x, nodes[startingNode].transform.position.y, nodes[startingNode].transform.position.z);
        //this.player.transform.position = new Vector3(nodes[startingNode].transform.position.x,
        //                                             nodes[startingNode].transform.position.y,
        //                                             nodes[startingNode].transform.position.z);
        this.player = GameObject.Find("TheWhiteKnight1(Clone)");

        this.player.transform.position = this.pointToVector[this.pathMap[this.dm.currentNode]];
        this.player.transform.rotation = Quaternion.identity;
        //movePlayer();
        cam.transform.position = this.player.transform.position + new Vector3(0,0,-10);
        
        for (int i = 0; i < 4; i++)
        {
            if (i == facing)
                this.player.transform.GetChild(i).gameObject.SetActive(true);
            else
                this.player.transform.GetChild(i).gameObject.SetActive(false);
        }
        this.gm.om.dm.setInterableAll();
        this.gm.om.dm.setInitialSelection();
        ButtonOverlay.Instance.inventory.interactable = true;

        updating = false;
    }

    //private void movePlayer()
    //{
    //    float distCovered = (Time.time - startTime) * speed;
    //    float fractionOfJourney = distCovered / journeyLength;
    //    player.transform.position = Vector3.Lerp(player.transform.position, destPos, fractionOfJourney);
    //}

    private void spawnPlayer()
    {
        string path = "Prefabs/PlayerCharacters/";
        path += gm.pm.pc.name;
        //print(path + " " + gm.pm.pc.name);
        this.player = Instantiate(Resources.Load(path, typeof(GameObject))) as GameObject;
        this.player.transform.position = getCurrentNode(this.playerNodeId).physNode.transform.position;
        //print("Spawning Player at Node " + this.playerNodeId + ": " + nodes[this.playerNodeId].transform.position);
        playerSpawned = true;
        cam = GameObject.Find("MainCameraOW");
        cam.GetComponent<OWCamera>().target = player.transform;
        gm.pm.initPM();


        rollParchment = GameObject.Find("RollParchment");
        rollScript = rollParchment.GetComponent<RollMaster>();
        die1 = GameObject.Find("d1");
        die2 = GameObject.Find("d2");
        dr1 = die1.GetComponent<DiceRoller>();
        dr2 = die2.GetComponent<DiceRoller>();
        rollParchment.SetActive(false);
    }


    public IEnumerator BattleEvent()
    {
        this.dm.Panel.SetActive(false);
        this.rollParchment.SetActive(false);
        this.gm.sm.setBattleMusic();
        SceneManager.LoadScene("Battleworld", LoadSceneMode.Additive);
        this.gm.pm.combatInitialized = true;
        this.gm.pm.inCombat = true;
        this.gm.jingle = false;

        yield return new WaitForSeconds(.5f);

        SceneManager.SetActiveScene(SceneManager.GetSceneByName("Battleworld"));

        yield return new WaitUntil(() => this.gm.bm.isBattleResolved() == true || this.gm.pm.PLAYERDEAD);

        // print("WE WON? " + this.gm.bm.didWeWinTheBattle());

        if (this.gm.bm.didWeWinTheBattle())
            this.dm.currentNode += 1;
        else
            this.dm.currentNode += 2;
        
        this.gm.pm.combatInitialized = false;
        this.gm.pm.inCombat = false;

        
        this.dm.Panel.SetActive(true);
        this.dm.EventComplete();

        while (SceneManager.GetSceneByName("Battleworld").IsValid())
            yield return null;
    }

    public IEnumerator SkillSaveEventCR(string stat, GameObject n, int difficulty)
    {
        //print("IN SKILL SAVE");
        this.dm.Panel.SetActive(false);
        this.rollParchment.SetActive(true);
        yield return StartCoroutine(rollScript.waitForStart(stat, this.gm.pm.getStatModifier(stat), difficulty));
        this.rollParchment.SetActive(false);

        int modifier = this.gm.pm.getStatModifier(stat);
        if (dr1.final + dr2.final + modifier < difficulty)
        {
            //print("FAIL");
            this.dm.currentNode += 2;
        }
        else
        {
            //print("SAVE");
            this.dm.currentNode += 1;
        }

        this.dm.EventComplete();
        dr1.final = 0;
        dr2.final = 0;
        yield break;
    }

    public void HPEvent(int dmg)
    {
        this.gm.pm.setHealthEvent(dmg);
    }

    public void HPMaxEvent(int change)
    {
        this.gm.pm.maxHealthEvent(change);
    }

    public void ItemGet(Item.ItemType item, int count)
    {
        this.gm.pm.pc.inventory.addItem(item, count);
    }

    public void ItemRemove(Item.ItemType item, int count)
    {
        this.gm.pm.pc.inventory.removeItem(item, count);
    }

    public GameObject GetCurrentNode()
    {
        WorldNode curWorldNode;

        if (this.nodes == null)
        {
            Debug.AssertFormat(false, "OWNodes list(nodes) has not been created yet.");
            return null;
        }

        for (int i = 0; i < this.nodes.Count; i++)
        {
            curWorldNode = this.nodes[i].GetComponent<WorldNode>();

            for (int j = 0; j < curWorldNode.NodeIDs.Count; j++)
            {
                if (curWorldNode.NodeIDs[j] == this.dm.currentNode)
                    return this.nodes[i];
            }
        }


        Debug.AssertFormat(false, "Couldn't find currentNode " + this.dm.currentNode + " in OWNodes list(nodes).");
        return null;
    }

    public BattleClass GetBattleClass()
    {
        BattleClass curBattleClass = new BattleClass();
        DialogueNode curNode = this.dm.dialogue.nodes[this.dm.currentNode];

        curBattleClass = curBattleClass.DialogueNodeToBattleClass(curNode);

        return curBattleClass;
    }

    public void TurnPlayer(GameObject entity, Vector3 movTar)
    {
        // How I WILL do it later entity.dir... maybe?
        float dirX, dirY;
        dirX = movTar.x - entity.transform.localPosition.x;
        dirY = movTar.y - entity.transform.localPosition.y;

        GameObject SE = entity.transform.GetChild(0).gameObject, SW = entity.transform.GetChild(1).gameObject,
                   NW = entity.transform.GetChild(2).gameObject, NE = entity.transform.GetChild(3).gameObject;

        if (dirX > 0)
        {
            if (dirY > 0)
            {
                SE.gameObject.SetActive(false);
                SW.gameObject.SetActive(false);
                NW.gameObject.SetActive(false);
                NE.gameObject.SetActive(true);
            }
            else
            {
                SE.gameObject.SetActive(true);
                SW.gameObject.SetActive(false);
                NW.gameObject.SetActive(false);
                NE.gameObject.SetActive(false);
            }

        }
        else if (dirX < 0)
        {
            if (dirY < 0)
            {
                SE.gameObject.SetActive(false);
                SW.gameObject.SetActive(true);
                NW.gameObject.SetActive(false);
                NE.gameObject.SetActive(false);
            }
            else
            {
                SE.gameObject.SetActive(false);
                SW.gameObject.SetActive(false);
                NW.gameObject.SetActive(true);
                NE.gameObject.SetActive(false);
            }
        }
    }

    public void TurnPlayer(GameObject entity, int dir)
    {
        GameObject SE = entity.transform.GetChild(0).gameObject, SW = entity.transform.GetChild(1).gameObject,
                   NW = entity.transform.GetChild(2).gameObject, NE = entity.transform.GetChild(3).gameObject;

        switch(dir)
        {
            case 0:
                SE.gameObject.SetActive(true);
                SW.gameObject.SetActive(false);
                NW.gameObject.SetActive(false);
                NE.gameObject.SetActive(false);
                break;
            case 1:
                SE.gameObject.SetActive(false);
                SW.gameObject.SetActive(true);
                NW.gameObject.SetActive(false);
                NE.gameObject.SetActive(false);
                break;
            case 2:
                SE.gameObject.SetActive(false);
                SW.gameObject.SetActive(false);
                NW.gameObject.SetActive(true);
                NE.gameObject.SetActive(false);
                break;
            case 3:
            default:
                SE.gameObject.SetActive(false);
                SW.gameObject.SetActive(false);
                NW.gameObject.SetActive(false);
                NE.gameObject.SetActive(true);
                break;
        }

    }

    private void generatePathingGrid()
    {
        Tilemap pathTileMap = GameObject.Find("PlayerPathing").GetComponent<Tilemap>();
        BoundsInt bounds = pathTileMap.cellBounds;
        Grid pathGrid = pathTileMap.layoutGrid;

        GameObject temp = Resources.Load("Prefabs/AttackAnimHighlight") as GameObject;

        //print("x: " + bounds.x + " y: " + bounds.y);
        //print("xM: " + bounds.xMax + " yM: " + bounds.yMax);

        pathingGrid = new bool[Mathf.Abs(bounds.x) + bounds.xMax + 1, Mathf.Abs(bounds.y) + bounds.yMax + 1];


        foreach (var position in pathTileMap.cellBounds.allPositionsWithin)
        {
            int xDif = position.x - bounds.position.x;
            int yDif = position.y - bounds.position.y;

            if (pathTileMap.HasTile(position))
            {
                Vector3 cv = new Vector3((position.x * 0.5f) - (position.y * 0.5f), ((position.x + 1) * 0.25f) + (position.y * 0.25f), 0);
                //Vector3 cv = ConvertVector(position.x, position.y);
                pathingGrid[xDif, yDif] = true;
                //print(pathGrid.CellToWorld(position));

                if (cv == player.transform.position)
                {
                    playerX = xDif;
                    playerY = yDif;
                    this.saveX = playerX;
                    this.saveY = playerY;
                }

                if (nodeMap.ContainsKey(cv))
                {
                    foreach(int i in nodeMap[cv])
                    {
                        pathMap.Add(i, new Point(xDif, yDif));
                    }
                }

                pointToVector.Add(new Point(xDif, yDif), cv);
            }
        }
    }

    Vector3 ConvertVector(int x, int y)
    {
        return new Vector3((x * 0.5f) - (y * 0.5f), ((x + 1) * 0.25f) + (y * 0.25f), 0);
    }
}
