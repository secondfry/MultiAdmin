using System;
using System.Collections;
using System.Collections.Generic;
using GameConsole;
using Unity;
using UnityEngine;
using UnityEngine.Networking;
using UnityEngine.PostProcessing;

public class CharacterClassManager : NetworkBehaviour
{
	public void SetUnit(int unit)
	{
		this.NetworkntfUnit = unit;
	}

	public void SyncDeathPos(Vector3 v)
	{
		this.NetworkdeathPosition = v;
	}

	[ServerCallback]
	public void AllowContain()
	{
		if (!NetworkServer.active)
		{
			return;
		}
		if (TutorialManager.status)
		{
			return;
		}
		foreach (GameObject gameObject in PlayerManager.singleton.players)
		{
			if (Vector3.Distance(gameObject.transform.position, this.lureSpj.transform.position) < 1.97f)
			{
				CharacterClassManager component = gameObject.GetComponent<CharacterClassManager>();
				PlayerStats component2 = gameObject.GetComponent<PlayerStats>();
				if (component.klasy[component.curClass].team != Team.SCP && component.curClass != 2)
				{
					component2.HurtPlayer(new PlayerStats.HitInfo(10000f, "WORLD", "LURE"), gameObject);
					this.lureSpj.SetState(true);
				}
			}
		}
	}

	private void Start()
	{
        // Code for using translation files
        if (CharacterClassManager.staticClasses == null || CharacterClassManager.staticClasses.Length <= 0)
        {
            for (int curClass = 0; curClass < this.klasy.Length; curClass++)
            {
                this.klasy[curClass].fullName = TranslationReader.Get("Class_Names", curClass);
                this.klasy[curClass].description = TranslationReader.Get("Class_Descriptions", curClass);
            }

            CharacterClassManager.staticClasses = this.klasy;
        }
        else
        {
            this.klasy = CharacterClassManager.staticClasses;
        }

        this.lureSpj = UnityEngine.Object.FindObjectOfType<LureSubjectContainer>();

		this.scp049 = base.GetComponent<Scp049PlayerScript>();
		this.scp049_2 = base.GetComponent<Scp049_2PlayerScript>();
		this.scp079 = base.GetComponent<Scp079PlayerScript>();

        /*
         * The script for this already exists in game, this allows for it to be loaded anyways
         * (Prepare for future updates, am I right?)
         */
        this.scp096 = base.GetComponent<Scp096PlayerScript>();

        this.scp106 = base.GetComponent<Scp106PlayerScript>();
		this.scp173 = base.GetComponent<Scp173PlayerScript>();

		this.forceClass = ConfigFile.GetInt("server_forced_class", -1);
		this.smBanComputerFirstPick = ConfigFile.GetBool("NO_SCP079_FIRST", true);
		this.ciPercentage = (float) ConfigFile.GetInt("ci_on_start_percent", 10);
		this.smStartRoundTimer = ConfigFile.GetInt("START_ROUND_TIMER", 20);
		this.smWaitForPlayers = ConfigFile.GetInt("START_ROUND_MINIMUM_PLAYERS", 2) - 1;
		base.StartCoroutine("Init");

        /*
         * Original code had unneccesary "."s
         * 
         * ... + "..........................." ...
         */
		string teamRespawnQueue = ConfigFile.GetString("team_respawn_queue", "401431403144144");
		this.classTeamQueue.Clear();

        // Loops through each number from "teamRespawnQueue" and adds to the team respawn queue (Defaulting to team 4, NTF)
        for (int i = 0; i < teamRespawnQueue.Length; i++)
		{
			int item = 4;
			if (!int.TryParse(teamRespawnQueue[i].ToString(), out item))
			{
				item = 4;
			}
			this.classTeamQueue.Add((Team) item);
		}

        // While the team queue is shorter than max player count, add more NTF to spawn in
        while (this.classTeamQueue.Length < CustomNetworkManager.singleton.maxConnections)
        {
            this.classTeamQueue.Add((Team) 4);
        }

		if (!base.isLocalPlayer && TutorialManager.status)
		{
			this.ApplyProperties();
		}
		this.SetMaxHP(0, "SCP173_HP", this.klasy[0].maxHP);
		this.SetMaxHP(1, "CLASSD_HP", this.klasy[1].maxHP);
		this.SetMaxHP(3, "SCP106_HP", this.klasy[3].maxHP);
		this.SetMaxHP(4, "NTFSCIENTIST_HP", this.klasy[4].maxHP);
		this.SetMaxHP(5, "SCP049_HP", this.klasy[5].maxHP);
		this.SetMaxHP(6, "SCIENTIST_HP", this.klasy[6].maxHP);
		this.SetMaxHP(7, "SCP079_HP", this.klasy[7].maxHP);
		this.SetMaxHP(8, "CI_HP", this.klasy[8].maxHP);
		this.SetMaxHP(9, "SCP096_HP", this.klasy[9].maxHP); // As seen in "CharacterClassManager Class List.png", class 9 is SCP-096 for now
        this.SetMaxHP(10, "SCP049-2_HP", this.klasy[10].maxHP);
		this.SetMaxHP(11, "NTFL_HP", this.klasy[11].maxHP);
		this.SetMaxHP(12, "NTFC_HP", this.klasy[12].maxHP);
		this.SetMaxHP(13, "NTFG_HP", this.klasy[13].maxHP);

		this.smBan049 = ConfigFile.GetBool("SCP049_DISABLE", false);
        this.smBan079 = ConfigFile.GetBool("SCP079_DISABLE", true);
        this.smBan096 = ConfigFile.GetBool("SCP096_DISABLE", true);
        this.smBan106 = ConfigFile.GetBool("SCP106_DISABLE", false);
		this.smBan173 = ConfigFile.GetBool("SCP173_DISABLE", false);
        this.smBan457 = ConfigFile.GetBool("SCP457_DISABLE", true);

        // This should be discouraged, this could possibly allow for classes not fully implemented yet to be spawned in
        this.smForceSCPBans = ConfigFile.GetBool("FORCE_DISABLE_ENABLE", false);

        // For debugging, set this to true to output a class list (Though this can just be done through an in-game command)
        if (false)
        {
            for (int classID = 0; classID < this.klasy.Length; classID++)
            {
                ServerConsole.AddLog(string.Concat(new object[]
                {
                    "Class #",
                    classID,
                    ": ",
                    this.klasy[classID].fullName,
                    " - ",
                    this.klasy[classID].curClass.maxHP,
                    "HP"
                }));
            }
        }
    }

