using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Newtonsoft.Json;
using NitroxClient.Communication.Abstract;
using NitroxClient.MonoBehaviours;
using NitroxClient.Unity.Helper;
using NitroxModel.Core;
using NitroxModel.DataStructures;
using NitroxModel.DataStructures.GameLogic.Buildings.New;
using NitroxModel.Helper;
using NitroxModel.Packets;
using NitroxModel_Subnautica.DataStructures;
using UnityEngine;

namespace NitroxClient.GameLogic.Bases.New;

public class BuildingTester : MonoBehaviour
{
    private IPacketSender packetSender;
    public static BuildingTester Main;

    public Queue<Packet> BuildQueue;
    private bool working;
    public string TargetBaseName;
    public Base TargetBase;
    public int Cycle = 0;
    public int Slot = 0;
    private JsonSerializer serializer;
    public string SavePath = Path.Combine(NitroxUser.LauncherPath, "SavedBases");
    
    public NitroxId TempId;
    public Dictionary<NitroxId, DateTimeOffset> BasesCooldown;
    /// <summary>
    /// Time in milliseconds before local player can build on a base that was modified by another player
    /// </summary>
    private const int MULTIPLAYER_BUILD_COOLDOWN = 2000;

    public void Start()
    {
        Main = this;
        packetSender = NitroxServiceLocator.LocateService<IPacketSender>();
        BuildQueue = new();
        BasesCooldown = new();
        serializer = new();
        serializer.NullValueHandling = NullValueHandling.Ignore;
        serializer.TypeNameHandling = TypeNameHandling.Auto;
        CycleTargetBase();
    }

    public void Update()
    {
        CleanCooldowns();
        if (BuildQueue.Count > 0 && !working)
        {
            working = true;            
            StartCoroutine(SafelyTreatNextBuildCommand());
        }
    }

    private IEnumerator SafelyTreatNextBuildCommand()
    {
        Packet packet = BuildQueue.Dequeue();
        yield return CoroutineHelper.SafelyYieldEnumerator(TreatBuildCommand(packet), (exception) =>
        {
            Log.Error($"An error happened when treating build command {packet}:\n{exception}");
        });
        working = false;
    }

    private IEnumerator TreatBuildCommand(object buildCommand)
    {
        switch (buildCommand)
        {
            case PlaceGhost placeGhost:
                yield return BuildGhost(placeGhost);
                break;
            case PlaceModule placeModule:
                yield return BuildModule(placeModule);
                break;
            case ModifyConstructedAmount modifyConstructedAmount:
                PeekNextCommand(ref modifyConstructedAmount);
                yield return ProgressConstruction(modifyConstructedAmount);
                break;
            case UpdateBase updateBase:
                yield return UpdatePlacedBase(updateBase);
                break;
            case PlaceBase placeBase:
                yield return BuildBase(placeBase);
                break;
            case BaseDeconstructed baseDeconstructed:
                yield return DeconstructBase(baseDeconstructed);
                break;
            case PieceDeconstructed pieceDeconstructed:
                yield return DeconstructPiece(pieceDeconstructed);
                break;
        }
    }

    /// <summary>
    /// If the next build command is also a ModifyConstructedAmount applied on the same object, we'll just skip the current one to apply the new one.
    /// </summary>
    private void PeekNextCommand(ref ModifyConstructedAmount currentCommand)
    {
        if (BuildQueue.Count == 0 || BuildQueue.Peek() is not ModifyConstructedAmount nextCommand || !nextCommand.GhostId.Equals(currentCommand.GhostId))
        {
            return;
        }
        BuildQueue.Dequeue();
        currentCommand = nextCommand;
        PeekNextCommand(ref currentCommand);
    }

    public IEnumerator BuildGhost(PlaceGhost placeGhost)
    {
        Transform parent = GetParentOrGlobalRoot(placeGhost.ParentId);
        yield return NitroxBuild.RestoreGhost(parent, placeGhost.SavedGhost);
        BasesCooldown[placeGhost.ParentId ?? placeGhost.SavedGhost.NitroxId] = DateTimeOffset.Now;
    }

