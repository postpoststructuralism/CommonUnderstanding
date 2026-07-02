SELECT e."Id", e."SourceNodeId", e."TargetNodeId", e."Relationship", e."ProvenanceJson"
FROM "UnderstandingEdges" e
WHERE (e."SourceNodeId" IN (176, 177, 178) OR e."TargetNodeId" IN (176, 177, 178))
  AND (e."SourceNodeId" IN (176, 177, 178) AND e."TargetNodeId" IN (176, 177, 178));