	private IEnumerator Init()
	{
		GameObject host = null;
		while (host == null)
		{
			host = GameObject.Find("Host");
			yield return new WaitForEndOfFrame();
		}
		while (this.seed == 0) // If the seed hasn't been set, generate one
		{
			this.seed = host.GetComponent<RandomSeedSync>().seed;
			UnityEngine.Object.FindObjectOfType<GameConsole.Console>().UpdateValue("seed", this.seed.ToString());
		}

		if (!base.isLocalPlayer)
		{
			yield break;
		}
		yield return new WaitForSeconds(2f);

		if (base.isServer)
		{
			if (ServerStatic.isDedicated)
			{
				ServerConsole.AddLog("Waiting for players..");
			}

			CursorManager.roundStarted = true;
			RoundStart rs = RoundStart.singleton;

			if (TutorialManager.status)
			{
				this.ForceRoundStart();
			}
			else
			{
				rs.ShowButton();
				int timeLeft = this.smStartRoundTimer;
				int maxPlayers = 1;

				while (rs.info != "started")
				{
					if (maxPlayers > this.smWaitForPlayers)
					{
						timeLeft--;
					}

					int playerCount = PlayerManager.singleton.players.Length;

					if (playerCount > maxPlayers)
					{
						maxPlayers = playerCount;

                        // If the server is full, start the game immediately
                        if (maxPlayers == NetworkManager.singleton.maxConnections)
                        {
                            timeLeft = 0;
                        }

                        /*
                         * Don't know why this isn't just adding five, like so
                         * 
                         * timeLeft += 5;
                         */
                        else if (timeLeft % 5 > 0)
                        {
                            timeLeft = timeLeft / 5 * 5 + 5;
                        }

                        else
                        {
                            timeLeft += 5;
                        }
					}

					if (timeLeft > 0)
					{
						this.CmdUpdateStartText(timeLeft.ToString());
					}
					else
					{
						this.ForceRoundStart();
					}

					yield return new WaitForSeconds(1f);
				}
			}

			CursorManager.roundStarted = false;
			this.CmdStartRound();
			this.SetRandomRoles(); // Set player's roles

			rs = null;
		}
		else
		{
			while (!host.GetComponent<CharacterClassManager>().roundStarted)
			{
				yield return new WaitForEndOfFrame();
			}
			yield return new WaitForSeconds(2f);
			if (this.curClass < 0)
			{
				this.CallCmdSuicide(default(PlayerStats.HitInfo));
			}
		}

		int iteration = 0;
		for (;;)
		{
			GameObject[] plys = PlayerManager.singleton.players;

			if (iteration >= plys.Length)
			{
				yield return new WaitForSeconds(3f);

				iteration = 0;
			}
			try
			{
				plys[iteration].GetComponent<CharacterClassManager>().InitSCPs(); // Initialize SCPs for all connected players
			}
			catch
			{
			}

			iteration++;

			yield return new WaitForEndOfFrame();
			plys = null;
		}
		yield break;
	}