    public IEnumerator BuildModule(PlaceModule placeModule)
    {
        Transform parent = GetParentOrGlobalRoot(placeModule.ParentId);
        yield return NitroxBuild.RestoreModule(parent, placeModule.SavedModule);
        BasesCooldown[placeModule.ParentId ?? placeModule.SavedModule.NitroxId] = DateTimeOffset.Now;
    }

    public IEnumerator ProgressConstruction(ModifyConstructedAmount modifyConstructedAmount)
    {
        if (NitroxEntity.TryGetComponentFrom(modifyConstructedAmount.GhostId, out Constructable constructable))
        {
            BasesCooldown[modifyConstructedAmount.GhostId] = DateTimeOffset.Now;
            if (modifyConstructedAmount.ConstructedAmount == 0f)
            {
                constructable.constructedAmount = 0f;
                yield return constructable.ProgressDeconstruction();
                UnityEngine.Object.Destroy(constructable.gameObject);
                BasesCooldown.Remove(modifyConstructedAmount.GhostId);
                yield break;
            }
            else if (modifyConstructedAmount.ConstructedAmount == 1f)
            {
                constructable.SetState(true, true);
                yield break;
            }
            constructable.SetState(false, false);
            constructable.constructedAmount = modifyConstructedAmount.ConstructedAmount;
            yield return constructable.ProgressDeconstruction();
            constructable.UpdateMaterial();
        }
    }

    public IEnumerator BuildBase(PlaceBase placeBase)
    {
        if (NitroxEntity.TryGetComponentFrom(placeBase.FormerGhostId, out ConstructableBase constructableBase))
        {
            BaseGhost baseGhost = constructableBase.model.GetComponent<BaseGhost>();
            constructableBase.SetState(true, true);
            NitroxEntity.SetNewId(baseGhost.targetBase.gameObject, placeBase.FormerGhostId);
            BasesCooldown[placeBase.FormerGhostId] = DateTimeOffset.Now;
            yield break;
        }
        // yield return NitroxBuild.CreateBuild(placeBase.SavedBuild);
        // TODO: Ask for resync
    }

    public IEnumerator UpdatePlacedBase(UpdateBase updateBase)
    {
        // Is probably useless check but can probably be useful for desync detection
        if (!NitroxEntity.TryGetComponentFrom(updateBase.BaseId, out Base parentBase))
        {
            Log.Debug("Couldn't find the parentBase");
            // TODO: Ask for resync
            yield break;
        }
        if (NitroxEntity.TryGetComponentFrom(updateBase.FormerGhostId, out ConstructableBase constructableBase))
        {
            constructableBase.SetState(true, true);
            BasesCooldown[updateBase.BaseId] = DateTimeOffset.Now;
            yield break;
        }
        // TODO: Ask for resync
        /* Log.Debug("Not ok, rebuilding the whole base");
        parentBase.ghosts.ForEach(Destroy);
        yield return null;
        yield return updateBase.SavedBuild.ApplyToAsync(parentBase);
        yield return updateBase.SavedBuild.RestoreGhosts(parentBase);
        RefreshBase(parentBase);*/
    }

    public IEnumerator DeconstructBase(BaseDeconstructed baseDeconstructed)
    {
        if (!NitroxEntity.TryGetObjectFrom(baseDeconstructed.FormerBaseId, out GameObject baseObject))
        {
            // TODO: Ask for resync
            Log.Debug("Couldn't find the parentBase");
            yield break;
        }
        BaseDeconstructable[] deconstructableChildren = baseObject.GetComponentsInChildren<BaseDeconstructable>(true);
        Log.Debug($"Found {deconstructableChildren.Length} BaseDeconstructable in the base");

        if (deconstructableChildren.Length == 1 && deconstructableChildren[0])
        {
            Log.Debug("Will only deconstruct the base manually");
            using (packetSender.Suppress<BaseDeconstructed>())
            using (packetSender.Suppress<PieceDeconstructed>())
            {
                deconstructableChildren[0].Deconstruct();
            }
            BasesCooldown[baseDeconstructed.FormerBaseId] = DateTimeOffset.Now;
            yield break;
        }
        // TODO: Ask for resync
        /*UnityEngine.Object.Destroy(baseObject);
        yield return null;
        yield return NitroxBuild.RestoreGhost(LargeWorldStreamer.main.globalRoot.transform, baseDeconstructed.ReplacerGhost);*/
    }

