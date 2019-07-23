﻿using System;
using System.Collections.Generic;
using System.Linq;
using SpeckleCore;
using SpeckleElementsClasses;

namespace SpeckleElementsGSA
{
  [GSAObject("GEN_REST.2", new string[] { }, "elements", true, true, new Type[] { typeof(GSANode), typeof(GSAConstructionStage) }, new Type[] { typeof(GSANode), typeof(GSAConstructionStage) })]
  public class GSAStructuralStagedNodalRestraints : IGSASpeckleContainer
  {
    public int GSAId { get; set; }
    public string GWACommand { get; set; }
    public List<string> SubGWACommand { get; set; } = new List<string>();
    public dynamic Value { get; set; } = new StructuralStagedNodalRestraints();

    public void ParseGWACommand(GSAInterfacer GSA, List<GSANode> nodes, List<GSAConstructionStage> stages)
    {
      if (this.GWACommand == null)
        return;

      StructuralStagedNodalRestraints obj = new StructuralStagedNodalRestraints();

      string[] pieces = this.GWACommand.ListSplit("\t");

      int counter = 1; // Skip identifier
      
      obj.Name = pieces[counter++];

      //Restraints
      var restraints = new bool[6];
      for (int i = 0; i < 6; i++)
        restraints[i] = pieces[counter++] == "1";
      obj.Restraint = new StructuralVectorBoolSix(restraints);
      
      int[] targetNodeRefs = GSA.ConvertGSAList(pieces[counter++], GSAEntity.NODE);

      if (nodes != null)
      {
        List<GSANode> targetNodes = nodes
            .Where(n => targetNodeRefs.Contains(n.GSAId)).ToList();

        obj.NodeRefs = targetNodes.Select(n => (string)n.Value.ApplicationId).ToList();
        this.SubGWACommand.AddRange(targetNodes.Select(n => n.GWACommand));

        foreach (GSANode n in targetNodes)
          n.ForceSend = true;
      }
      
      var gwaStageDefGsaIds = pieces[counter++].ListSplit(" ");
      obj.ConstructionStageRefs = stages.Where(sd => gwaStageDefGsaIds.Any(gwaSDId => gwaSDId == sd.GSAId.ToString())).Select(x => (string)x.Value.ApplicationId).ToList();

      this.Value = obj;
    }

    public void SetGWACommand(GSAInterfacer GSA)
    {
      if (this.Value == null)
        return;

      StructuralStagedNodalRestraints obj = this.Value as StructuralStagedNodalRestraints;
			var destinationType = typeof(GSAStructuralStagedNodalRestraints);

			string keyword = destinationType.GetGSAKeyword();
      var subkeywords = destinationType.GetSubGSAKeyword();

      int index = GSA.Indexer.ResolveIndex(destinationType, obj);

      var nodesStr = "none"; //default value
      if (obj.NodeRefs != null && obj.NodeRefs.Count() >= 1)
      {
        var nodeIndices = GSA.Indexer.LookupIndices(typeof(GSANode), obj.NodeRefs);
        nodesStr = string.Join(" ", nodeIndices);
      }

      var stageDefStr = "all"; //default value
      if (obj.ConstructionStageRefs != null && obj.ConstructionStageRefs.Count() >= 1)
      {
        var stageDefIndices = GSA.Indexer.LookupIndices(typeof(GSAConstructionStage), obj.ConstructionStageRefs);
        stageDefStr = string.Join(" ", stageDefIndices);
      }

      var ls = new List<string>
      {
        "SET_AT",
        index.ToString(),
        keyword + ":" + GSA.GenerateSID(obj),
				string.IsNullOrEmpty(obj.Name) ? " " : obj.Name
			};

			ls.AddRange(obj.Restraint.Value.Select(v => v ? "1" : "0"));
			ls.Add(nodesStr);
			ls.Add(stageDefStr);

      GSA.RunGWACommand(string.Join("\t", ls));
    }
  }

  public static partial class Conversions
  {
    public static bool ToNative(this StructuralStagedNodalRestraints restraint)
    {
      var gsaGeneralisedNodeRestraints = new GSAStructuralStagedNodalRestraints() { Value = restraint };

      gsaGeneralisedNodeRestraints.SetGWACommand(GSA);

      return true;
    }

    public static SpeckleObject ToSpeckle(this GSAStructuralStagedNodalRestraints dummyObject)
    {
      var destinationType = typeof(GSAStructuralStagedNodalRestraints);

      if (!GSASenderObjects.ContainsKey(destinationType))
        GSASenderObjects[destinationType] = new List<object>();

      var genNodeRestraints = new List<GSAStructuralStagedNodalRestraints>();

      var stageDefs = GSASenderObjects[typeof(GSAConstructionStage)].Cast<GSAConstructionStage>().ToList();
      var nodes = GSASenderObjects[typeof(GSANode)].Cast<GSANode>().ToList();

      string keyword = destinationType.GetGSAKeyword();
      string[] subKeywords = destinationType.GetSubGSAKeyword();

      string[] lines = GSA.GetGWARecords("GET_ALL\t" + keyword);
      List<string> deletedLines = GSA.GetDeletedGWARecords("GET_ALL\t" + keyword).ToList();
      foreach (string k in subKeywords)
        deletedLines.AddRange(GSA.GetDeletedGWARecords("GET_ALL\t" + k));

      // Remove deleted lines
      GSASenderObjects[destinationType].RemoveAll(l => deletedLines.Contains((l as IGSASpeckleContainer).GWACommand));
      foreach (var kvp in GSASenderObjects)
        kvp.Value.RemoveAll(l => (l as IGSASpeckleContainer).SubGWACommand.Any(x => deletedLines.Contains(x)));

      // Filter only new lines
      string[] prevLines = GSASenderObjects[destinationType].Select(l => (l as IGSASpeckleContainer).GWACommand).ToArray();
      string[] newLines = lines.Where(l => !prevLines.Contains(l)).ToArray();

      foreach (string p in newLines)
      {
        var genNodeRestraint = new GSAStructuralStagedNodalRestraints() { GWACommand = p };
        genNodeRestraint.ParseGWACommand(GSA, nodes, stageDefs);
        genNodeRestraints.Add(genNodeRestraint);
      }

      GSASenderObjects[destinationType].AddRange(genNodeRestraints);

      if (genNodeRestraints.Count() > 0 || deletedLines.Count() > 0) return new SpeckleObject();

      return new SpeckleNull();
    }
  }
}