	[Client]
	[Command]
	public void CmdSuicide(PlayerStats.HitInfo hitInfo)
	{
		if (!NetworkClient.active)
		{
			Debug.LogWarning("[Client] function 'System.Void CharacterClassManager::CmdSuicide(PlayerStats/HitInfo)' called on server");
			return;
		}
		hitInfo.amount = ((hitInfo.amount != 0f) ? hitInfo.amount : 999799f);
		base.GetComponent<PlayerStats>().HurtPlayer(hitInfo, base.gameObject);
	}

	public void ForceRoundStart()
	{
		this.smRoundStartTime = Time.time;
		ServerConsole.AddLog("New round has been started.");
		this.CmdUpdateStartText("started");
	}

	[ServerCallback]
	private void CmdUpdateStartText(string str)
	{
		if (!NetworkServer.active)
		{
			return;
		}
		RoundStart.singleton.Networkinfo = str;
	}

	public void InitSCPs()
	{
		if (this.curClass != -1 && !TutorialManager.status)
		{
			Class c = this.klasy[this.curClass];
			this.scp049.Init(this.curClass, c);
			this.scp049_2.Init(this.curClass, c);
			this.scp079.Init(this.curClass, c);
            this.scp096.Init(this.curClass, c); // Preparing for future SCP
            this.scp106.Init(this.curClass, c);
			this.scp173.Init(this.curClass, c);
		}
	}

	public void RegisterEscape()
	{
		this.CallCmdRegisterEscape(base.gameObject);
	}

	[Command(channel = 2)]
	private void CmdRegisterEscape(GameObject sender)
	{
		CharacterClassManager escapeeClassManager = sender.GetComponent<CharacterClassManager>();

		if (Vector3.Distance(sender.transform.position, base.GetComponent<Escape>().worldPosition) < (float) (base.GetComponent<Escape>().radius * 2))
		{
			RoundSummary roundSummary = GameObject.Find("Host").GetComponent<RoundSummary>();

			if (this.klasy[escapeeClassManager.curClass].team == Team.CDP)
			{
				roundSummary.summary.classD_escaped++;
				this.SetClassID(8);
				base.GetComponent<PlayerStats>().SetHPAmount(this.klasy[8].maxHP);
			}
			if (this.klasy[escapeeClassManager.curClass].team == Team.RSC)
			{
				roundSummary.summary.scientists_escaped++;
				this.SetClassID(4);
				base.GetComponent<PlayerStats>().SetHPAmount(this.klasy[4].maxHP);
			}
		}
	}