    public IEnumerator DeconstructPiece(PieceDeconstructed pieceDeconstructed)
    {
        if (!NitroxEntity.TryGetComponentFrom(pieceDeconstructed.BaseId, out Base @base))
        {
            Log.Debug("Couldn't find the parentBase");
            yield break;
        }
        BuildPieceIdentifier pieceIdentifier = pieceDeconstructed.BuildPieceIdentifier;
        Transform cellObject = @base.GetCellObject(pieceIdentifier.BaseCell.ToUnity());
        if (!cellObject)
        {
            Log.Debug($"Couldn't find cell object {pieceIdentifier.BaseCell} when destructing piece {pieceDeconstructed}");
            yield break;
        }
        BaseDeconstructable[] deconstructableChildren = cellObject.GetComponentsInChildren<BaseDeconstructable>(true);
        // TODO: Fix this
        foreach (BaseDeconstructable baseDeconstructable in deconstructableChildren)
        {
            if (!BuildManager.TryGetIdentifier(baseDeconstructable, out BuildPieceIdentifier identifier) || !identifier.Equals(pieceIdentifier))
            {
                continue;
            }
            Log.Debug($"Found a BaseDeconstructable {baseDeconstructable.name}, will now deconstruct it manually");
            using (packetSender.Suppress<BaseDeconstructed>())
            using (packetSender.Suppress<PieceDeconstructed>())
            {
                TempId = pieceDeconstructed.PieceId;
                baseDeconstructable.Deconstruct();
                TempId = null;
            }
            BasesCooldown[pieceDeconstructed.BaseId] = DateTimeOffset.Now;
            yield break;
        }
        Log.Error("Couldn't find BaseDeconstructable to be destructed");
        // TODO: Ask for resync
    }

    private void RefreshBase(Base @base)
    {
        @base.StorePreviousFaces();
        @base.FixCorridorLinks();
        @base.RecalculateFlowData();
        @base.RebuildGeometry();
        if (!@base.isGhost)
        {
            for (int i = 0; i < @base.ghosts.Count; i++)
            {
                @base.ghosts[i].RecalculateTargetOffset();
            }
        }
    }

    private Transform GetParentOrGlobalRoot(NitroxId id)
    {
        Transform transform;
        if (id != null && NitroxEntity.TryGetObjectFrom(id, out GameObject parentObject))
        {
            transform = parentObject.transform;
        }
        else
        {
            transform = LargeWorldStreamer.main.globalRoot.transform;
        }
        return transform;
    }

    public IEnumerator IsAvailable()
    {
        yield return new WaitUntil(LargeWorldStreamer.main.IsWorldSettled);
        yield return Base.InitializeAsync();
    }

    private void CleanCooldowns()
    {
        DateTimeOffset now = DateTimeOffset.Now;
        List<NitroxId> keysToRemove = BasesCooldown.Where(entry => (now - entry.Value).TotalMilliseconds >= MULTIPLAYER_BUILD_COOLDOWN).Select(e => e.Key).ToList();
        keysToRemove.ForEach(key => BasesCooldown.Remove(key));
    }

    // TODO: Remove Legacy singleplayer testing code

    public void CycleTargetBase()
    {
        Base[] bases = LargeWorldStreamer.main.globalRoot.GetComponentsInChildren<Base>(true);
        if (Cycle >= bases.Length - 1)
        {
            Cycle = 0;
        }
        else
        {
            Cycle++;
        }

        if (Cycle < bases.Length)
        {
            TargetBase = bases[Cycle];
            TargetBaseName = TargetBase.name;
        }
        else
        {
            TargetBase = null;
            TargetBaseName = string.Empty;
        }
    }

    public void SaveGlobalRoot()
    {
        SavedGlobalRoot savedGlobalRoot = NitroxGlobalRoot.From(LargeWorldStreamer.main.globalRoot.transform);
        using (StreamWriter writer = File.CreateText(string.Format("{0}\\big-save{1}.json", SavePath, Slot)))
        {
            serializer.Serialize(writer, savedGlobalRoot);
        }
    }

