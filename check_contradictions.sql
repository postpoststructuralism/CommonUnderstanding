SELECT "Id", "SourceNodeId", "TargetNodeId", "Relationship", "Weight" 
FROM "UnderstandingEdges" 
WHERE "Relationship"='contradicts' 
ORDER BY "Weight" DESC;