	public void ApplyProperties()
	{
		Class @class = this.klasy[this.curClass];
		this.InitSCPs();
		Inventory component = base.GetComponent<Inventory>();

        try
        {
            base.GetComponent<FootstepSync>().SetLoundness(@class.team);
        }
        catch
        {
        }

        if (base.isLocalPlayer)
		{
			base.GetComponent<Radio>().UpdateClass();
			base.GetComponent<Handcuffs>().CallCmdTarget(null);
			base.GetComponent<Spectator>().Init();
			base.GetComponent<Searching>().Init(@class.team == Team.SCP | @class.team == Team.RIP);
		}
		if (TutorialManager.status || base.isLocalPlayer)
		{
			if (@class.team == Team.RIP)
			{
				if (base.isLocalPlayer)
				{
					base.GetComponent<WeaponManager>().DisableAllWeaponCameras();
					base.GetComponent<Inventory>().DropAll();
					component.items.Clear();
					component.NetworkcurItem = -1;
					base.GetComponent<FirstPersonController>().enabled = false;
					UnityEngine.Object.FindObjectOfType<StartScreen>().PlayAnimation(this.curClass);
					base.GetComponent<HorrorSoundController>().horrorSoundSource.PlayOneShot(this.bell_dead);
					base.transform.position = new Vector3(0f, 2048f, 0f);
					base.transform.rotation = Quaternion.Euler(Vector3.zero);
					base.GetComponent<PlayerStats>().maxHP = @class.maxHP;
					this.unfocusedCamera.GetComponent<Camera>().enabled = false;
					this.unfocusedCamera.GetComponent<PostProcessingBehaviour>().enabled = false;
				}
				this.RefreshPlyModel(-1);
			}
			else
			{
				if (base.isLocalPlayer)
				{
					base.GetComponent<Scp106PlayerScript>().SetDoors();
					GameObject randomPosition = UnityEngine.Object.FindObjectOfType<SpawnpointManager>().GetRandomPosition(this.curClass);
					if (randomPosition != null)
					{
						base.transform.position = randomPosition.transform.position;
						base.transform.rotation = randomPosition.transform.rotation;
					}
					else
					{
						base.transform.position = this.deathPosition;
					}
					component.items.Clear();
					component.NetworkcurItem = -1;
					foreach (int id in @class.startItems)
					{
						component.AddItem(id, -4.65664672E+11f);
					}
					UnityEngine.Object.FindObjectOfType<StartScreen>().PlayAnimation(this.curClass);
					if (!base.GetComponent<HorrorSoundController>().horrorSoundSource.isPlaying)
					{
						base.GetComponent<HorrorSoundController>().horrorSoundSource.PlayOneShot(this.bell);
					}
					base.Invoke("EnableFPC", 0.2f);
				}
				this.RefreshPlyModel(-1);
				if (base.isLocalPlayer)
				{
					base.GetComponent<Radio>().NetworkcurPreset = 0;
					base.GetComponent<Radio>().CallCmdUpdatePreset(0);
					base.GetComponent<AmmoBox>().SetAmmoAmount();
					FirstPersonController component2 = base.GetComponent<FirstPersonController>();
					PlayerStats component3 = base.GetComponent<PlayerStats>();
					if (@class.postprocessingProfile != null && base.GetComponentInChildren<PostProcessingBehaviour>() != null)
					{
						base.GetComponentInChildren<PostProcessingBehaviour>().profile = @class.postprocessingProfile;
					}
					this.unfocusedCamera.GetComponent<Camera>().enabled = true;
					this.unfocusedCamera.GetComponent<PostProcessingBehaviour>().enabled = true;
					component2.m_WalkSpeed = @class.walkSpeed;
					component2.m_RunSpeed = @class.runSpeed;
					component2.m_UseHeadBob = @class.useHeadBob;
					component2.m_JumpSpeed = @class.jumpSpeed;
					base.GetComponent<WeaponManager>().SetRecoil(@class.classRecoil);
					int maxHP = @class.maxHP;
					component3.maxHP = maxHP;
					UnityEngine.Object.FindObjectOfType<UserMainInterface>().lerpedHP = (float) maxHP;
				}
				else
				{
					base.GetComponent<PlayerStats>().maxHP = @class.maxHP;
				}
			}
			if (base.isLocalPlayer)
			{
				UnityEngine.Object.FindObjectOfType<InventoryDisplay>().isSCP = (this.curClass == 2 | @class.team == Team.SCP);
				UnityEngine.Object.FindObjectOfType<InterfaceColorAdjuster>().ChangeColor(@class.classColor);
				return;
			}
		}
		else
		{
			this.RefreshPlyModel(-1);
		}
	}

	private void EnableFPC()
	{
		base.GetComponent<FirstPersonController>().enabled = true;
	}