    public void LoadGlobalRoot()
    {
        DateTimeOffset beginTime = DateTimeOffset.Now;
        using (StreamReader reader = File.OpenText(string.Format("{0}\\big-save{1}.json", SavePath, Slot)))
        {
            SavedGlobalRoot savedGlobalRoot = (SavedGlobalRoot)serializer.Deserialize(reader, typeof(SavedGlobalRoot));
            DateTimeOffset endTime = DateTimeOffset.Now;
            Log.Debug(string.Format("Took {0}ms to deserialize the SavedGlobalRoot\n{1}", (endTime - beginTime).TotalMilliseconds, savedGlobalRoot));
            StartCoroutine(LoadGlobalRootAsync(savedGlobalRoot));
        }
    }

    public void ResetGlobalRoot()
    {
        Transform globalRoot = LargeWorldStreamer.main.globalRoot.transform;
        for (int i = globalRoot.childCount - 1; i >= 0; i--)
        {
            UnityEngine.Object.Destroy(globalRoot.GetChild(i).gameObject);
        }
    }

    public IEnumerator LoadGlobalRootAsync(SavedGlobalRoot savedGlobalRoot)
    {
        DateTimeOffset beginTime = DateTimeOffset.Now;
        foreach (SavedBuild build in savedGlobalRoot.Builds)
        {
            yield return LoadBaseAsync(build);
        }
        yield return NitroxBuild.RestoreModules(LargeWorldStreamer.main.globalRoot.transform, savedGlobalRoot.Modules);
        yield return NitroxBuild.RestoreGhosts(LargeWorldStreamer.main.globalRoot.transform, savedGlobalRoot.Ghosts);
        DateTimeOffset endTime = DateTimeOffset.Now;
        Log.Debug($"Took {(endTime - beginTime).TotalMilliseconds}ms to fully load the SavedGlobalRoot");
    }

    public void LogCurrentBase()
    {
        Log.Debug(NitroxBase.From(TargetBase));
        if (TargetBase.TryGetComponentInParent(out ConstructableBase constructableBase))
        {
            foreach (Renderer renderer in constructableBase.ghostRenderers)
            {
                Log.Debug($"{renderer.name} (under {renderer.transform.parent.name}) bounds: {renderer.bounds}");
            }
        }
    }

    public void LogAndSaveCurrentBase()
    {
        if (!TargetBase)
        {
            Log.Debug("Base is null or not alive");
        }
        else
        {
            SavedBuild saved = NitroxBuild.From(TargetBase);
            Log.Debug($"Saved base: {TargetBase.name}\n{saved}");
            SaveBase(saved);
        }
    }

    private void SaveBase(SavedBuild savedBuild)
    {
        using (StreamWriter writer = File.CreateText(string.Format("{0}\\save{1}.json", SavePath, Slot)))
        {
            serializer.Serialize(writer, savedBuild);
        }
    }

    public void LoadBase()
    {
        DateTimeOffset beginTime = DateTimeOffset.Now;
        using (StreamReader reader = File.OpenText(string.Format("{0}\\save{1}.json", SavePath, Slot)))
        {
            SavedBuild savedBuild = (SavedBuild)serializer.Deserialize(reader, typeof(SavedBuild));
            DateTimeOffset endTime = DateTimeOffset.Now;
            Log.Debug(string.Format("Took {0}ms to deserialize the SavedBuild\n:{1}", (endTime - beginTime).TotalMilliseconds, savedBuild));
            StartCoroutine(LoadBaseAsync(savedBuild));
        }
    }

    private IEnumerator LoadBaseAsync(SavedBuild savedBuild)
    {
        DateTimeOffset beginTime = DateTimeOffset.Now;
        yield return NitroxBuild.CreateBuild(savedBuild);
        DateTimeOffset endTime = DateTimeOffset.Now;
        Log.Debug(string.Format("Took {0}ms to create the Base", (endTime - beginTime).TotalMilliseconds));
    }

    public void DeleteTargetBase()
    {
        if (TargetBase)
        {
            UnityEngine.Object.Destroy(TargetBase.gameObject);
        }
    }

}