	public void RefreshPlyModel(int classID = -1)
	{
		if (this.myModel != null)
		{
			UnityEngine.Object.Destroy(this.myModel);
		}
		Class @class = this.klasy[(classID >= 0) ? classID : this.curClass];
		if (@class.team != Team.RIP)
		{
			GameObject gameObject = UnityEngine.Object.Instantiate<GameObject>(@class.model_player);
			gameObject.transform.SetParent(base.gameObject.transform);
			gameObject.transform.localPosition = @class.model_offset.position;
			gameObject.transform.localRotation = Quaternion.Euler(@class.model_offset.rotation);
			gameObject.transform.localScale = @class.model_offset.scale;
			this.myModel = gameObject;
			if (this.myModel.GetComponent<Animator>() != null)
			{
				base.GetComponent<AnimationController>().animator = this.myModel.GetComponent<Animator>();
			}
			if (base.isLocalPlayer)
			{
				if (this.myModel.GetComponent<Renderer>() != null)
				{
					this.myModel.GetComponent<Renderer>().enabled = false;
				}
				Renderer[] componentsInChildren = this.myModel.GetComponentsInChildren<Renderer>();
				for (int i = 0; i < componentsInChildren.Length; i++)
				{
					componentsInChildren[i].enabled = false;
				}
				foreach (Collider collider in this.myModel.GetComponentsInChildren<Collider>())
				{
					if (collider.name != "LookingTarget")
					{
						collider.enabled = false;
					}
				}
			}
		}
		base.GetComponent<CapsuleCollider>().enabled = (@class.team != Team.RIP);
	}

	public void SetClassID(int id)
	{
		this.NetworkcurClass = id;
		if (id != 2 || base.isLocalPlayer)
		{
			this.aliveTime = 0f;
			this.ApplyProperties();
		}
	}

	public void InstantiateRagdoll(int id)
	{
		if (id < 0)
		{
			return;
		}
		Class @class = this.klasy[this.curClass];
		GameObject gameObject = UnityEngine.Object.Instantiate<GameObject>(@class.model_ragdoll);
		gameObject.transform.position = base.transform.position + @class.ragdoll_offset.position;
		gameObject.transform.rotation = Quaternion.Euler(base.transform.rotation.eulerAngles + @class.ragdoll_offset.rotation);
		gameObject.transform.localScale = @class.ragdoll_offset.scale;
	}

	public void SetRandomRoles()
	{
		MTFRespawn mtfRespawn = base.GetComponent<MTFRespawn>();

		if (base.isLocalPlayer && base.isServer)
		{
			List<GameObject> playerList = new List<GameObject>();
			List<GameObject> shuffledPlayerList = new List<GameObject>();

			foreach (GameObject item in PlayerManager.singleton.players)
			{
				playerList.Add(item);
			}

			while (playerList.Count > 0)
			{
				int index = UnityEngine.Random.Range(0, playerList.Count);
				shuffledPlayerList.Add(playerList[index]);
				playerList.RemoveAt(index);
			}

			GameObject[] shuffledPlyrs = shuffledPlayerList.ToArray();

			RoundSummary roundSummary = base.GetComponent<RoundSummary>();

			bool spawnChaos = false;

			if ((float) UnityEngine.Random.Range(0, 100) < this.ciPercentage)
			{
				spawnChaos = true;
			}

            // SCP-173
            this.klasy[0].banClass = this.smForceSCPBans ? this.smBan173 : (this.smBan173 ? true : this.klasy[0].banClass);

            // SCP-106
            this.klasy[3].banClass = this.smForceSCPBans ? this.smBan106 : (this.smBan106 ? true : this.klasy[3].banClass);

            // SCP-049
            this.klasy[5].banClass = this.smForceSCPBans ? this.smBan049 : (this.smBan049 ? true : this.klasy[5].banClass);

            // SCP-079
            this.klasy[7].banClass = this.smForceSCPBans ? (this.smBanComputerFirstPick ? true : this.smBan079) : (this.smBanComputerFirstPick || this.smBan079 ? true : this.klasy[7].banClass);

            // SCP-096
            this.klasy[9].banClass = this.smForceSCPBans ? this.smBan096 : (this.smBan096 ? true : this.klasy[9].banClass);


            /*
             * Unreleased SCP, not implemented yet
             * 
             * // SCP-457
             * this.klasy[9].banClass = this.smForceSCPBans ? this.smBan457 : (this.smBan457 ? true : this.klasy[9].banClass);
             */

            this.smFirstPick = true;

			for (int curPlayer = 0; curPlayer < shuffledPlyrs.Length; curPlayer++)
			{
				int randomClass = (this.forceClass != -1) ? this.forceClass : this.Find_Random_ID_Using_Defined_Team(this.classTeamQueue[curPlayer]);

				if (this.klasy[randomClass].team == Team.CDP)
				{
					roundSummary.summary.classD_start++;
				}

				if (this.klasy[randomClass].team == Team.RSC)
				{
					roundSummary.summary.scientists_start++;
				}

				if (this.klasy[randomClass].team == Team.SCP)
				{
					if (this.smBanComputerFirstPick && this.smFirstPick && !this.smBan079)
					{
						this.klasy[7].banClass = false;
					}
					this.smFirstPick = false;

					roundSummary.summary.scp_start++;
				}

				if (randomClass == 4)
				{
					if (spawnChaos)
					{
						randomClass = 8; // Number 8 is Chaos' class number
					}
					else
					{
						mtfRespawn.playersToNTF.Add(shuffledPlyrs[curPlayer]);
					}
				}

				if (TutorialManager.status)
				{
					this.SetPlayersClass(14, base.gameObject);
				}
				else if (randomClass != 4)
				{
					this.SetPlayersClass(randomClass, shuffledPlyrs[curPlayer]);
				}
			}

			mtfRespawn.SummonNTF();
		}
	}

	private void SetRoundStart(bool b)
	{
		this.NetworkroundStarted = b;
	}

	[ServerCallback]
	private void CmdStartRound()
	{
		if (!NetworkServer.active)
		{
			return;
		}
		if (!TutorialManager.status)
		{
			try
			{
				GameObject.Find("MeshDoor173").GetComponentInChildren<Door>().ForceCooldown((float) ConfigFile.GetInt("173_door_starting_cooldown", 25));
				UnityEngine.Object.FindObjectOfType<ChopperAutostart>().SetState(false);
			}
			catch
			{
			}
		}
		this.SetRoundStart(true);
	}

	[ServerCallback]
	public void SetPlayersClass(int classid, GameObject ply)
	{
		if (!NetworkServer.active)
		{
			return;
		}
		ply.GetComponent<CharacterClassManager>().SetClassID(classid);
		ply.GetComponent<PlayerStats>().SetHPAmount(this.klasy[classid].maxHP);
	}

	private int Find_Random_ID_Using_Defined_Team(Team team)
	{
		List<int> list = new List<int>();
		for (int i = 0; i < this.klasy.Length; i++)
		{
			if (this.klasy[i].team == team && !this.klasy[i].banClass)
			{
				list.Add(i);
			}
		}
		if (list.Count == 0)
		{
			return 1;
		}
		int index = UnityEngine.Random.Range(0, list.Count);
		if (this.klasy[list[index]].team == Team.SCP)
		{
			this.klasy[list[index]].banClass = true;
		}
		return list[index];
	}

	public bool SpawnProtection()
	{
		return this.aliveTime < 2f;
	}

	private void Update()
	{
		if (this.curClass == 2)
		{
			this.aliveTime = 0f;
		}
		else
		{
			this.aliveTime += Time.deltaTime;
		}
		if (base.isLocalPlayer)
		{
			if (ServerStatic.isDedicated)
			{
				CursorManager.isServerOnly = true;
			}
			if (base.isServer)
			{
				this.AllowContain();
			}
		}
		if (this.prevId != this.curClass)
		{
			this.RefreshPlyModel(-1);
			this.prevId = this.curClass;
		}
		if (base.name == "Host")
		{
			Radio.roundStarted = this.roundStarted;
		}
	}

	private void UNetVersion()
	{
	}

	public int NetworkntfUnit
	{
		get
		{
			return this.ntfUnit;
		}
		set
		{
			uint dirtyBit = 1u;
			if (NetworkServer.localClientActive && !base.syncVarHookGuard)
			{
				base.syncVarHookGuard = true;
				this.SetUnit(value);
				base.syncVarHookGuard = false;
			}
			base.SetSyncVar<int>(value, ref this.ntfUnit, dirtyBit);
		}
	}

	public int NetworkcurClass
	{
		get
		{
			return this.curClass;
		}
		set
		{
			uint dirtyBit = 2u;
			if (NetworkServer.localClientActive && !base.syncVarHookGuard)
			{
				base.syncVarHookGuard = true;
				this.SetClassID(value);
				base.syncVarHookGuard = false;
			}
			base.SetSyncVar<int>(value, ref this.curClass, dirtyBit);
		}
	}

	public Vector3 NetworkdeathPosition
	{
		get
		{
			return this.deathPosition;
		}
		set
		{
			uint dirtyBit = 4u;
			if (NetworkServer.localClientActive && !base.syncVarHookGuard)
			{
				base.syncVarHookGuard = true;
				this.SyncDeathPos(value);
				base.syncVarHookGuard = false;
			}
			base.SetSyncVar<Vector3>(value, ref this.deathPosition, dirtyBit);
		}
	}

	public bool NetworkroundStarted
	{
		get
		{
			return this.roundStarted;
		}
		set
		{
			uint dirtyBit = 8u;
			if (NetworkServer.localClientActive && !base.syncVarHookGuard)
			{
				base.syncVarHookGuard = true;
				this.SetRoundStart(value);
				base.syncVarHookGuard = false;
			}
			base.SetSyncVar<bool>(value, ref this.roundStarted, dirtyBit);
		}
	}

	protected static void InvokeCmdCmdSuicide(NetworkBehaviour obj, NetworkReader reader)
	{
		if (!NetworkServer.active)
		{
			Debug.LogError("Command CmdSuicide called on client.");
			return;
		}
		((CharacterClassManager)obj).CmdSuicide(GeneratedNetworkCode._ReadHitInfo_PlayerStats(reader));
	}

	protected static void InvokeCmdCmdRegisterEscape(NetworkBehaviour obj, NetworkReader reader)
	{
		if (!NetworkServer.active)
		{
			Debug.LogError("Command CmdRegisterEscape called on client.");
			return;
		}
		((CharacterClassManager)obj).CmdRegisterEscape(reader.ReadGameObject());
	}

	public void CallCmdSuicide(PlayerStats.HitInfo hitInfo)
	{
		if (!NetworkClient.active)
		{
			Debug.LogError("Command function CmdSuicide called on server.");
			return;
		}
		if (base.isServer)
		{
			this.CmdSuicide(hitInfo);
			return;
		}
		NetworkWriter networkWriter = new NetworkWriter();
		networkWriter.Write(0);
		networkWriter.Write(5);
		networkWriter.WritePackedUInt32((uint)CharacterClassManager.kCmdCmdSuicide);
		networkWriter.Write(base.GetComponent<NetworkIdentity>().netId);
		GeneratedNetworkCode._WriteHitInfo_PlayerStats(networkWriter, hitInfo);
		base.SendCommandInternal(networkWriter, 0, "CmdSuicide");
	}

	public void CallCmdRegisterEscape(GameObject sender)
	{
		if (!NetworkClient.active)
		{
			Debug.LogError("Command function CmdRegisterEscape called on server.");
			return;
		}
		if (base.isServer)
		{
			this.CmdRegisterEscape(sender);
			return;
		}
		NetworkWriter networkWriter = new NetworkWriter();
		networkWriter.Write(0);
		networkWriter.Write(5);
		networkWriter.WritePackedUInt32((uint)CharacterClassManager.kCmdCmdRegisterEscape);
		networkWriter.Write(base.GetComponent<NetworkIdentity>().netId);
		networkWriter.Write(sender);
		base.SendCommandInternal(networkWriter, 2, "CmdRegisterEscape");
	}

	static CharacterClassManager()
	{
		NetworkBehaviour.RegisterCommandDelegate(typeof(CharacterClassManager), CharacterClassManager.kCmdCmdSuicide, new NetworkBehaviour.CmdDelegate(CharacterClassManager.InvokeCmdCmdSuicide));
		CharacterClassManager.kCmdCmdRegisterEscape = -1826587486;
		NetworkBehaviour.RegisterCommandDelegate(typeof(CharacterClassManager), CharacterClassManager.kCmdCmdRegisterEscape, new NetworkBehaviour.CmdDelegate(CharacterClassManager.InvokeCmdCmdRegisterEscape));
		NetworkCRC.RegisterBehaviour("CharacterClassManager", 0);
	}

	public override bool OnSerialize(NetworkWriter writer, bool forceAll)
	{
		if (forceAll)
		{
			writer.WritePackedUInt32((uint)this.ntfUnit);
			writer.WritePackedUInt32((uint)this.curClass);
			writer.Write(this.deathPosition);
			writer.Write(this.roundStarted);
			return true;
		}
		bool flag = false;
		if ((base.syncVarDirtyBits & 1u) != 0u)
		{
			if (!flag)
			{
				writer.WritePackedUInt32(base.syncVarDirtyBits);
				flag = true;
			}
			writer.WritePackedUInt32((uint)this.ntfUnit);
		}
		if ((base.syncVarDirtyBits & 2u) != 0u)
		{
			if (!flag)
			{
				writer.WritePackedUInt32(base.syncVarDirtyBits);
				flag = true;
			}
			writer.WritePackedUInt32((uint)this.curClass);
		}
		if ((base.syncVarDirtyBits & 4u) != 0u)
		{
			if (!flag)
			{
				writer.WritePackedUInt32(base.syncVarDirtyBits);
				flag = true;
			}
			writer.Write(this.deathPosition);
		}
		if ((base.syncVarDirtyBits & 8u) != 0u)
		{
			if (!flag)
			{
				writer.WritePackedUInt32(base.syncVarDirtyBits);
				flag = true;
			}
			writer.Write(this.roundStarted);
		}
		if (!flag)
		{
			writer.WritePackedUInt32(base.syncVarDirtyBits);
		}
		return flag;
	}

	public override void OnDeserialize(NetworkReader reader, bool initialState)
	{
		if (initialState)
		{
			this.ntfUnit = (int)reader.ReadPackedUInt32();
			this.curClass = (int)reader.ReadPackedUInt32();
			this.deathPosition = reader.ReadVector3();
			this.roundStarted = reader.ReadBoolean();
			return;
		}
		uint num = reader.ReadPackedUInt32();
		if ((num & 1u) != 0u)
		{
			this.SetUnit((int)reader.ReadPackedUInt32());
		}
		if ((num & 2u) != 0u)
		{
			this.SetClassID((int)reader.ReadPackedUInt32());
		}
		if ((num & 4u) != 0u)
		{
			this.SyncDeathPos(reader.ReadVector3());
		}
		if ((num & 8u) != 0u)
		{
			this.SetRoundStart(reader.ReadBoolean());
		}
	}

	public void SetMaxHP(int id, string config_key, int defaultHp)
	{
        this.klasy[id].maxHP = ConfigFile.GetInt(config_key, defaultHp);
	}

	[SyncVar(hook = "SetUnit")]
	public int ntfUnit;

	public float ciPercentage;

	public int forceClass = -1;

	[SerializeField]
	private AudioClip bell;

	[SerializeField]
	private AudioClip bell_dead;

	[HideInInspector]
	public GameObject myModel;

	[HideInInspector]
	public GameObject charCamera;

	public Class[] klasy;

	public List<Team> classTeamQueue = new List<Team>();

	[SyncVar(hook = "SetClassID")]
	public int curClass;

	private int seed;

	private GameObject plyCam;

	public GameObject unfocusedCamera;

	[SyncVar(hook = "SyncDeathPos")]
	public Vector3 deathPosition;

	[SyncVar(hook = "SetRoundStart")]
	public bool roundStarted;

	private Scp049PlayerScript scp049;

	private Scp049_2PlayerScript scp049_2;

	private Scp079PlayerScript scp079;

    private Scp096PlayerScript scp096; // Preparing for future SCP

    private Scp106PlayerScript scp106;

	private Scp173PlayerScript scp173;

    private LureSubjectContainer lureSpj;

    private static Class[] staticClasses;

    private float aliveTime;

	private int prevId = -1;

	private static int kCmdCmdSuicide = -1051695024;

	private static int kCmdCmdRegisterEscape;

	public bool smBanComputerFirstPick;

	public bool smBan049;

    public bool smBan079;

    public bool smBan096;

    public bool smBan106;

	public bool smBan173;

	public bool smBan457;

    public bool smForceSCPBans;

    public bool smFirstPick;

	private int smStartRoundTimer;

	private int smWaitForPlayers;

	public float smRoundStartTime;
}
